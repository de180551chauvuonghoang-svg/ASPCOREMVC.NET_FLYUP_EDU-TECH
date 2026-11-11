namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Trạng thái phê duyệt và xuất bản của Khóa học
/// </summary>
public enum CourseStatus
{
    Draft = 0,              // Bản nháp đang biên soạn
    PendingApproval = 1,    // Đã gửi yêu cầu phê duyệt tới Admin
    Approved = 2,           // Admin đã duyệt và cho phép xuất bản
    Rejected = 3,           // Admin từ chối phê duyệt kèm lý do
    Archived = 4            // Giảng viên tạm ẩn / lưu trữ khóa học
}

public static class CourseStatusExtensions
{
    /// <summary>
    /// Chuẩn hóa trạng thái hiển thị hiệu lực của khóa học dựa trên cờ xuất bản và quy trình duyệt.
    /// </summary>
    public static CourseStatus GetEffectiveStatus(this Course course)
        => GetEffectiveStatus(course.Status, course.IsPublished);

    public static CourseStatus GetEffectiveStatus(CourseStatus status, bool isPublished)
    {
        if (isPublished) return CourseStatus.Approved;
        return status == CourseStatus.Approved ? CourseStatus.Rejected : status;
    }
}
