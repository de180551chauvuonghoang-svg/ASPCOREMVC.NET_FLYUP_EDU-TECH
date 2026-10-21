using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IAdminDashboardService
{
    Task<AdminDashboardStats> GetStatsAsync();
}

public sealed class AdminDashboardService(IAdminDashboardRepository repository) : IAdminDashboardService
{
    public Task<AdminDashboardStats> GetStatsAsync() => repository.GetStatsAsync();
}
