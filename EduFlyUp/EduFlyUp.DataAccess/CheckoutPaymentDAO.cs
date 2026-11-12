using System.Data;
using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public sealed class CheckoutPaymentDAO : SingletonBase<CheckoutPaymentDAO>
{
    public async Task<CheckoutPaymentResult> PrepareAsync(AppDbContext db, string buyerId, CheckoutPaymentInput input,
        CheckoutPaymentReceiver receiver, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(buyerId) || buyerId.Length > 450 || !CheckoutPaymentPolicy.Valid(input) || now.Kind != DateTimeKind.Utc)
            return new(CheckoutPaymentChange.Invalid);
        if (!CheckoutPaymentPolicy.ValidReceiver(receiver)) return new(CheckoutPaymentChange.NotConfigured);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
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
                    where ur.UserId == buyerId && r.Name == "Student" select ur).AnyAsync()) return new(CheckoutPaymentChange.Forbidden);
        var order = await db.CheckoutOrders.AsNoTracking().Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.PublicId == input.OrderPublicId && o.BuyerId == buyerId);
        if (order is null) return new(CheckoutPaymentChange.NotFound);
        if (order.Status != CheckoutOrderStatus.Pending || order.ExpiresAtUtc <= now || order.Currency != "VND" ||
            order.PayableAmount <= 0 || order.PayableAmount > CheckoutPolicy.MaxVndAmount || decimal.Truncate(order.PayableAmount) != order.PayableAmount)
            return new(CheckoutPaymentChange.NotPayable);
        var previous = await db.CheckoutPaymentIntents.AsNoTracking().SingleOrDefaultAsync(i => i.CheckoutOrderId == order.Id);
        if (previous is not null)
        {
            if (previous.Amount != order.PayableAmount || previous.Currency != order.Currency || previous.ExpiresAtUtc != order.ExpiresAtUtc ||
                !CheckoutPaymentPolicy.ValidReceiver(new(previous.BankBin, previous.AccountNumber, previous.AccountName)))
                return new(CheckoutPaymentChange.NotPayable);
            return Result(CheckoutPaymentChange.AlreadyPrepared, previous);
        }
        if (!order.RowVersion.SequenceEqual(Convert.FromBase64String(input.Version!))) return new(CheckoutPaymentChange.Stale);
        if (await db.PaymentTransactions.AnyAsync(t => t.CheckoutOrderId == order.Id) || order.Items.Count == 0 ||
            order.Items.Any(i => i.OriginalAmount < 0 || i.DiscountAmount < 0 || i.DiscountAmount > i.OriginalAmount ||
                i.PayableAmount != i.OriginalAmount - i.DiscountAmount || decimal.Truncate(i.PayableAmount) != i.PayableAmount) ||
            order.Items.Sum(i => i.OriginalAmount) != order.OriginalAmount || order.Items.Sum(i => i.DiscountAmount) != order.DiscountAmount ||
            order.Items.Sum(i => i.PayableAmount) != order.PayableAmount) return new(CheckoutPaymentChange.NotPayable);
        var intent = new CheckoutPaymentIntent
        {
            CheckoutOrderId = order.Id, Reference = "FU" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),
            BankBin = receiver.BankBin, AccountNumber = receiver.AccountNumber, AccountName = receiver.AccountName,
            Amount = order.PayableAmount, ExpiresAtUtc = order.ExpiresAtUtc, CreatedAt = now
        };
        db.CheckoutPaymentIntents.Add(intent);
        await db.SaveChangesAsync();
        await db.CheckoutOrders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.UpdatedAt, now));
        await transaction.CommitAsync();
        return Result(CheckoutPaymentChange.Prepared, intent);
    }
    private static CheckoutPaymentResult Result(CheckoutPaymentChange change, CheckoutPaymentIntent intent) => new(change,
        new(intent.Reference, intent.BankBin, intent.AccountNumber, intent.AccountName, intent.Amount, intent.ExpiresAtUtc));
}
