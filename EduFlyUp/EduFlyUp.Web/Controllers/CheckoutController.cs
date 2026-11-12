using System.Data.Common;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Student")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CheckoutController(ICheckoutService checkout, ICheckoutCouponService coupons,
    ICheckoutQueryService queries, ICheckoutPaymentService payments, IOptions<BankTransferOptions> paymentOptions,
    ILogger<CheckoutController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
        => !ModelState.IsValid || courseId <= 0 ? BadRequest() : await CreateForm(new() { CourseId = courseId, SubmissionKey = Guid.NewGuid() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CourseId,SubmissionKey")] CheckoutCreateForm form)
    {
        if (form.CourseId <= 0) return BadRequest();
        if (form.SubmissionKey == Guid.Empty) ModelState.AddModelError("", "Phiên đặt hàng không hợp lệ. Vui lòng mở lại trang đăng ký.");
        if (!ModelState.IsValid) { Response.StatusCode = 400; return await CreateForm(form); }
        var result = await checkout.CreateAsync(User, new(form.SubmissionKey, [form.CourseId]));
        if (result.Order is not null && result.Change is CheckoutChange.Created or CheckoutChange.AlreadySubmitted or CheckoutChange.PendingExists)
        {
            TempData["SuccessMessage"] = result.Change == CheckoutChange.PendingExists ? "Bạn đã có đơn đang chờ cho khóa học này." :
                result.Order.Status == CheckoutOrderStatus.Paid ? "Đăng ký khóa học miễn phí thành công." : "Đã tạo đơn. Bạn có thể áp dụng mã giảm giá.";
            return RedirectToAction(nameof(Details), new { id = result.Order.PublicId });
        }
        if (result.Change == CheckoutChange.Forbidden) return Forbid();
        ModelState.AddModelError("", result.Change switch
        {
            CheckoutChange.AlreadyEnrolled => "Bạn đã đăng ký khóa học này.",
            CheckoutChange.CourseUnavailable => "Khóa học hiện không thể đăng ký. Vui lòng chọn khóa khác.",
            CheckoutChange.SubmissionConflict => "Phiên đặt hàng đã dùng cho khóa khác. Vui lòng mở lại trang đăng ký.",
            CheckoutChange.Unavailable => "Chưa tạo được đơn. Vui lòng thử lại với phiên đặt hàng này.",
            _ => "Thông tin đặt hàng không hợp lệ."
        });
        Response.StatusCode = result.Change == CheckoutChange.Unavailable ? 503 : result.Change == CheckoutChange.Invalid ? 400 : 409;
        return await CreateForm(form);
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] Guid id)
    {
        if (!ModelState.IsValid || id == Guid.Empty) return BadRequest();
        try { var order = await queries.DetailAsync(User, id); return order is null ? NotFound() : View(order); }
        catch (Exception ex) when (ex is DbException or DbUpdateException) { return ReadFailure(ex); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PreparePayment([FromRoute] Guid id, [Bind("Version")] CheckoutPaymentForm form)
    {
        if (!ModelState.IsValid || id == Guid.Empty) return BadRequest();
        if (!CheckoutPaymentDisplay.Available(paymentOptions.Value)) return StatusCode(503, "Chưa mở thanh toán.");
        var result = await payments.PrepareAsync(User, new(id, form.Version));
        if (result.Change == CheckoutPaymentChange.Forbidden) return Forbid();
        if (result.Change == CheckoutPaymentChange.NotFound) return NotFound();
        if (result.Change is CheckoutPaymentChange.Prepared or CheckoutPaymentChange.AlreadyPrepared)
            return RedirectToAction(nameof(Details), new { id });
        ModelState.AddModelError("", result.Change switch
        {
            CheckoutPaymentChange.Stale => "Đơn đã thay đổi. Kiểm tra tổng tiền và thử lại.",
            CheckoutPaymentChange.NotPayable => "Đơn không còn khả dụng để thanh toán.",
            CheckoutPaymentChange.Invalid => "Thông tin đơn không hợp lệ. Tải lại trang để thử lại.",
            _ => "Chưa tạo được QR. Vui lòng thử lại sau."
        });
        try
        {
            var order = await queries.DetailAsync(User, id); if (order is null) return NotFound();
            ModelState.Remove("Version");
            Response.StatusCode = result.Change is CheckoutPaymentChange.Unavailable or CheckoutPaymentChange.NotConfigured ? 503 :
                result.Change == CheckoutPaymentChange.Invalid ? 400 : 409;
            return View("Details", order);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException) { return ReadFailure(ex); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon([FromRoute] Guid id, [Bind("Code,Version")] CheckoutCouponForm form)
    {
        if (!ModelState.IsValid || id == Guid.Empty) return BadRequest();
        var result = await coupons.ApplyAsync(User, new(id, form.Code, form.Version));
        if (result.Change == CheckoutCouponChange.Forbidden) return Forbid();
        if (result.Change == CheckoutCouponChange.NotFound) return NotFound();
        if (result.Change == CheckoutCouponChange.Applied)
        {
            TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(form.Code) ? "Đã gỡ mã giảm giá." : "Đã áp dụng mã giảm giá.";
            return RedirectToAction(nameof(Details), new { id });
        }
        ModelState.AddModelError("", result.Change switch
        {
            CheckoutCouponChange.Stale => "Đơn đã thay đổi. Kiểm tra tổng tiền mới và thử lại.",
            CheckoutCouponChange.NotEditable => "Đơn hiện không thể thay đổi mã giảm giá.",
            CheckoutCouponChange.Exhausted => "Mã đã hết lượt hoặc các lượt còn lại đang được giữ chỗ.",
            CheckoutCouponChange.Unavailable => "Chưa cập nhật được mã. Vui lòng thử lại.",
            _ => "Mã không hợp lệ, hết hạn hoặc không áp dụng cho khóa học trong đơn."
        });
        try
        {
            var order = await queries.DetailAsync(User, id); if (order is null) return NotFound();
            // Lấy lại version từ server khi trả lỗi; không để tag helper dùng version cũ của POST.
            ModelState.Remove("Version");
            Response.StatusCode = result.Change == CheckoutCouponChange.Unavailable ? 503 : result.Change == CheckoutCouponChange.Invalid ? 400 : 409;
            return View("Details", order);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException) { return ReadFailure(ex); }
    }

    private async Task<IActionResult> CreateForm(CheckoutCreateForm form)
    {
        try { form.Preview = await queries.PreviewAsync(User, form.CourseId); return form.Preview is null ? NotFound() : View("Create", form); }
        catch (Exception ex) when (ex is DbException or DbUpdateException) { return ReadFailure(ex); }
    }
    private IActionResult ReadFailure(Exception ex)
    {
        logger.LogWarning(ex, "Không thể tải thông tin checkout.");
        return StatusCode(503, "Chưa tải được thông tin đơn hàng. Vui lòng thử lại sau.");
    }
}
