namespace EduFlyUp.BusinessObjects;

public enum PaymentTransactionStatus { Received = 0, Verified = 1, Rejected = 2 }

/// <summary>Giao dịch tiền vào. Received chưa phải bằng chứng thanh toán/ghi có cho giảng viên.</summary>
public class PaymentTransaction : BaseEntity
{
    public int CheckoutOrderId { get; set; }
    public CheckoutOrder Order { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Received;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
