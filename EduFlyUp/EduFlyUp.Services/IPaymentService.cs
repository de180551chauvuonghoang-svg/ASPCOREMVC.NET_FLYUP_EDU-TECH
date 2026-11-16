using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Services;

public record CheckoutViewModel(
    string OrderCode,
    int CourseId,
    string CourseTitle,
    string CourseThumbnail,
    decimal Amount,
    string QrImageUrl,
    string BankBin,
    string BankName,
    string BankAccountNumber,
    string AccountHolderName,
    string TransferContent,
    decimal OriginalAmount = 0,
    decimal DiscountAmount = 0,
    string? AppliedCouponCode = null,
    string? CouponMessage = null,
    bool CouponSuccess = false
);

public class SePayWebhookPayload
{
    public long id { get; set; }
    public string? gateway { get; set; }
    public string? transactionDate { get; set; }
    public string? accountNumber { get; set; }
    public string? code { get; set; }
    public string? content { get; set; }
    public string? transferType { get; set; }
    public decimal transferAmount { get; set; }
    public decimal accumulated { get; set; }
    public string? subAccount { get; set; }
    public string? referenceCode { get; set; }
    public string? description { get; set; }
}

public interface IPaymentService
{
    Task<CheckoutViewModel> GenerateCheckoutAsync(string userId, int courseId, string? couponCode = null);
    Task<bool> ProcessSePayWebhookAsync(string? authHeader, SePayWebhookPayload payload);
    Task<Order?> GetOrderByCodeAsync(string orderCode);
    Task<bool> IsCourseEnrolledAsync(string userId, int courseId);
}
