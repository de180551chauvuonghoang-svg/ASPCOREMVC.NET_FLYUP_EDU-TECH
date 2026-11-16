using System.Data.Common;
using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPayoutsController(AdminPayoutDAO payouts, ILogger<AdminPayoutsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, PayoutStatus? status = PayoutStatus.Pending)
    {
        if (!ModelState.IsValid || (status.HasValue && !Enum.IsDefined(status.Value)))
            return BadRequest();
        return View(await payouts.ListAsync(page, status));
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        if (!ModelState.IsValid || id <= 0)
            return BadRequest();
        var payout = await payouts.DetailAsync(id);
        return payout is null ? NotFound() : View(payout);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ConfirmTransfer([FromRoute] int id, [FromForm] string? note) =>
        ProcessAsync(id, transferred: true, note);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reject([FromRoute] int id, [FromForm] string? note) =>
        ProcessAsync(id, transferred: false, note);

    private async Task<IActionResult> ProcessAsync(int id, bool transferred, string? note)
    {
        if (!ModelState.IsValid || id <= 0)
            return BadRequest();
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        AdminPayoutChange result;
        try
        {
            result = await payouts.ProcessAsync(id, adminId, transferred, note);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogWarning(ex, "Lỗi xử lý yêu cầu rút tiền {PayoutId}", id);
            return StatusCode(503, "Chưa cập nhật được yêu cầu. Vui lòng mở lại chi tiết để kiểm tra.");
        }

        if (result == AdminPayoutChange.Forbidden)
            return Forbid();
        if (result == AdminPayoutChange.NotFound)
            return NotFound();
        if (result == AdminPayoutChange.Updated)
        {
            logger.LogInformation("Quản trị viên {AdminId} xử lý yêu cầu {PayoutId}. Đã chuyển: {Transferred}",
                adminId, id, transferred);
            TempData["SuccessMessage"] = transferred ? "Đã ghi nhận chuyển khoản." : "Đã từ chối yêu cầu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result == AdminPayoutChange.AlreadyProcessed)
        {
            TempData["ErrorMessage"] = "Yêu cầu đã được xử lý.";
            return RedirectToAction(nameof(Details), new { id });
        }

        Response.StatusCode = result == AdminPayoutChange.Invalid ? 400 : 409;
        ModelState.AddModelError("", result == AdminPayoutChange.Invalid
            ? "Nhập mã chuyển khoản hoặc lý do, tối đa 250 ký tự."
            : "Chưa xác minh được đủ số dư cho yêu cầu.");
        return View("Details", await payouts.DetailAsync(id));
    }
}
