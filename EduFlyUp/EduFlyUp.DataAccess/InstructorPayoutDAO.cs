using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public sealed class InstructorPayoutDAO : SingletonBase<InstructorPayoutDAO>
{
    public async Task<InstructorPayoutBalance> BalanceAsync(AppDbContext db, IInstructorEarningsSource earnings, InstructorIdentity instructor)
    {
        var credited = await earnings.CreditedEarningsAsync(db, instructor);
        var summary = await db.PayoutRequests.AsNoTracking()
            .Where(p => p.InstructorId == instructor.UserId || p.InstructorId == instructor.UserName)
            .GroupBy(_ => 1).Select(g => new
            {
                Pending = g.Sum(p => p.Status == PayoutStatus.Pending ? p.Amount : 0),
                Approved = g.Sum(p => p.Status == PayoutStatus.Approved ? p.Amount : 0),
                PendingCount = g.Count(p => p.Status == PayoutStatus.Pending),
                Invalid = g.Any(p => (p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.Approved) && p.Amount <= 0)
            }).SingleOrDefaultAsync();
        if (credited < 0 || (credited.HasValue && decimal.Round(credited.Value, 2) != credited.Value) || summary?.Invalid == true) credited = null;
        return new(credited, Math.Max(0, summary?.Pending ?? 0), Math.Max(0, summary?.Approved ?? 0), summary?.PendingCount ?? 0);
    }

    public async Task<InstructorPayoutChange> CreateAsync(AppDbContext db, IInstructorEarningsSource earnings,
        InstructorIdentity instructor, InstructorPayoutInput input)
    {
        if (string.IsNullOrWhiteSpace(instructor.UserId) || string.IsNullOrWhiteSpace(instructor.UserName) || !InstructorPayoutPolicy.Valid(input))
            return InstructorPayoutChange.Invalid;
        if (!(await earnings.CreditedEarningsAsync(db, instructor)).HasValue)
            return InstructorPayoutChange.EarningsUnavailable;
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        // Settlement and Admin payout writers must use this same lock for balance-affecting changes.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=N'EduFlyUp.PayoutManagement',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51002, N'Không thể lấy khóa quản lý yêu cầu rút tiền.', 1;
            """);
        var balance = await BalanceAsync(db, earnings, instructor);
        if (!balance.Available.HasValue) return InstructorPayoutChange.EarningsUnavailable;
        if (balance.PendingCount > 0) return InstructorPayoutChange.PendingExists;
        if (input.Amount > balance.Available.Value) return InstructorPayoutChange.InsufficientBalance;
        db.PayoutRequests.Add(new PayoutRequest
        {
            InstructorId = instructor.UserId, Amount = input.Amount, BankName = input.BankName,
            AccountNumber = input.AccountNumber, AccountHolderName = input.AccountHolderName,
            Status = PayoutStatus.Pending, CreatedAt = DateTime.UtcNow, ProcessedAt = null, AdminNote = null
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return InstructorPayoutChange.Created;
    }

    public async Task<InstructorPayoutHistory> HistoryAsync(AppDbContext db, InstructorIdentity instructor, int page, PayoutStatus? status = null)
    {
        var query = db.PayoutRequests.AsNoTracking()
            .Where(p => p.InstructorId == instructor.UserId || p.InstructorId == instructor.UserName);
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (count + InstructorPayoutHistory.PageSize - 1) / InstructorPayoutHistory.PageSize));
        var rows = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * InstructorPayoutHistory.PageSize).Take(InstructorPayoutHistory.PageSize)
            .Select(p => new InstructorPayoutRow(p.Id, p.Amount, p.Status, p.CreatedAt, p.ProcessedAt, p.AdminNote)).ToListAsync();
        return new(rows, page, count, status);
    }
}
