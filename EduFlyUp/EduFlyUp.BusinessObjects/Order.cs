namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho một Đơn hàng thanh toán mua khóa học
/// </summary>
public class Order : BaseEntity
{
    public string OrderCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string PaymentMethod { get; set; } = "VietQR_SePay";
    public string? TransactionReference { get; set; }
    public DateTime? PaidAt { get; set; }

    // Navigation Properties
    public virtual Course Course { get; set; } = null!;
}
