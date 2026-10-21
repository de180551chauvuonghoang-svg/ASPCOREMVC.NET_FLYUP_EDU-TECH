using System.Data.Common;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduFlyUp.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLYUP_TEST_SQLSERVER")))
            Skip = "Set FLYUP_TEST_SQLSERVER to a development SQL Server connection with create/drop database rights.";
    }
}

public sealed class AdminUserSqlServerTests
{
    [SqlServerFact]
    public async Task Concurrent_admins_cannot_deactivate_each_other_and_remove_the_last_active_admin()
    {
        await using var database = await SqlDatabase.CreateAsync();
        var a = await database.CreateUserAsync("a@example.test");
        var b = await database.CreateUserAsync("b@example.test");
        using var first = database.Services.CreateScope(); using var second = database.Services.CreateScope();
        // Cookie authentication can preload actors before either request gets
        // the transaction lock. The status service must discard those snapshots.
        await first.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(a);
        await second.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(b);
        var results = await Task.WhenAll(first.ServiceProvider.GetRequiredService<IAdminUserService>().SetActiveAsync(a, b, false),
            second.ServiceProvider.GetRequiredService<IAdminUserService>().SetActiveAsync(b, a, false));
        Assert.Contains(AdminUserChangeResult.Changed, results);
        Assert.Contains(AdminUserChangeResult.ActorNotAllowed, results);
        using var scope = database.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAdminUserRepository>();
        Assert.Equal(1, await repository.CountActiveAdminsAsync());
        var activeId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.IsActive).Select(u => u.Id).SingleAsync();
        Assert.Equal(AdminUserChangeResult.SelfDeactivation,
            await scope.ServiceProvider.GetRequiredService<IAdminUserService>().SetActiveAsync(activeId, activeId, false));
        Assert.Equal(1, await repository.CountActiveAdminsAsync());
    }

    [SqlServerFact]
    public async Task Failed_commit_rolls_back_both_account_state_and_security_stamp()
    {
        await using var database = await SqlDatabase.CreateAsync();
        var actor = await database.CreateUserAsync("admin@example.test");
        var target = await database.CreateUserAsync("student@example.test", "Student");
        string? stamp;
        using (var before = database.Services.CreateScope())
            stamp = (await before.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(target))!.SecurityStamp;
        using (var scope = database.Services.CreateScope())
        {
            var service = new AdminUserService(scope.ServiceProvider.GetRequiredService<IAdminUserRepository>(),
                new FailingCommitLock(scope.ServiceProvider.GetRequiredService<IAdminUserStatusLock>()), NullLogger<AdminUserService>.Instance);
            Assert.Equal(AdminUserChangeResult.Unavailable, await service.SetActiveAsync(actor, target, false));
        }
        using var after = database.Services.CreateScope();
        var user = await after.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(target);
        Assert.NotNull(user); Assert.True(user.IsActive); Assert.Equal(stamp, user.SecurityStamp);
        Assert.Equal(new[] { "Student" }, await after.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(user));
    }

    private sealed class FailingCommitLock(IAdminUserStatusLock inner) : IAdminUserStatusLock
    {
        public async Task<IAdminUserStatusTransaction> BeginAsync() => new FailedTransaction(await inner.BeginAsync());
        private sealed class FailedTransaction(IAdminUserStatusTransaction transaction) : IAdminUserStatusTransaction
        {
            public Task CommitAsync() => throw new SimulatedDatabaseException();
            public ValueTask DisposeAsync() => transaction.DisposeAsync();
        }
        private sealed class SimulatedDatabaseException : DbException;
    }

    internal sealed class SqlDatabase(ServiceProvider services, string connection) : IAsyncDisposable
    {
        public ServiceProvider Services => services;
        public static async Task<SqlDatabase> CreateAsync()
        {
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLYUP_TEST_SQLSERVER"))
            {
                // Never migrate, clear or delete the database named in the input.
                InitialCatalog = "FlyUpAdminStatusTest_" + Guid.NewGuid().ToString("N"),
                AttachDBFilename = string.Empty
            }.ConnectionString;
            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddDbContext<AppDbContext>(o => o.UseSqlServer(connection));
            collection.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
            collection.AddScoped<IAdminUserRepository, AdminUserRepository>();
            collection.AddScoped<IAdminUserStatusLock, SqlServerAdminUserStatusLock>();
            collection.AddScoped<IAdminUserService, AdminUserService>();
            collection.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
            collection.AddScoped<IAdminDashboardService, AdminDashboardService>();
            var provider = collection.BuildServiceProvider();
            try
            {
                using var scope = provider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
                return new SqlDatabase(provider, connection);
            }
            catch { await provider.DisposeAsync(); throw; }
        }

        public async Task<string> CreateUserAsync(string email, string role = "Admin")
        {
            using var scope = services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(user, "SqlTestOnly@12345")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            return user.Id;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                // Only the fresh GUID database owned by this fixture is removed.
                var name = new SqlConnectionStringBuilder(connection).InitialCatalog;
                const string prefix = "FlyUpAdminStatusTest_";
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
                    throw new InvalidOperationException("Unexpected test database name; refusing cleanup.");
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            }
            finally { await services.DisposeAsync(); }
        }
    }
}
