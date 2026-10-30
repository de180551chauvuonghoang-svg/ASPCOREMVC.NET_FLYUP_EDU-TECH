using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Instructor;

public static class InstructorPayoutDisplay
{
    public static string Status(PayoutStatus status) => status switch
    {
        PayoutStatus.Pending => "Chờ xử lý", PayoutStatus.Approved => "Đã duyệt",
        PayoutStatus.Rejected => "Bị từ chối", _ => "Không xác định"
    };
}
