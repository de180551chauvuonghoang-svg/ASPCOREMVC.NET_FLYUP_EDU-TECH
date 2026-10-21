namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Trạng thái yêu cầu rút tiền Payout của Giảng viên (UC-38)
/// </summary>
public enum PayoutStatus
{
    Pending = 0,    // Đang chờ Admin xử lý
    Approved = 1,   // Đã thanh toán / chuyển khoản thành công
    Rejected = 2    // Bị từ chối kèm lý do phản hồi
}
