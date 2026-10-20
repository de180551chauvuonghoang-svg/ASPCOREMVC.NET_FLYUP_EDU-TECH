namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho một Khóa học trong hệ thống EduFlyUp
/// </summary>
public class Course : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;
    public CourseLevel Level { get; set; }
    public string InstructorId { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = false;

    // Các thuộc tính mở rộng cho Phân hệ Giảng viên
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public string Category { get; set; } = "Công nghệ thông tin";
    public string? RejectionReason { get; set; }

    // Navigation Properties
    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();
}
