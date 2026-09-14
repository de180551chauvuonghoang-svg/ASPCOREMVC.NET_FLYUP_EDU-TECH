namespace EduFlyUp.Domain.Entities;

/// <summary>
/// Đại diện cho một Bài học nằm trong một Khóa học
/// </summary>
public class Lesson : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int Order { get; set; }                  // Thứ tự bài học trong khóa học
    public bool IsPreview { get; set; } = false;    // Bài học có cho phép học viên học thử miễn phí không

    // Foreign Key & Navigation Property trỏ về Course
    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;
}
