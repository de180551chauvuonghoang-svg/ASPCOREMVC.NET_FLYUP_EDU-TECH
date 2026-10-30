using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public sealed class InstructorPayoutDAO : SingletonBase<InstructorPayoutDAO>
{
    public async Task<InstructorPayoutHistory> HistoryAsync(AppDbContext db, InstructorIdentity instructor, int page)
    {
        var query = db.PayoutRequests.AsNoTracking()
            .Where(p => p.InstructorId == instructor.UserId || p.InstructorId == instructor.UserName);
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (count + InstructorPayoutHistory.PageSize - 1) / InstructorPayoutHistory.PageSize));
        var rows = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * InstructorPayoutHistory.PageSize).Take(InstructorPayoutHistory.PageSize)
            .Select(p => new InstructorPayoutRow(p.Id, p.Amount, p.Status, p.CreatedAt, p.ProcessedAt, p.AdminNote)).ToListAsync();
        return new(rows, page, count);
    }
}
