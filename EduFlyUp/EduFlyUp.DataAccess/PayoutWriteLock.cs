using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

internal static class PayoutWriteLock
{
    internal static Task AcquireAsync(AppDbContext db)
    {
        if (db.Database.IsNpgsql())
            return db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6531162726022911)");
        return db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=N'EduFlyUp.PayoutManagement',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51002, N'Không thể lấy khóa quản lý yêu cầu rút tiền.', 1;
            """);
    }
}
