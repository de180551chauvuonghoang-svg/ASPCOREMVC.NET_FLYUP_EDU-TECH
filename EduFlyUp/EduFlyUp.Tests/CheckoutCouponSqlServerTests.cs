using System.Data.Common;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class CheckoutCouponSqlServerTests
{
    [SqlServerFact]
    public async Task Discount_uses_order_snapshots_and_only_issuer_courses_with_legacy_username_support()
    {
        await using var r = await Rig.CreateAsync();
        var order = await r.Order(r.Buyer, r.A, r.B, r.Other);
        var course = await r.Db.Courses.SingleAsync(c => c.Id == r.A);
        course.Price = 999000; course.InstructorId = "changed-owner"; await r.Db.SaveChangesAsync();
        var result = await r.Apply(r.Buyer, order, "  demo10 ");
        Assert.Equal(CheckoutCouponChange.Applied, result.Change);
        Assert.Equal(651, result.Order!.OriginalAmount); Assert.Equal(35, result.Order.DiscountAmount);
        Assert.Equal(616, result.Order.PayableAmount);
        var items = await r.Db.CheckoutOrderItems.AsNoTracking().OrderBy(i => i.CourseId).ToListAsync();
        Assert.Equal(10, items.Single(i => i.CourseId == r.A).DiscountAmount);
        Assert.Equal(25, items.Single(i => i.CourseId == r.B).DiscountAmount);
        Assert.Null(items.Single(i => i.CourseId == r.Other).CouponId);
        Assert.Equal(0, (await r.Db.Coupons.AsNoTracking().SingleAsync()).UsedCount);
        Assert.Empty(await r.Db.PaymentTransactions.ToListAsync()); Assert.Empty(await r.Db.Enrollments.ToListAsync());
    }

    [SqlServerFact]
    public async Task Course_specific_coupon_and_rounding_never_reduce_other_lines_or_create_zero_payment()
    {
        await using var r = await Rig.CreateAsync();
        await r.Db.Coupons.ExecuteUpdateAsync(s => s.SetProperty(c => c.CourseId, r.A));
        var order = await r.Order(r.Buyer, r.A, r.B);
        var result = await r.Apply(r.Buyer, order);
        Assert.Equal(10, result.Order!.DiscountAmount); Assert.Equal(341, result.Order.PayableAmount);
        Assert.Null((await r.Db.CheckoutOrderItems.AsNoTracking().SingleAsync(i => i.CourseId == r.B)).CouponId);
        await r.Db.Courses.Where(c => c.Id == r.A).ExecuteUpdateAsync(s => s.SetProperty(c => c.Price, 1m));
        var tiny = await r.Order(r.Buyer2, r.A);
        Assert.Equal(CheckoutCouponChange.InvalidCoupon, (await r.Apply(r.Buyer2, tiny)).Change);
    }

    [SqlServerFact]
    public async Task Invalid_expired_future_disabled_ambiguous_and_foreign_coupons_leave_order_unchanged()
    {
        await using var r = await Rig.CreateAsync(); var order = await r.Order(r.Buyer, r.A);
        var baseline = await r.Input(order, "DEMO10");
        Action<Coupon>[] invalid =
        [
            c => c.IsActive = false, c => c.EndDate = r.Now,
            c => c.StartDate = r.Now.AddMinutes(1), c => c.DiscountPercent = 91,
            c => c.UsedCount = -1, c => c.InstructorId = "other-instructor",
            c => c.CourseId = r.B
        ];
        foreach (var mutate in invalid)
        {
            var coupon = await r.Db.Coupons.SingleAsync();
            coupon.IsActive = true; coupon.StartDate = r.Now.AddDays(-1); coupon.EndDate = r.Now.AddDays(1);
            coupon.DiscountPercent = 10; coupon.UsedCount = 0; coupon.InstructorId = r.Owner; coupon.CourseId = null;
            mutate(coupon); await r.Db.SaveChangesAsync();
            Assert.Equal(CheckoutCouponChange.InvalidCoupon, (await CheckoutCouponDAO.Instance.ApplyAsync(r.Db, r.Buyer, baseline, r.Now)).Change);
        }
        r.Db.Coupons.Add(new() { Code = "DEMO10", InstructorId = r.Owner, DiscountPercent = 10, StartDate = r.Now.AddDays(-1), EndDate = r.Now.AddDays(1) });
        await r.Db.SaveChangesAsync();
        Assert.Equal(CheckoutCouponChange.InvalidCoupon, (await r.Apply(r.Buyer, order)).Change);
        Assert.Equal(CheckoutCouponChange.InvalidCoupon, (await r.Apply(r.Buyer, order, "MISSING")).Change);
        Assert.Equal(0, (await r.Db.CheckoutOrders.AsNoTracking().SingleAsync()).DiscountAmount);
    }

    [SqlServerFact]
    public async Task Concurrent_orders_cannot_both_reserve_the_last_coupon_use()
    {
        await using var r = await Rig.CreateAsync();
        await r.Db.Coupons.ExecuteUpdateAsync(s => s.SetProperty(c => c.MaxUsage, 1));
        var a = await r.Order(r.Buyer, r.A, r.B); var b = await r.Order(r.Buyer2, r.A);
        var ai = await r.Input(a); var bi = await r.Input(b);
        using var sa = r.Data.Services.CreateScope(); using var sb = r.Data.Services.CreateScope();
        var results = await Task.WhenAll(CheckoutCouponDAO.Instance.ApplyAsync(sa.ServiceProvider.GetRequiredService<AppDbContext>(), r.Buyer, ai, r.Now),
            CheckoutCouponDAO.Instance.ApplyAsync(sb.ServiceProvider.GetRequiredService<AppDbContext>(), r.Buyer2, bi, r.Now));
        Assert.Contains(results, x => x.Change == CheckoutCouponChange.Applied);
        Assert.Contains(results, x => x.Change == CheckoutCouponChange.Exhausted);
        Assert.Equal(1, await CheckoutCouponReservations.CountAsync(r.Db, r.CouponId, r.Now));
        Assert.Equal(0, (await r.Db.Coupons.AsNoTracking().SingleAsync()).UsedCount);
    }

    [SqlServerFact]
    public async Task Removing_replacing_and_expiring_orders_release_reservations_without_consuming_usage()
    {
        await using var r = await Rig.CreateAsync();
        await r.Db.Coupons.ExecuteUpdateAsync(s => s.SetProperty(c => c.MaxUsage, 1));
        r.Db.Coupons.Add(new() { Code = "OTHER20", InstructorId = r.Owner, DiscountPercent = 20, MaxUsage = 1,
            StartDate = r.Now.AddDays(-1), EndDate = r.Now.AddDays(1) }); await r.Db.SaveChangesAsync();
        var a = await r.Order(r.Buyer, r.A); var b = await r.Order(r.Buyer2, r.A);
        Assert.Equal(CheckoutCouponChange.Applied, (await r.Apply(r.Buyer, a)).Change);
        Assert.Equal(CheckoutCouponChange.Exhausted, (await r.Apply(r.Buyer2, b)).Change);
        Assert.Equal(CheckoutCouponChange.Applied, (await r.Apply(r.Buyer, a, "OTHER20")).Change);
        Assert.Equal(0, await CheckoutCouponReservations.CountAsync(r.Db, r.CouponId, r.Now));
        Assert.Equal(CheckoutCouponChange.Applied, (await r.Apply(r.Buyer2, b)).Change);
        var removed = await r.Apply(r.Buyer2, b, "");
        Assert.Equal(CheckoutCouponChange.Applied, removed.Change); Assert.Null(removed.Order!.Code);
        Assert.Equal(101, removed.Order.PayableAmount); Assert.Equal(0, removed.Order.DiscountAmount);
        Assert.Equal(CheckoutCouponChange.Applied, (await r.Apply(r.Buyer, a)).Change);
        await r.Db.CheckoutOrders.Where(o => o.PublicId == a.PublicId).ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, r.Now));
        Assert.Equal(CheckoutCouponChange.Applied, (await r.Apply(r.Buyer2, b)).Change);
        Assert.All(await r.Db.Coupons.AsNoTracking().ToListAsync(), c => Assert.Equal(0, c.UsedCount));
    }

    [SqlServerFact]
    public async Task Management_cannot_change_reserved_terms_or_reduce_limit_below_used_plus_reserved()
    {
        await using var r = await Rig.CreateAsync();
        var order = await r.Order(r.Buyer, r.A); await r.Apply(r.Buyer, order);
        var coupon = await r.Db.Coupons.AsNoTracking().SingleAsync();
        var owner = new CouponOwner(r.Owner, "issuer@example.test");
        var terms = new CouponInput("DEMO10", 10, 1, coupon.StartDate, coupon.EndDate, null);
        // Chưa có lượt thanh toán nào, chỉ reservation cũng khóa các điều kiện của mã.
        Assert.Equal(CouponChange.UsedTermsLocked, await CouponDAO.Instance.SaveAsync(r.Db, owner,
            terms with { MaxUsage = 3, DiscountPercent = 20 }, coupon.Id, CouponPresentation.Version(coupon)));
        await r.Db.Coupons.ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, 1));
        Assert.Equal(CouponChange.UsageConflict, await CouponDAO.Instance.SaveAsync(r.Db, owner, terms, coupon.Id, CouponPresentation.Version(coupon)));
        Assert.Equal(CouponChange.UsedTermsLocked, await CouponDAO.Instance.SaveAsync(r.Db, owner,
            terms with { MaxUsage = 3, DiscountPercent = 20 }, coupon.Id, CouponPresentation.Version(coupon)));
        Assert.Equal(CouponChange.Saved, await CouponDAO.Instance.SetActiveAsync(r.Db, owner, coupon.Id, false, CouponPresentation.Version(coupon), r.Now));
        var replay = await r.Apply(r.Buyer, order);
        Assert.Equal(CheckoutCouponChange.Applied, replay.Change); Assert.Equal(10, replay.Order!.DiscountAmount);
        Assert.Equal(CheckoutCouponChange.Applied, (await r.Apply(r.Buyer, order, "")).Change);
    }

    [SqlServerFact]
    public async Task Stale_foreign_inactive_expired_paid_and_transaction_started_orders_cannot_be_edited()
    {
        await using var r = await Rig.CreateAsync(); var order = await r.Order(r.Buyer, r.A);
        var old = await r.Input(order);
        Assert.Equal(CheckoutCouponChange.NotFound, (await CheckoutCouponDAO.Instance.ApplyAsync(r.Db, r.Buyer2, old, r.Now)).Change);
        await CheckoutCouponDAO.Instance.ApplyAsync(r.Db, r.Buyer, old, r.Now);
        Assert.Equal(CheckoutCouponChange.Stale, (await CheckoutCouponDAO.Instance.ApplyAsync(r.Db, r.Buyer, old, r.Now)).Change);
        await r.Db.Users.Where(u => u.Id == r.Buyer).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(CheckoutCouponChange.Forbidden, (await r.Apply(r.Buyer, order, "")).Change);
        await r.Db.Users.Where(u => u.Id == r.Buyer).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true));
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, r.Now));
        Assert.Equal(CheckoutCouponChange.NotEditable, (await r.Apply(r.Buyer, order, "")).Change);
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, r.Now.AddMinutes(30))
            .SetProperty(o => o.Status, CheckoutOrderStatus.Paid).SetProperty(o => o.PaidAtUtc, r.Now));
        Assert.Equal(CheckoutCouponChange.NotEditable, (await r.Apply(r.Buyer, order, "")).Change);
        await r.Db.CheckoutOrders.ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, CheckoutOrderStatus.Pending).SetProperty(o => o.PaidAtUtc, (DateTime?)null));
        var id = await r.Db.CheckoutOrders.Select(o => o.Id).SingleAsync();
        r.Db.PaymentTransactions.Add(new() { CheckoutOrderId = id, Amount = 91, Provider = "fixture", ProviderTransactionId = "event-1", OccurredAtUtc = r.Now });
        await r.Db.SaveChangesAsync();
        Assert.Equal(CheckoutCouponChange.NotEditable, (await r.Apply(r.Buyer, order, "")).Change);
    }

    [SqlServerFact]
    public async Task Item_write_failure_rolls_back_order_totals_and_reservation()
    {
        await using var r = await Rig.CreateAsync(); var order = await r.Order(r.Buyer, r.A);
        var input = await r.Input(order);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(r.Db.Database.GetConnectionString())
            .AddInterceptors(new FailItems()).Options;
        await using (var failing = new AppDbContext(options))
            await Assert.ThrowsAsync<SimulatedFailure>(() => CheckoutCouponDAO.Instance.ApplyAsync(failing, r.Buyer, input, r.Now));
        var stored = await r.Db.CheckoutOrders.AsNoTracking().SingleAsync();
        Assert.Equal(0, stored.DiscountAmount); Assert.Equal(101, stored.PayableAmount);
        Assert.Equal(0, await CheckoutCouponReservations.CountAsync(r.Db, r.CouponId, r.Now));
    }

    [SqlServerFact]
    public async Task Management_limit_change_and_coupon_application_coordinate_under_same_lock()
    {
        await using var r = await Rig.CreateAsync();
        var a = await r.Order(r.Buyer, r.A); var b = await r.Order(r.Buyer2, r.A);
        await r.Apply(r.Buyer, a);
        var input = await r.Input(b); var coupon = await r.Db.Coupons.AsNoTracking().SingleAsync();
        var terms = new CouponInput("DEMO10", 10, 1, coupon.StartDate, coupon.EndDate, null);
        using var management = r.Data.Services.CreateScope(); using var checkout = r.Data.Services.CreateScope();
        var save = CouponDAO.Instance.SaveAsync(management.ServiceProvider.GetRequiredService<AppDbContext>(),
            new(r.Owner, "issuer@example.test"), terms, coupon.Id, CouponPresentation.Version(coupon));
        var apply = CheckoutCouponDAO.Instance.ApplyAsync(checkout.ServiceProvider.GetRequiredService<AppDbContext>(), r.Buyer2, input, r.Now);
        await Task.WhenAll(save, apply);
        Assert.True(save.Result == CouponChange.Saved && apply.Result.Change == CheckoutCouponChange.Exhausted ||
            save.Result == CouponChange.UsageConflict && apply.Result.Change == CheckoutCouponChange.Applied);
        var stored = await r.Db.Coupons.AsNoTracking().SingleAsync();
        Assert.True(stored.UsedCount + await CheckoutCouponReservations.CountAsync(r.Db, stored.Id, r.Now) <= stored.MaxUsage);
    }

    private sealed class SimulatedFailure : DbException;
    private sealed class FailItems : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("CheckoutOrderItems"))
                throw new SimulatedFailure();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class Rig(AdminUserSqlServerTests.SqlDatabase data, IServiceScope scope) : IAsyncDisposable
    {
        public AdminUserSqlServerTests.SqlDatabase Data => data;
        public AppDbContext Db => scope.ServiceProvider.GetRequiredService<AppDbContext>();
        public DateTime Now { get; } = DateTime.UtcNow;
        public string Buyer = "", Buyer2 = "", Owner = ""; public int A, B, Other, CouponId;
        public static async Task<Rig> CreateAsync()
        {
            var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
            var rig = new Rig(data, data.Services.CreateScope());
            rig.Buyer = await data.CreateUserAsync("buyer@example.test", "Student");
            rig.Buyer2 = await data.CreateUserAsync("buyer2@example.test", "Student");
            rig.Owner = await data.CreateUserAsync("issuer@example.test", "Instructor");
            var a = new Course { Title = "A", InstructorId = rig.Owner, Price = 101, IsPublished = true, Status = CourseStatus.Approved };
            var b = new Course { Title = "B", InstructorId = "ISSUER@EXAMPLE.TEST", Price = 250, IsPublished = true, Status = CourseStatus.Approved };
            var other = new Course { Title = "Other", InstructorId = "other-instructor", Price = 300, IsPublished = true, Status = CourseStatus.Approved };
            var coupon = new Coupon { Code = "DEMO10", InstructorId = rig.Owner, DiscountPercent = 10, MaxUsage = 3,
                StartDate = rig.Now.AddDays(-1), EndDate = rig.Now.AddDays(1) };
            rig.Db.AddRange(a, b, other, coupon); await rig.Db.SaveChangesAsync();
            rig.A = a.Id; rig.B = b.Id; rig.Other = other.Id; rig.CouponId = coupon.Id; rig.Db.ChangeTracker.Clear(); return rig;
        }
        public async Task<CheckoutCreated> Order(string buyer, params int[] ids)
        {
            var result = await CheckoutDAO.Instance.CreateAsync(Db, buyer, new(Guid.NewGuid(), ids), Now);
            Assert.Equal(CheckoutChange.Created, result.Change); return result.Order!;
        }
        public async Task<CheckoutCouponInput> Input(CheckoutCreated order, string code = "DEMO10") =>
            new(order.PublicId, code, Convert.ToBase64String(await Db.CheckoutOrders.Where(o => o.PublicId == order.PublicId).Select(o => o.RowVersion).SingleAsync()));
        public async Task<CheckoutCouponResult> Apply(string buyer, CheckoutCreated order, string code = "DEMO10") =>
            await CheckoutCouponDAO.Instance.ApplyAsync(Db, buyer, await Input(order, code), Now);
        public async ValueTask DisposeAsync() { scope.Dispose(); await data.DisposeAsync(); }
    }
}
