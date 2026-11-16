using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

public class PaymentController : Controller
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // GET: /Payment/Checkout/5?coupon=NODEJS50
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Checkout(int id, [FromQuery] string? coupon = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            var returnUrl = $"/Payment/Checkout/{id}" + (!string.IsNullOrEmpty(coupon) ? $"?coupon={coupon}" : "");
            return RedirectToAction("Login", "Account", new { returnUrl });
        }

        // Kiểm tra xem học viên đã sở hữu khóa học này chưa (chặn không cho mua 2 lần)
        if (await _paymentService.IsCourseEnrolledAsync(userId, id))
        {
            TempData["SuccessMessage"] = "Bạn đã sở hữu khóa học này rồi! Không thể mua hai lần.";
            return RedirectToAction("MyCourses", "Courses");
        }

        try
        {
            var model = await _paymentService.GenerateCheckoutAsync(userId, id, coupon);
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Details", "Courses", new { id });
        }
    }

    // API Polling: Trình duyệt gọi định kỳ 3 giây/lần để phát hiện khi có tiền về
    // GET: /api/payments/status/EDUPAY123456
    [HttpGet("/api/payments/status/{orderCode}")]
    public async Task<IActionResult> CheckStatus(string orderCode)
    {
        var order = await _paymentService.GetOrderByCodeAsync(orderCode);
        if (order == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        }

        return Ok(new
        {
            success = true,
            orderCode = order.OrderCode,
            status = order.Status.ToString(),
            isPaid = order.Status == OrderStatus.Paid,
            courseId = order.CourseId
        });
    }

    // Webhook Endpoint nhận tín hiệu biến động số dư từ SePay
    // POST: /api/payments/sepay-webhook
    [HttpPost("/api/payments/sepay-webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookPayload payload)
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        var processed = await _paymentService.ProcessSePayWebhookAsync(authHeader, payload);

        // Trả về 200 cho SePay để xác nhận đã tiếp nhận
        return Ok(new
        {
            success = processed,
            message = processed ? "Giao dịch đã được xử lý và kích hoạt thành công." : "Giao dịch được ghi nhận hoặc bỏ qua."
        });
    }
}
