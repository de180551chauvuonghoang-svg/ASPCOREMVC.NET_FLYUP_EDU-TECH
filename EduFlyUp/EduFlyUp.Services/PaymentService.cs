using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EduFlyUp.Services;

public class PaymentService : IPaymentService
{
    private readonly IOrderRepository _orderRepo;
    private readonly ICourseRepository _courseRepo;
    private readonly IEnrollmentRepository _enrollmentRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IOrderRepository orderRepo,
        ICourseRepository courseRepo,
        IEnrollmentRepository enrollmentRepo,
        IConfiguration config,
        ILogger<PaymentService> logger)
    {
        _orderRepo = orderRepo;
        _courseRepo = courseRepo;
        _enrollmentRepo = enrollmentRepo;
        _config = config;
        _logger = logger;
    }

    public async Task<CheckoutViewModel> GenerateCheckoutAsync(string userId, int courseId, string? couponCode = null)
    {
        var course = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new InvalidOperationException($"Không tìm thấy khóa học với ID {courseId}.");

        decimal originalAmount = course.Price;
        decimal amount = originalAmount;
        decimal discountAmount = 0;
        string? appliedCoupon = null;
        string? couponMessage = null;
        bool couponSuccess = false;

        // Xử lý kiểm tra và áp dụng mã giảm giá nếu người dùng nhập
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var normCode = couponCode.Trim().ToUpperInvariant();
            using var db = new AppDbContext();
            var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == normCode);
            if (coupon == null || !coupon.IsActive)
            {
                couponMessage = "Mã giảm giá không tồn tại hoặc đã bị vô hiệu hóa.";
            }
            else if (coupon.EndDate < DateTime.UtcNow)
            {
                couponMessage = "Mã giảm giá này đã hết hạn sử dụng.";
            }
            else if (coupon.MaxUsage > 0 && coupon.UsedCount >= coupon.MaxUsage)
            {
                couponMessage = "Mã giảm giá đã hết lượt sử dụng.";
            }
            else if (coupon.CourseId.HasValue && coupon.CourseId != courseId)
            {
                couponMessage = "Mã giảm giá này không áp dụng cho khóa học hiện tại.";
            }
            else if (!string.IsNullOrEmpty(coupon.InstructorId) &&
                     !string.Equals(coupon.InstructorId, course.InstructorId, StringComparison.OrdinalIgnoreCase))
            {
                couponMessage = "Mã giảm giá không thuộc giảng viên của khóa học này.";
            }
            else
            {
                discountAmount = Math.Round(originalAmount * coupon.DiscountPercent / 100m, 0);
                amount = Math.Max(0, originalAmount - discountAmount);
                appliedCoupon = coupon.Code;
                couponSuccess = true;
                couponMessage = $"Áp dụng mã {coupon.Code} thành công! Giảm {coupon.DiscountPercent}% (-{discountAmount:N0} đ)";
            }
        }

        // Kiểm tra xem học viên có đơn hàng Pending chưa
        var existingOrder = await _orderRepo.GetUserPendingOrderAsync(userId, courseId);
        string orderCode;

        if (existingOrder != null)
        {
            orderCode = existingOrder.OrderCode;
            if (existingOrder.Amount != amount)
            {
                await _orderRepo.UpdateAmountAsync(existingOrder.Id, amount);
            }
        }
        else
        {
            // Sinh mã thanh toán ngẫu nhiên duy nhất: EDUPAY + 6 chữ số
            orderCode = $"EDUPAY{Random.Shared.Next(100000, 999999)}";
            await _orderRepo.CreateAsync(new Order
            {
                OrderCode = orderCode,
                UserId = userId,
                CourseId = courseId,
                Amount = amount,
                Status = OrderStatus.Pending,
                PaymentMethod = "VietQR_SePay"
            });
        }

        var bankBin = _config["SePay:BankBin"] ?? _config["SEPAY_BANK_BIN"] ?? "970423"; // 970423 là BIN TPBank
        var bankName = _config["SePay:BankName"] ?? _config["SEPAY_BANK_NAME"] ?? "TPBank";
        var bankAcc = BankAccountNumber;
        var accountName = _config["SePay:AccountHolderName"] ?? _config["SEPAY_ACCOUNT_HOLDER_NAME"] ?? "CHAU VUONG HOANG";

        // URL tạo ảnh VietQR chuẩn Napas 247
        var qrImageUrl = $"https://img.vietqr.io/image/{bankBin}-{bankAcc}-compact2.png?amount={(long)amount}&addInfo={orderCode}&accountName={Uri.EscapeDataString(accountName)}";

        return new CheckoutViewModel(
            OrderCode: orderCode,
            CourseId: course.Id,
            CourseTitle: course.Title,
            CourseThumbnail: string.IsNullOrEmpty(course.ThumbnailUrl) ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80" : course.ThumbnailUrl,
            Amount: amount,
            QrImageUrl: qrImageUrl,
            BankBin: bankBin,
            BankName: bankName,
            BankAccountNumber: bankAcc,
            AccountHolderName: accountName,
            TransferContent: orderCode,
            OriginalAmount: originalAmount,
            DiscountAmount: discountAmount,
            AppliedCouponCode: appliedCoupon,
            CouponMessage: couponMessage,
            CouponSuccess: couponSuccess
        );
    }

    public async Task<bool> ProcessSePayWebhookAsync(string? authHeader, SePayWebhookPayload payload)
    {
        var configuredApiKey = _config["SePay:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey) ||
            !AuthenticationHeaderValue.TryParse(authHeader, out var authorization) ||
            !string.Equals(authorization.Scheme, "Apikey", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(authorization.Parameter),
                Encoding.UTF8.GetBytes(configuredApiKey)))
        {
            _logger.LogWarning("SePay Webhook: Xác thực thất bại.");
            return false;
        }

        if (payload is null || payload.id <= 0 || payload.transferAmount <= 0 ||
            !string.Equals(payload.transferType, "in", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(payload.accountNumber, BankAccountNumber, StringComparison.Ordinal))
        {
            return false;
        }

        _logger.LogInformation("SePay Webhook nhận giao dịch: ID={Id}", payload.id);

        // Nội dung giao dịch có thể nằm ở trường content hoặc description
        var rawContent = $"{payload.content} {payload.description}";
        var codes = Regex.Matches(rawContent, @"(?<![A-Za-z0-9])EDUPAY[0-9]{6}(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Take(2).ToArray();
        return codes.Length == 1 &&
            await _orderRepo.MarkAsPaidAsync(codes[0], payload.id, payload.transferAmount, payload.referenceCode);
    }

    private string BankAccountNumber =>
        _config["SePay:BankAccountNumber"] ?? _config["SEPAY_BANK_ACCOUNT_NUMBER"] ?? "25816798888";

    public async Task<Order?> GetOrderByCodeAsync(string orderCode)
    {
        return await _orderRepo.GetByCodeAsync(orderCode);
    }

    public async Task<bool> IsCourseEnrolledAsync(string userId, int courseId)
    {
        return await _enrollmentRepo.IsEnrolledAsync(userId, courseId);
    }
}
