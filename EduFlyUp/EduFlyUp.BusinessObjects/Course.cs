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

    // Navigation Properties
    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
