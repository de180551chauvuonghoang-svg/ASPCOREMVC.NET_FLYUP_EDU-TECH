using EduFlyUp.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EduFlyUp.Infrastructure.Services;

/// <summary>
/// Implement ICurrentUser bằng cách đọc thông tin từ HttpContext.
/// 
/// Luồng hoạt động:
///   1. ASP.NET Core nhận HTTP Request
///   2. Authentication Middleware giải mã cookie/JWT → tạo ClaimsPrincipal
///   3. ClaimsPrincipal được lưu vào HttpContext.User
///   4. CurrentUserService đọc Claims từ HttpContext.User
/// 
/// Lý do dùng IHttpContextAccessor thay vì HttpContext trực tiếp:
///   - HttpContext chỉ tồn tại trong Web layer
///   - IHttpContextAccessor cho phép inject vào bất kỳ class nào trong DI
///   - Infrastructure có thể implement ICurrentUser mà không vi phạm kiến trúc
/// </summary>
public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Lấy ClaimsPrincipal từ HttpContext.User
    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// Lấy UserId từ Claim "NameIdentifier" (chuẩn của ASP.NET Identity).
    /// Trả về null nếu chưa đăng nhập.
    /// </summary>
    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Lấy UserName từ Claim "Name".
    /// </summary>
    public string? UserName =>
        User?.FindFirstValue(ClaimTypes.Name);

    /// <summary>
    /// Kiểm tra user đã authenticate (đăng nhập) chưa.
    /// </summary>
    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Kiểm tra user có thuộc role chỉ định không.
    /// VD: IsInRole("Admin") — dùng trong Authorization logic
    /// </summary>
    public bool IsInRole(string role) =>
        User?.IsInRole(role) ?? false;
}
