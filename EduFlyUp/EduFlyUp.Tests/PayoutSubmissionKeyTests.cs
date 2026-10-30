using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class PayoutSubmissionKeyTests
{
    [SqlServerFact]
    public async Task Sql_unique_key_allows_legacy_nulls_but_rejects_reusing_a_submission_key()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = Guid.NewGuid();
        db.PayoutRequests.AddRange(new PayoutRequest { InstructorId = "legacy", Amount = 100 },
            new PayoutRequest { InstructorId = "legacy", Amount = 200 },
            new PayoutRequest { InstructorId = "owner", Amount = 300, SubmissionKey = key });
        await db.SaveChangesAsync();
        db.PayoutRequests.Add(new PayoutRequest { InstructorId = "other", Amount = 400, SubmissionKey = key });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(3, await db.PayoutRequests.AsNoTracking().CountAsync());
        Assert.Equal(2, await db.PayoutRequests.AsNoTracking().CountAsync(p => p.SubmissionKey == null));
    }
}
