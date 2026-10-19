using System.Data.Common;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using Microsoft.Extensions.Logging;

namespace EduFlyUp.Services;

public interface IAdminUserService
{
    Task<AdminUserPage> SearchAsync(AdminUserQuery query);
    Task<AdminUserDetail?> GetDetailAsync(string id);
    Task<AdminUserChangeResult> SetActiveAsync(string actorId, string id, bool active);
}

public sealed class AdminUserService(IAdminUserRepository repository, IAdminUserStatusLock statusLock,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    public Task<AdminUserPage> SearchAsync(AdminUserQuery query) => repository.SearchAsync(query with
    { Search = query.Search?.Trim(), Role = query.Role?.Trim(), Page = Math.Max(1, query.Page) });
    public Task<AdminUserDetail?> GetDetailAsync(string id) => repository.GetDetailAsync(id);

    public async Task<AdminUserChangeResult> SetActiveAsync(string actorId, string id, bool active)
    {
        AdminUserChangeResult result;
        try { result = await ChangeAsync(actorId, id, active); }
        catch (Exception ex) when (ex is DbException or AdminUserPersistenceException)
        {
            logger.LogWarning(ex, "Admin status transaction unavailable for actor {ActorId}, target {UserId}.", actorId, id);
            result = AdminUserChangeResult.Unavailable;
        }
        logger.LogInformation("Admin user status: actor {ActorId}, target {UserId}, active {IsActive}, result {Result}.", actorId, id, active, result);
        return result;
    }

    private async Task<AdminUserChangeResult> ChangeAsync(string actorId, string id, bool active)
    {
        await using var transaction = await statusLock.BeginAsync();
        repository.RefreshForStatusChange();
        var actor = await repository.GetStatusAsync(actorId);
        if (actor is not { IsActive: true, IsAdmin: true }) return AdminUserChangeResult.ActorNotAllowed;
        var target = await repository.GetStatusAsync(id);
        if (target is null) return AdminUserChangeResult.NotFound;
        if (!active && actorId == id) return AdminUserChangeResult.SelfDeactivation;
        if (target.IsActive == active) return AdminUserChangeResult.Unchanged;
        if (!active && target.IsAdmin && await repository.CountActiveAdminsAsync() <= 1)
            return AdminUserChangeResult.LastActiveAdmin;
        if (!await repository.SaveActiveStateAsync(id, active)) return AdminUserChangeResult.Conflict;
        await transaction.CommitAsync();
        return AdminUserChangeResult.Changed;
    }
}
