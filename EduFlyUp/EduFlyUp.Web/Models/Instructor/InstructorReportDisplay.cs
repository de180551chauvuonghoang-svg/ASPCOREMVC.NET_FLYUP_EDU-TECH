using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Instructor;

public static class InstructorReportDisplay
{
    public static string VietnamTime(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy HH:mm");
    public static string Status(EnrollmentStatus status) => status switch
    {
        EnrollmentStatus.Active => "Đang học", EnrollmentStatus.Completed => "Đã hoàn thành",
        EnrollmentStatus.Cancelled => "Đã hủy", _ => "Không xác định"
    };
}
