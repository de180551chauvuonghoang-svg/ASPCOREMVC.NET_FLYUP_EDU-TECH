using System.Data;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduFlyUp.Repositories;

public interface IAdminUserStatusTransaction : IAsyncDisposable
{
    Task CommitAsync();
}

public interface IAdminUserStatusLock
{
    Task<IAdminUserStatusTransaction> BeginAsync();
}

// Database-owned lock works across app instances. All Admin status mutations
// must acquire it before reading actor/target state or counting active Admins.
public sealed class SqlServerAdminUserStatusLock(AppDbContext context) : IAdminUserStatusLock
{
    public async Task<IAdminUserStatusTransaction> BeginAsync()
    {
        if (!context.Database.IsSqlServer())
            throw new InvalidOperationException("Admin status changes require the SQL Server transaction guard.");
        var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await context.Database.ExecuteSqlRawAsync("""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock
                    @Resource = N'EduFlyUp.AdminUserStatus',
                    @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000;
                IF @lockResult < 0 THROW 51000, 'Unable to acquire Admin user status lock.', 1;
                """);
            return new SqlTransaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class SqlTransaction(IDbContextTransaction transaction) : IAdminUserStatusTransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
