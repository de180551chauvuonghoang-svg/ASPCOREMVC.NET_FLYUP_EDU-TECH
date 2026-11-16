using System.Globalization;
using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public sealed class SePayInstructorSettlementSource : IInstructorSettlementSource
{
    public async Task<IReadOnlyList<InstructorSettledReceipt>?> ReadVerifiedReceiptsAsync(
        AppDbContext db, InstructorIdentity instructor)
    {
        var rows = await db.SePayReceipts.AsNoTracking()
            .Where(receipt => receipt.InstructorId == instructor.UserId)
            .ToListAsync();

        return rows.Select(receipt => new InstructorSettledReceipt(
            "SePay/" + receipt.TransactionId.ToString(CultureInfo.InvariantCulture),
            receipt.InstructorId,
            receipt.Amount,
            0,
            new InstructorSettlementInfo(receipt.CourseId, receipt.CourseTitle, null, null, receipt.VerifiedAtUtc)))
            .ToArray();
    }
}
