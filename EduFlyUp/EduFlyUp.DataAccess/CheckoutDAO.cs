using System.Data;
using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public sealed class CheckoutDAO : SingletonBase<CheckoutDAO>
{
    public async Task<CheckoutResult> CreateAsync(AppDbContext db, string buyerId, CheckoutInput input, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(buyerId) || buyerId.Length > 450 || !CheckoutPolicy.Valid(input) || now.Kind != DateTimeKind.Utc)
            return new(CheckoutChange.Invalid);
        var ids = input.CourseIds!.Order().ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Cùng khóa cho tạo checkout/xác nhận thanh toán/ghi danh sau này, kể cả khác app instance.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=N'EduFlyUp.CheckoutManagement',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51002, N'Không thể lấy khóa xử lý đơn hàng.', 1;
            """);
        var buyer = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == buyerId && u.IsActive);
        if (buyer is null || !await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                                   where ur.UserId == buyerId && r.Name == "Student" select ur).AnyAsync())
            return new(CheckoutChange.Forbidden);

        var previous = await db.CheckoutOrders.AsNoTracking().Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.BuyerId == buyerId && o.SubmissionKey == input.SubmissionKey);
        if (previous is not null)
            return previous.Items.Select(i => i.CourseId).Order().SequenceEqual(ids)
                ? Result(CheckoutChange.AlreadySubmitted, previous) : new(CheckoutChange.SubmissionConflict);

        var courses = await db.Courses.AsNoTracking().Where(c => ids.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync();
        if (courses.Count != ids.Length || courses.Any(c => !c.IsPublished || c.Status != CourseStatus.Approved ||
            string.IsNullOrWhiteSpace(c.InstructorId) || c.InstructorId.Length > 450 ||
            string.IsNullOrWhiteSpace(c.Title) || c.Title.Length > 200 ||
            string.Equals(c.InstructorId, buyer.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.InstructorId, buyer.UserName, StringComparison.OrdinalIgnoreCase) ||
            c.Price < 0 || c.Price > CheckoutPolicy.MaxVndAmount || decimal.Truncate(c.Price) != c.Price))
            return new(CheckoutChange.CourseUnavailable);

        var enrollments = await db.Enrollments.AsNoTracking().Where(e => e.UserId == buyerId && ids.Contains(e.CourseId)).ToListAsync();
        if (enrollments.Any(e => e.Status != EnrollmentStatus.Cancelled)) return new(CheckoutChange.AlreadyEnrolled);
        if (await db.CheckoutOrders.AsNoTracking().AnyAsync(o => o.BuyerId == buyerId && o.Status == CheckoutOrderStatus.Pending &&
            o.ExpiresAtUtc > now && o.Items.Any(i => ids.Contains(i.CourseId)))) return new(CheckoutChange.PendingExists);

        var total = courses.Sum(c => c.Price);
        if (total > CheckoutPolicy.MaxVndAmount) return new(CheckoutChange.Invalid);
        var buyerName = string.IsNullOrWhiteSpace(buyer.FullName) ? buyer.UserName ?? buyer.Id : buyer.FullName;
        var order = new CheckoutOrder
        {
            BuyerId = buyer.Id, BuyerName = buyerName.Length > 256 ? buyerName[..256] : buyerName,
            SubmissionKey = input.SubmissionKey, CreatedAt = now, ExpiresAtUtc = now.AddMinutes(30),
            OriginalAmount = total, PayableAmount = total,
            Status = total == 0 ? CheckoutOrderStatus.Paid : CheckoutOrderStatus.Pending,
            PaidAtUtc = total == 0 ? now : null,
            Items = courses.Select(c => new CheckoutOrderItem
            {
                CourseId = c.Id, CourseTitle = c.Title, InstructorId = c.InstructorId,
                OriginalAmount = c.Price, PayableAmount = c.Price, CreatedAt = now
            }).ToList()
        };
        if (total == 0)
        {
            foreach (var course in courses)
            {
                if (enrollments.Any(e => e.CourseId == course.Id))
                    await db.Enrollments.Where(e => e.UserId == buyerId && e.CourseId == course.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EnrollmentStatus.Active)
                            .SetProperty(e => e.EnrolledAt, now).SetProperty(e => e.CompletedAt, (DateTime?)null)
                            .SetProperty(e => e.UpdatedAt, now));
                else db.Enrollments.Add(new Enrollment { UserId = buyer.Id, CourseId = course.Id, EnrolledAt = now, CreatedAt = now });
            }
        }
        db.CheckoutOrders.Add(order);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result(CheckoutChange.Created, order);
    }

    private static CheckoutResult Result(CheckoutChange change, CheckoutOrder order)
        => new(change, new(order.PublicId, order.Status, order.PayableAmount, order.ExpiresAtUtc));
}
