namespace EduFlyUp.BusinessObjects;

public enum CheckoutOrderStatus { Pending = 0, Paid = 1, Cancelled = 2, Expired = 3 }

/// <summary>Đơn mua khóa học; các số tiền được chốt ở server khi tạo checkout.</summary>
public class CheckoutOrder : BaseEntity
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public Guid SubmissionKey { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PayableAmount { get; set; }
    public CheckoutOrderStatus Status { get; set; } = CheckoutOrderStatus.Pending;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<CheckoutOrderItem> Items { get; set; } = new List<CheckoutOrderItem>();
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
