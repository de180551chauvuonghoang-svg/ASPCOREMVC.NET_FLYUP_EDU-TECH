namespace EduFlyUp.Domain.Interfaces;

/// <summary>
/// Interface trừu tượng hóa "người dùng hiện tại" đang thao tác trên hệ thống.
/// 
/// Mục đích:
///   - Application layer cần biết UserId để thực thi business logic
///     (VD: "chỉ Instructor mới được sửa Course của mình")
///   - Nhưng Application KHÔNG được phụ thuộc vào Infrastructure/HttpContext
/// 
/// Giải pháp:
///   - Domain định nghĩa interface này (hợp đồng)
///   - Infrastructure implement qua CurrentUserService (dùng IHttpContextAccessor)
///   - Tuân thủ Dependency Inversion Principle: cả hai phụ thuộc vào abstraction
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Id của user đang đăng nhập (null nếu chưa đăng nhập / anonymous).
    /// Dạng string vì ASP.NET Identity dùng GUID string.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Tên đăng nhập (username) của user hiện tại.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Kiểm tra user đã đăng nhập (authenticated) chưa.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Kiểm tra user có thuộc role chỉ định không.
    /// VD: IsInRole("Admin"), IsInRole("Teacher")
    /// </summary>
    bool IsInRole(string role);
}
