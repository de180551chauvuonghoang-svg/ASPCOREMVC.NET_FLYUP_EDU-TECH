namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Trạng thái phê duyệt và xuất bản của Khóa học
/// </summary>
public enum CourseStatus
{
    Draft = 0,              // Bản nháp đang biên soạn
    PendingApproval = 1,    // Đã gửi yêu cầu phê duyệt tới Admin (UC-22)
    Approved = 2,           // Admin đã duyệt và cho phép xuất bản (UC-58)
    Rejected = 3,           // Admin từ chối phê duyệt kèm lý do (UC-59)
    Archived = 4            // Giảng viên tạm ẩn / lưu trữ khóa học (UC-23)
}
