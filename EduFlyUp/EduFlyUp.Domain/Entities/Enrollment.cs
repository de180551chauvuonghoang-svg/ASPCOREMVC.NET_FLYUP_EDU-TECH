using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Domain.Entities;

/// <summary>
/// Đại diện cho việc một Học viên (User) ghi danh tham gia một Khóa học (Course)
/// </summary>
public class Enrollment : BaseEntity
{
    public string UserId { get; set; } = string.Empty;     // Id của User (ASP.NET Identity sử dụng chuỗi Guid)
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }              // Thời điểm hoàn thành khóa học (nullable)
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    // Foreign Key & Navigation Property trỏ về Course
    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;
}
