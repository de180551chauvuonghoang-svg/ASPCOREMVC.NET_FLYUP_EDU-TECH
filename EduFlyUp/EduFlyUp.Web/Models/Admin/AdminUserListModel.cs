using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Admin;

public sealed class AdminUserFilter
{
    [StringLength(256)] public string? Search { get; set; }
    [StringLength(256)] public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public AdminUserQuery ToQuery() => new(Search?.Trim(), Role?.Trim(), IsActive, Page);
}
public sealed record AdminUserListModel(AdminUserQuery Filter, AdminUserPage Result);
