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

    // Các thuộc tính mở rộng cho cấu trúc chương và học liệu đính kèm (UC-21, UC-24)
    public string SectionName { get; set; } = "Chương 1: Tổng quan";
    public string MaterialUrl { get; set; } = string.Empty;
    public string AttachmentName { get; set; } = string.Empty;

    // Foreign Key & Navigation
    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;
}
