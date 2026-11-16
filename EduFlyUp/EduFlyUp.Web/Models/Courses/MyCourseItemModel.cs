using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Courses;

public class MyCourseItemModel
{
    public int EnrollmentId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public int LessonCount { get; set; }
    public int? FirstLessonId { get; set; }
    public EnrollmentStatus Status { get; set; }

    // Thông tin hoàn thành & Chứng chỉ
    public bool IsCompleted { get; set; }
    public string? CertificateCode { get; set; }
    public int? CertificateId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? FinalScore { get; set; }
}
