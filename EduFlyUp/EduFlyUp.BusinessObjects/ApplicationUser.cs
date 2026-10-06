using Microsoft.AspNetCore.Identity;

namespace EduFlyUp.BusinessObjects;

/// <summary>
/// Người dùng của hệ thống EduFlyUp.
/// Kế thừa IdentityUser (có sẵn: Email, PasswordHash, PhoneNumber, UserName...)
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Họ và tên đầy đủ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>URL ảnh đại diện</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Tiểu sử ngắn</summary>
    public string? Bio { get; set; }

    /// <summary>Thời điểm tạo tài khoản</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Trạng thái hoạt động</summary>
    public bool IsActive { get; set; } = true;
}
