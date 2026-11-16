using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public enum AdminPayoutChange { Updated, NotFound, Forbidden, AlreadyProcessed, BalanceUnavailable, Invalid }

public sealed class AdminPayoutDAO(AppDbContext db, IInstructorEarningsSource earnings, TimeProvider time)
{
    public async Task<InstructorPayoutHistory> ListAsync(int page, PayoutStatus? status)
    {
        var query = db.PayoutRequests.AsNoTracking();
        if (status is { } filter)
            query = query.Where(p => p.Status == filter);
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / (double)InstructorPayoutHistory.PageSize)));
        var items = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * InstructorPayoutHistory.PageSize).Take(InstructorPayoutHistory.PageSize)
            .Select(p => new InstructorPayoutRow(p.Id, p.Amount, p.Status, p.CreatedAt, p.ProcessedAt, null))
            .ToListAsync();
        return new(items, page, count, status);
    }

    public Task<PayoutRequest?> DetailAsync(int id) =>
        db.PayoutRequests.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);

    public async Task<AdminPayoutChange> ProcessAsync(int id, string adminId, bool transferred, string? note)
    {
        note = note?.Trim();
        if (id <= 0 || string.IsNullOrWhiteSpace(note) || note.Length > 250 || note.Any(char.IsControl))
            return AdminPayoutChange.Invalid;
        if (string.IsNullOrWhiteSpace(adminId))
            return AdminPayoutChange.Forbidden;

        await using var transaction = await db.Database.BeginTransactionAsync();
        await PayoutWriteLock.AcquireAsync(db);
        var authorized = await (from user in db.Users
            join membership in db.UserRoles on user.Id equals membership.UserId
            join role in db.Roles on membership.RoleId equals role.Id
            where user.Id == adminId && user.IsActive && role.NormalizedName == "ADMIN"
            select user.Id).AnyAsync();
        if (!authorized)
            return AdminPayoutChange.Forbidden;

        var payout = await db.PayoutRequests.SingleOrDefaultAsync(p => p.Id == id);
        if (payout is null)
            return AdminPayoutChange.NotFound;
        if (payout.Status != PayoutStatus.Pending)
            return AdminPayoutChange.AlreadyProcessed;

        if (transferred)
        {
            var owner = payout.InstructorId;
            var normalized = owner.ToUpperInvariant();
            var owners = await db.Users.AsNoTracking()
                .Where(u => u.Id == owner || u.NormalizedUserName == normalized)
                .Select(u => new InstructorIdentity(u.Id, u.UserName!)).Take(2).ToListAsync();
            if (owners.Count != 1)
                return AdminPayoutChange.BalanceUnavailable;

            var balance = await InstructorPayoutDAO.Instance.BalanceAsync(db, earnings, owners[0]);
            if (payout.Amount <= 0 || balance.PendingAmount < payout.Amount || balance.CreditedEarnings is not decimal credited ||
                credited - balance.ApprovedAmount < balance.PendingAmount)
                return AdminPayoutChange.BalanceUnavailable;
        }

        payout.Status = transferred ? PayoutStatus.Approved : PayoutStatus.Rejected;
        payout.AdminNote = transferred ? "Mã chuyển khoản: " + note : note;
        payout.ProcessedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return AdminPayoutChange.Updated;
    }
}
