namespace EduFlyUp.BusinessObjects;

/// <summary>Snapshot phục vụ hóa đơn và chia doanh thu, không tra lại giá/chủ khóa học hiện tại.</summary>
public class CheckoutOrderItem : BaseEntity
{
    public int CheckoutOrderId { get; set; }
    public CheckoutOrder Order { get; set; } = null!;
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PayableAmount { get; set; }
    public int? CouponId { get; set; }
    public string? CouponCode { get; set; }
    public int? CouponPercent { get; set; }
}
