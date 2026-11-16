using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Account;

public class ProfileViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Role { get; set; } = "Student";

    // Statistics
    public int TotalCoursesCount { get; set; }
    public int TotalCertificatesCount { get; set; }
    public int TotalOrdersCount { get; set; }
    public decimal TotalSpent { get; set; }

    // Sub-models for forms
    public EditProfileInputModel EditProfile { get; set; } = new();
    public ChangePasswordInputModel ChangePassword { get; set; } = new();

    // Data lists
    public List<Order> Orders { get; set; } = new();
    public List<Certificate> Certificates { get; set; } = new();
    public List<Enrollment> Enrollments { get; set; } = new();

    public string ActiveTab { get; set; } = "info";
}

public class EditProfileInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [StringLength(500, ErrorMessage = "Tiểu sử không quá 500 ký tự")]
    [Display(Name = "Giới thiệu bản thân")]
    public string? Bio { get; set; }
}

public class ChangePasswordInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải từ 6 ký tự trở lên")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu mới")]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
