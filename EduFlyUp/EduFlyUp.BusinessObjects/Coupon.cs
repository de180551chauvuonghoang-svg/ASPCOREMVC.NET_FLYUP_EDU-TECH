namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho Mã giảm giá / Khuyến mãi khóa học (UC-40)
/// </summary>
public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public int DiscountPercent { get; set; } // Tối đa 90% theo quy tắc BR-40.1
    public int MaxUsage { get; set; } = 100;
    public int UsedCount { get; set; } = 0;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool IsActive { get; set; } = true;
    public string InstructorId { get; set; } = string.Empty;

    // Khóa ngoại liên kết tới khóa học cụ thể (nếu null là áp dụng toàn bộ khóa của giảng viên)
    public int? CourseId { get; set; }
    public virtual Course? Course { get; set; }
}
