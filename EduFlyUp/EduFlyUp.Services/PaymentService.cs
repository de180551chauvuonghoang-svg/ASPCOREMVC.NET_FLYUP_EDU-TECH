using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
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

    public async Task<CheckoutViewModel> GenerateCheckoutAsync(string userId, int courseId)
    {
        var course = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new InvalidOperationException($"Không tìm thấy khóa học với ID {courseId}.");

        // Kiểm tra xem học viên có đơn hàng Pending chưa
        var existingOrder = await _orderRepo.GetUserPendingOrderAsync(userId, courseId);
        string orderCode;
        decimal amount = course.Price;

        if (existingOrder != null)
        {
            orderCode = existingOrder.OrderCode;
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
        var bankAcc = _config["SePay:BankAccountNumber"] ?? _config["SEPAY_BANK_ACCOUNT_NUMBER"] ?? "25816798888";
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
            TransferContent: orderCode
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
            !string.Equals(payload.transferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _logger.LogInformation("SePay Webhook nhận giao dịch: ID={Id}", payload.id);

        // Nội dung giao dịch có thể nằm ở trường content hoặc description
        var rawContent = $"{payload.content} {payload.description}";
        var match = Regex.Match(rawContent, @"EDUPAY\d{6}", RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            _logger.LogInformation("SePay Webhook: Không tìm thấy mã EDUPAY.");
            return false;
        }

        var orderCode = match.Value.ToUpper();
        var order = await _orderRepo.GetByCodeAsync(orderCode);

        if (order == null)
        {
            _logger.LogWarning("SePay Webhook: Không tìm thấy đơn hàng với mã {OrderCode}", orderCode);
            return false;
        }

        if (order.Status == OrderStatus.Paid)
        {
            _logger.LogInformation("SePay Webhook: Đơn hàng {OrderCode} đã được thanh toán trước đó.", orderCode);
            return true;
        }

        // Kiểm tra số tiền chuyển phải đủ số tiền khóa học
        if (payload.transferAmount >= order.Amount)
        {
            var txnRef = payload.referenceCode ?? payload.id.ToString();
            var success = await _orderRepo.MarkAsPaidAsync(orderCode, txnRef);

            if (success)
            {
                _logger.LogInformation("🎉 Thanh toán thành công cho đơn hàng {OrderCode}, kích hoạt khóa học {CourseId} cho user {UserId}!",
                    orderCode, order.CourseId, order.UserId);
                return true;
            }
        }
        else
        {
            _logger.LogWarning("SePay Webhook: Số tiền thanh toán ({PaidAmount}) không đủ giá trị đơn hàng ({OrderAmount}) cho đơn {OrderCode}",
                payload.transferAmount, order.Amount, orderCode);
        }

        return false;
    }

    public async Task<Order?> GetOrderByCodeAsync(string orderCode)
    {
        return await _orderRepo.GetByCodeAsync(orderCode);
    }

    public async Task<bool> IsCourseEnrolledAsync(string userId, int courseId)
    {
        return await _enrollmentRepo.IsEnrolledAsync(userId, courseId);
    }
}
