using System.Data.Common;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class CheckoutPaymentSqlServerTests
{
    private static readonly CheckoutPaymentReceiver Receiver = new("970415", "000000123456", "DEMO ACCOUNT");

    [SqlServerFact]
    public async Task Preparation_snapshots_discounted_amount_receiver_and_reference_without_confirming_payment()
    {
        await using var r = await Rig.CreateAsync();
        await r.Discount(); var input = await r.Input();
        var result = await r.Prepare(input);
        Assert.Equal(CheckoutPaymentChange.Prepared, result.Change); Assert.Equal(180000, result.Instructions!.Amount);
        Assert.Matches("^FU[A-Z0-9]{20}$", result.Instructions.Reference);
        Assert.Equal("000000123456", result.Instructions.AccountNumber);
        Assert.Equal(r.Now.AddMinutes(30), result.Instructions.ExpiresAtUtc);
        var replay = await CheckoutPaymentDAO.Instance.PrepareAsync(r.Db, r.Buyer, input,
            new("970436", "999999999999", "OTHER DEMO ACCOUNT"), r.Now);
        Assert.Equal(CheckoutPaymentChange.AlreadyPrepared, replay.Change); Assert.Equal(result.Instructions, replay.Instructions);
        Assert.Single(await r.Db.CheckoutPaymentIntents.ToListAsync());
        Assert.Empty(await r.Db.PaymentTransactions.ToListAsync()); Assert.Empty(await r.Db.Enrollments.ToListAsync());
        Assert.Equal(CheckoutOrderStatus.Pending, (await r.Db.CheckoutOrders.AsNoTracking().SingleAsync()).Status);
        Assert.False((await new CheckoutQueryRepository(r.Db).DetailAsync(r.Buyer, r.Order.PublicId, r.Now))!.CanEditCoupon);
        var current = await r.Input();
        Assert.Equal(CheckoutCouponChange.NotEditable, (await CheckoutCouponDAO.Instance.ApplyAsync(r.Db, r.Buyer,
            new(r.Order.PublicId, "", current.Version), r.Now)).Change);
        Assert.Equal(0, (await r.Db.Coupons.AsNoTracking().SingleAsync()).UsedCount);
    }

    [SqlServerFact]
    public async Task Concurrent_preparations_create_one_intent_and_replay_the_same_reference()
    {
        await using var r = await Rig.CreateAsync(); var input = await r.Input();
        using var a = r.Data.Services.CreateScope(); using var b = r.Data.Services.CreateScope();
        var results = await Task.WhenAll(CheckoutPaymentDAO.Instance.PrepareAsync(a.ServiceProvider.GetRequiredService<AppDbContext>(), r.Buyer, input, Receiver, r.Now),
            CheckoutPaymentDAO.Instance.PrepareAsync(b.ServiceProvider.GetRequiredService<AppDbContext>(), r.Buyer, input, Receiver, r.Now));
        Assert.Contains(results, x => x.Change == CheckoutPaymentChange.Prepared);
        Assert.Contains(results, x => x.Change == CheckoutPaymentChange.AlreadyPrepared);
        Assert.Equal(results[0].Instructions, results[1].Instructions);
        Assert.Single(await r.Db.CheckoutPaymentIntents.ToListAsync());
    }

    [SqlServerFact]
    public async Task Preparing_payment_and_applying_coupon_cannot_silently_change_the_frozen_amount()
    {
        await using var r = await Rig.CreateAsync(); var input = await r.Input();
        using var a = r.Data.Services.CreateScope(); using var b = r.Data.Services.CreateScope();
        var prepare = CheckoutPaymentDAO.Instance.PrepareAsync(a.ServiceProvider.GetRequiredService<AppDbContext>(), r.Buyer, input, Receiver, r.Now);
        var coupon = CheckoutCouponDAO.Instance.ApplyAsync(b.ServiceProvider.GetRequiredService<AppDbContext>(), r.Buyer,
            new(r.Order.PublicId, "DEMO10", input.Version), r.Now);
        await Task.WhenAll(prepare, coupon);
        if (prepare.Result.Change == CheckoutPaymentChange.Prepared)
        {
            Assert.Equal(200000, prepare.Result.Instructions!.Amount);
            Assert.Equal(CheckoutCouponChange.Stale, coupon.Result.Change);
        }
        else
        {
            Assert.Equal(CheckoutPaymentChange.Stale, prepare.Result.Change);
            Assert.Equal(CheckoutCouponChange.Applied, coupon.Result.Change);
            Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
        }
    }

    [SqlServerFact]
    public async Task Preparation_checks_owner_current_account_role_and_stale_version()
    {
        await using var r = await Rig.CreateAsync(); var input = await r.Input();
        var other = await r.Data.CreateUserAsync("other@example.test", "Student");
        Assert.Equal(CheckoutPaymentChange.NotFound, (await CheckoutPaymentDAO.Instance.PrepareAsync(r.Db, other, input, Receiver, r.Now)).Change);
        await r.Discount();
        Assert.Equal(CheckoutPaymentChange.Stale, (await r.Prepare(input)).Change);
        await r.Db.Users.Where(u => u.Id == r.Buyer).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(CheckoutPaymentChange.Forbidden, (await r.Prepare(await r.Input())).Change);
        await r.Db.Users.Where(u => u.Id == r.Buyer).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true));
        await r.Db.UserRoles.Where(ur => ur.UserId == r.Buyer).ExecuteDeleteAsync();
        Assert.Equal(CheckoutPaymentChange.Forbidden, (await r.Prepare(await r.Input())).Change);
        Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
    }

    [SqlServerFact]
    public async Task Expired_free_inconsistent_and_transaction_started_orders_cannot_be_prepared()
    {
        await using var r = await Rig.CreateAsync();
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, r.Now));
        Assert.Equal(CheckoutPaymentChange.NotPayable, (await r.Prepare(await r.Input())).Change);
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, r.Now.AddMinutes(30)));
        await r.Db.CheckoutOrderItems.ExecuteUpdateAsync(s => s.SetProperty(i => i.OriginalAmount, 300000m).SetProperty(i => i.PayableAmount, 300000m));
        Assert.Equal(CheckoutPaymentChange.NotPayable, (await r.Prepare(await r.Input())).Change);
        await r.Db.CheckoutOrderItems.ExecuteUpdateAsync(s => s.SetProperty(i => i.OriginalAmount, 200000m).SetProperty(i => i.PayableAmount, 200000m));
        var orderId = await r.Db.CheckoutOrders.Select(o => o.Id).SingleAsync();
        r.Db.PaymentTransactions.Add(new() { CheckoutOrderId = orderId, Amount = 200000, Provider = "fixture", ProviderTransactionId = "received-1", OccurredAtUtc = r.Now });
        await r.Db.SaveChangesAsync();
        Assert.Equal(CheckoutPaymentChange.NotPayable, (await r.Prepare(await r.Input())).Change);
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, CheckoutOrderStatus.Paid)
            .SetProperty(o => o.PaidAtUtc, r.Now).SetProperty(o => o.OriginalAmount, 0m).SetProperty(o => o.PayableAmount, 0m));
        Assert.Equal(CheckoutPaymentChange.NotPayable, (await r.Prepare(await r.Input())).Change);
        Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
    }

    [SqlServerFact]
    public async Task Failed_order_update_rolls_back_inserted_intent_and_keeps_coupon_editable()
    {
        await using var r = await Rig.CreateAsync(); var input = await r.Input();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(r.Db.Database.GetConnectionString())
            .AddInterceptors(new FailOrderUpdate()).Options;
        await using (var failing = new AppDbContext(options))
            await Assert.ThrowsAsync<SimulatedFailure>(() => CheckoutPaymentDAO.Instance.PrepareAsync(failing, r.Buyer, input, Receiver, r.Now));
        Assert.Empty(await r.Db.CheckoutPaymentIntents.ToListAsync());
        Assert.True((await new CheckoutQueryRepository(r.Db).DetailAsync(r.Buyer, r.Order.PublicId, r.Now))!.CanEditCoupon);
    }

    [SqlServerFact]
    public async Task Migration_preserves_existing_orders_and_unique_intent_per_order_keeps_audit()
    {
        await using var r = await Rig.CreateAsync(useMigrations: true);
        var migrator = r.Db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261105163935_AddCheckoutOrdersAndPaymentTransactions");
        Assert.Equal(200000, (await r.Db.CheckoutOrders.AsNoTracking().SingleAsync()).PayableAmount);
        await migrator.MigrateAsync();
        var result = await r.Prepare(await r.Input()); Assert.Equal(CheckoutPaymentChange.Prepared, result.Change);
        var row = await r.Db.CheckoutPaymentIntents.AsNoTracking().SingleAsync();
        r.Db.CheckoutPaymentIntents.Add(new() { CheckoutOrderId = row.CheckoutOrderId, Reference = "FU" + new string('B', 20),
            BankBin = row.BankBin, AccountNumber = row.AccountNumber, AccountName = row.AccountName, Amount = row.Amount, ExpiresAtUtc = row.ExpiresAtUtc });
        await Assert.ThrowsAsync<DbUpdateException>(() => r.Db.SaveChangesAsync()); r.Db.ChangeTracker.Clear();
        var otherBuyer = await r.Data.CreateUserAsync("second@example.test", "Student");
        var courseId = await r.Db.CheckoutOrderItems.Select(i => i.CourseId).FirstAsync();
        var otherOrder = await CheckoutDAO.Instance.CreateAsync(r.Db, otherBuyer, new(Guid.NewGuid(), [courseId]), r.Now);
        var otherOrderId = await r.Db.CheckoutOrders.Where(o => o.PublicId == otherOrder.Order!.PublicId).Select(o => o.Id).SingleAsync();
        r.Db.CheckoutPaymentIntents.Add(new() { CheckoutOrderId = otherOrderId, Reference = row.Reference,
            BankBin = row.BankBin, AccountNumber = row.AccountNumber, AccountName = row.AccountName, Amount = row.Amount, ExpiresAtUtc = row.ExpiresAtUtc });
        await Assert.ThrowsAsync<DbUpdateException>(() => r.Db.SaveChangesAsync()); r.Db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<DbException>(() => r.Db.CheckoutOrders.ExecuteDeleteAsync());
        Assert.Single(await r.Db.CheckoutPaymentIntents.ToListAsync());
    }

    private sealed class SimulatedFailure : DbException;
    private sealed class FailOrderUpdate : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("CheckoutOrders")) throw new SimulatedFailure();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
    private sealed class Rig(AdminUserSqlServerTests.SqlDatabase data, IServiceScope scope) : IAsyncDisposable
    {
        public AdminUserSqlServerTests.SqlDatabase Data => data;
        public AppDbContext Db => scope.ServiceProvider.GetRequiredService<AppDbContext>();
        public string Buyer = ""; public CheckoutCreated Order = null!; public DateTime Now = DateTime.UtcNow;
        public static async Task<Rig> CreateAsync(bool useMigrations = false)
        {
            var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync(useMigrations);
            var r = new Rig(data, data.Services.CreateScope());
            r.Buyer = await data.CreateUserAsync("buyer@example.test", "Student");
            var issuer = await data.CreateUserAsync("issuer@example.test", "Instructor");
            var course = new Course { Title = "Payment fixture", InstructorId = issuer, Price = 200000, IsPublished = true, Status = CourseStatus.Approved };
            r.Db.Add(course); r.Db.Coupons.Add(new() { Code = "DEMO10", InstructorId = issuer, DiscountPercent = 10,
                StartDate = r.Now.AddDays(-1), EndDate = r.Now.AddDays(1) }); await r.Db.SaveChangesAsync();
            r.Order = (await CheckoutDAO.Instance.CreateAsync(r.Db, r.Buyer, new(Guid.NewGuid(), [course.Id]), r.Now)).Order!;
            r.Db.ChangeTracker.Clear(); return r;
        }
        public async Task<CheckoutPaymentInput> Input() => new(Order.PublicId,
            Convert.ToBase64String(await Db.CheckoutOrders.Where(o => o.PublicId == Order.PublicId).Select(o => o.RowVersion).SingleAsync()));
        public async Task Discount() { var input = await Input(); Assert.Equal(CheckoutCouponChange.Applied,
            (await CheckoutCouponDAO.Instance.ApplyAsync(Db, Buyer, new(Order.PublicId, "DEMO10", input.Version), Now)).Change); }
        public Task<CheckoutPaymentResult> Prepare(CheckoutPaymentInput input) => CheckoutPaymentDAO.Instance.PrepareAsync(Db, Buyer, input, Receiver, Now);
        public async ValueTask DisposeAsync() { scope.Dispose(); await data.DisposeAsync(); }
    }
}
