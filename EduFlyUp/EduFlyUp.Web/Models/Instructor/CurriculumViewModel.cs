using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Instructor;

public class CurriculumViewModel
{
    public Course Course { get; set; } = null!;
    public IEnumerable<Lesson> Lessons { get; set; } = Enumerable.Empty<Lesson>();
    public IDictionary<string, List<Lesson>> Sections { get; set; } = new Dictionary<string, List<Lesson>>();

    public int TotalDurationMinutes => Lessons.Sum(l => l.DurationMinutes);
    public int TotalLessons => Lessons.Count();
    public int TotalSections => Sections.Count;
    public int PreviewLessonsCount => Lessons.Count(l => l.IsPreview);

    public string FormattedTotalDuration
    {
        get
        {
            var total = TotalDurationMinutes;
            if (total < 60) return $"{total} phút";
            var hours = total / 60;
            var mins = total % 60;
            return mins > 0 ? $"{hours} giờ {mins} phút" : $"{hours} giờ";
        }
    }
}

public class LessonInputModel
{
    public int Id { get; set; }

    [Required]
    public int CourseId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài giảng.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên chương.")]
    [StringLength(150, ErrorMessage = "Tên chương không được vượt quá 150 ký tự.")]
    public string SectionName { get; set; } = "Chương 1: Tổng quan";

    public string? Content { get; set; }

    [Url(ErrorMessage = "Đường dẫn video không đúng định dạng URL.")]
    public string? VideoUrl { get; set; }

    [Range(0, 1440, ErrorMessage = "Thời lượng bài giảng từ 0 đến 1440 phút.")]
    public int DurationMinutes { get; set; }

    [Range(1, 9999, ErrorMessage = "Thứ tự sắp xếp phải lớn hơn hoặc bằng 1.")]
    public int Order { get; set; } = 1;

    public bool IsPreview { get; set; }

    [Url(ErrorMessage = "Đường dẫn tài liệu không hợp lệ.")]
    public string? MaterialUrl { get; set; }

    public string? AttachmentName { get; set; }
}
