using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Web.Models.Courses
{
    public class CourseItemModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsFree => Price == 0;
        public CourseLevel Level { get; set; }
        public int LessonCount { get; set; }
    }
}
