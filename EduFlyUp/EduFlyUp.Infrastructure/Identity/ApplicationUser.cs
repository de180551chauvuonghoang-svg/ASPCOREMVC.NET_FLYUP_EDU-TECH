using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduFlyUp.Infrastructure.Identity
{
    /// <summary>
    /// Người dùng của hệ thống EduFlyUp.
    /// Kế thừa IdentityUser để có sẵn: Email, PasswordHash, PhoneNumber...
    ///
    /// Đặt ở Infrastructure/Identity/ vì kế thừa IdentityUser (framework concern).
    /// Domain layer chỉ biết về ICurrentUser (interface ở Domain/Interfaces/).
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        // ── Thông tin cá nhân bổ sung (ngoài những gì IdentityUser đã có) ──

        /// <summary>Họ và tên đầy đủ</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>URL ảnh đại diện</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>Tiểu sử ngắn (dùng cho Instructor)</summary>
        public string? Bio { get; set; }

        /// <summary>Thời điểm tạo tài khoản</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Tài khoản có bị khóa bởi Admin không (khác với LockoutEnabled của Identity)</summary>
        public bool IsActive { get; set; } = true;
    }
}
