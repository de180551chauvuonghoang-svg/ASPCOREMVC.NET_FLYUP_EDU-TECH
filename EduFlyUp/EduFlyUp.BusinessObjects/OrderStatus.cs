namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Trạng thái của đơn hàng mua khóa học
/// </summary>
public enum OrderStatus
{
    Pending = 0,   // Đang chờ thanh toán
    Paid = 1,      // Đã thanh toán thành công
    Cancelled = 2, // Đã hủy đơn
    Expired = 3    // Đơn hàng hết hạn thanh toán
}
