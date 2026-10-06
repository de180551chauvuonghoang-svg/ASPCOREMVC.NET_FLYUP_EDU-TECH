namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Đại diện cho một Bài học trong Khóa học
/// </summary>
public class Lesson : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int Order { get; set; }
    public bool IsPreview { get; set; } = false;

    // Foreign Key & Navigation
    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;
}
