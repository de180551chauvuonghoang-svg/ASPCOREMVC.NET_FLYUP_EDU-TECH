using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Web.Models.Courses;

public class CourseDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;
    public CourseLevel Level { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LessonItemViewModel> Lessons { get; set; } = new();
}

public class LessonItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int Order { get; set; }
    public bool IsPreview { get; set; }
}
