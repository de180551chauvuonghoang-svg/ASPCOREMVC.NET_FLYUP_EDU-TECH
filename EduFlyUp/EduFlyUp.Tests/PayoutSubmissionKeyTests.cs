using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EduFlyUp.Tests;

public sealed class PayoutSubmissionKeyTests
{
    [SqlServerFact]
    public async Task Upgrading_an_existing_database_preserves_legacy_requests_without_assigning_submission_keys()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync(useMigrations: true);
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Only this fixture's newly created, disposable GUID database is downgraded/upgraded.
        await db.GetService<IMigrator>().MigrateAsync("20261020074004_AddInstructorModuleTables");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO PayoutRequests (InstructorId, Amount, BankName, AccountNumber, AccountHolderName, Status, CreatedAt)
            VALUES (N'legacy-owner', 123456.78, N'Demo', N'001234', N'Demo Holder', 1, '2026-10-31');
            """);
        await db.Database.MigrateAsync();
        var request = await db.PayoutRequests.AsNoTracking().SingleAsync();
        Assert.Equal("legacy-owner", request.InstructorId); Assert.Equal(123456.78m, request.Amount);
        Assert.Equal(PayoutStatus.Approved, request.Status); Assert.Equal("001234", request.AccountNumber);
        Assert.Null(request.SubmissionKey);
    }

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
