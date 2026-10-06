namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho việc một Học viên ghi danh tham gia một Khóa học
/// </summary>
public class Enrollment : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    // Foreign Key & Navigation
    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;
}
