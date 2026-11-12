using System.Data;
using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public static class CheckoutCouponReservations
{
    // Gọi dưới CouponManagement; đếm theo đơn, không đếm theo số dòng khóa học.
    public static Task<int> CountAsync(AppDbContext db, int couponId, DateTime utcNow, int exceptOrderId = 0)
        => db.CheckoutOrders.CountAsync(o => o.Id != exceptOrderId && o.Status == CheckoutOrderStatus.Pending &&
            o.ExpiresAtUtc > utcNow && o.Items.Any(i => i.CouponId == couponId));
}

public sealed class CheckoutCouponDAO : SingletonBase<CheckoutCouponDAO>
{
    public async Task<CheckoutCouponResult> ApplyAsync(AppDbContext db, string buyerId, CheckoutCouponInput input, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(buyerId) || buyerId.Length > 450 || !CheckoutCouponPolicy.Valid(input) || now.Kind != DateTimeKind.Utc)
            return new(CheckoutCouponChange.Invalid);
        var code = CheckoutCouponPolicy.Normalize(input.Code);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Cùng thứ tự với writer thanh toán tương lai: Checkout -> Coupon -> Payout.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=N'EduFlyUp.CheckoutManagement',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51002, N'Không thể lấy khóa xử lý đơn hàng.', 1;
            EXEC @result = sp_getapplock @Resource=N'EduFlyUp.CouponManagement',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51001, N'Không thể lấy khóa quản lý mã giảm giá.', 1;
            """);
        if (!await db.Users.AnyAsync(u => u.Id == buyerId && u.IsActive) ||
            !await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                    where ur.UserId == buyerId && r.Name == "Student" select ur).AnyAsync())
            return new(CheckoutCouponChange.Forbidden);
        var order = await db.CheckoutOrders.AsNoTracking().Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.PublicId == input.OrderPublicId && o.BuyerId == buyerId);
        if (order is null) return new(CheckoutCouponChange.NotFound);
        if (!order.RowVersion.SequenceEqual(Convert.FromBase64String(input.Version!))) return new(CheckoutCouponChange.Stale);
        if (order.Status != CheckoutOrderStatus.Pending || order.ExpiresAtUtc <= now ||
            await db.PaymentTransactions.AnyAsync(t => t.CheckoutOrderId == order.Id) ||
            await db.CheckoutPaymentIntents.AnyAsync(i => i.CheckoutOrderId == order.Id)) return new(CheckoutCouponChange.NotEditable);
        if (order.Items.Count == 0 || order.Items.Any(i => i.OriginalAmount < 0 || decimal.Truncate(i.OriginalAmount) != i.OriginalAmount) ||
            order.OriginalAmount != order.Items.Sum(i => i.OriginalAmount)) return new(CheckoutCouponChange.NotEditable);

        var appliedCodes = order.Items.Where(i => i.CouponId.HasValue).Select(i => i.CouponCode).Distinct().ToArray();
        if ((appliedCodes.Length == 1 && appliedCodes[0] == code) || (code.Length == 0 && appliedCodes.Length == 0))
            return Summary(order, code.Length == 0 ? null : code);

        Coupon? coupon = null;
        HashSet<int> eligible = [];
        if (code.Length > 0)
        {
            var matches = await db.Coupons.AsNoTracking().Where(c => c.Code.Trim().ToUpper() == code).Take(2).ToListAsync();
            if (matches.Count != 1) return new(CheckoutCouponChange.InvalidCoupon);
            coupon = matches[0];
            if (!coupon.IsActive || coupon.StartDate > now || coupon.EndDate <= now || coupon.EndDate <= coupon.StartDate ||
                coupon.DiscountPercent is < 1 or > 90 || coupon.UsedCount < 0 || coupon.MaxUsage < 1)
                return new(CheckoutCouponChange.InvalidCoupon);
            var owners = await db.Users.AsNoTracking().Where(u => u.Id == coupon.InstructorId || u.UserName == coupon.InstructorId).Take(2).ToListAsync();
            if (owners.Count != 1) return new(CheckoutCouponChange.InvalidCoupon);
            var owner = owners[0];
            eligible = order.Items.Where(i => i.OriginalAmount > 0 && (!coupon.CourseId.HasValue || coupon.CourseId == i.CourseId) &&
                (string.Equals(i.InstructorId, owner.Id, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(i.InstructorId, owner.UserName, StringComparison.OrdinalIgnoreCase)))
                .Where(i => CheckoutCouponPolicy.Discount(i.OriginalAmount, coupon.DiscountPercent) > 0).Select(i => i.Id).ToHashSet();
            if (eligible.Count == 0) return new(CheckoutCouponChange.InvalidCoupon);
            var reserved = await CheckoutCouponReservations.CountAsync(db, coupon.Id, now, order.Id);
            if ((long)coupon.UsedCount + reserved >= coupon.MaxUsage) return new(CheckoutCouponChange.Exhausted);
        }
        foreach (var item in order.Items)
        {
            var applied = coupon is not null && eligible.Contains(item.Id);
            item.CouponId = applied ? coupon!.Id : null; item.CouponCode = applied ? code : null;
            item.CouponPercent = applied ? coupon!.DiscountPercent : null;
            item.DiscountAmount = applied ? CheckoutCouponPolicy.Discount(item.OriginalAmount, coupon!.DiscountPercent) : 0;
            item.PayableAmount = item.OriginalAmount - item.DiscountAmount;
        }
        var discount = order.Items.Sum(i => i.DiscountAmount);
        var changed = await db.CheckoutOrders.Where(o => o.Id == order.Id && o.RowVersion == order.RowVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.DiscountAmount, discount)
                .SetProperty(o => o.PayableAmount, order.OriginalAmount - discount).SetProperty(o => o.UpdatedAt, now));
        if (changed != 1) return new(CheckoutCouponChange.Stale);
        foreach (var item in order.Items)
            await db.CheckoutOrderItems.Where(i => i.Id == item.Id).ExecuteUpdateAsync(s =>
                s.SetProperty(i => i.CouponId, item.CouponId).SetProperty(i => i.CouponCode, item.CouponCode)
                 .SetProperty(i => i.CouponPercent, item.CouponPercent).SetProperty(i => i.DiscountAmount, item.DiscountAmount)
                 .SetProperty(i => i.PayableAmount, item.PayableAmount).SetProperty(i => i.UpdatedAt, now));
        order.DiscountAmount = discount; order.PayableAmount = order.OriginalAmount - discount;
        order.RowVersion = await db.CheckoutOrders.Where(o => o.Id == order.Id).Select(o => o.RowVersion).SingleAsync();
        await transaction.CommitAsync();
        return Summary(order, code.Length == 0 ? null : code);
    }

    private static CheckoutCouponResult Summary(CheckoutOrder order, string? code) => new(CheckoutCouponChange.Applied,
        new(order.PublicId, order.OriginalAmount, order.DiscountAmount, order.PayableAmount, code, Convert.ToBase64String(order.RowVersion)));
}
