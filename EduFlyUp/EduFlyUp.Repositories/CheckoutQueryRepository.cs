using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Repositories;

public interface ICheckoutQueryRepository
{
    Task<CheckoutPreview?> PreviewAsync(string buyerId, int courseId);
    Task<CheckoutDetail?> DetailAsync(string buyerId, Guid id, DateTime now);
}

public sealed class CheckoutQueryRepository(AppDbContext db) : ICheckoutQueryRepository
{
    private async Task<ApplicationUser?> BuyerAsync(string id)
    {
        if (!await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                    where ur.UserId == id && r.Name == "Student" select ur).AnyAsync()) return null;
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive);
    }

    public async Task<CheckoutPreview?> PreviewAsync(string buyerId, int courseId)
    {
        var buyer = await BuyerAsync(buyerId); if (buyer is null) return null;
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courseId && c.IsPublished && c.Status == CourseStatus.Approved);
        if (course is null || string.IsNullOrWhiteSpace(course.InstructorId) ||
            string.Equals(course.InstructorId, buyer.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(course.InstructorId, buyer.UserName, StringComparison.OrdinalIgnoreCase) ||
            course.Price < 0 || course.Price > CheckoutPolicy.MaxVndAmount || decimal.Truncate(course.Price) != course.Price) return null;
        return new(course.Id, course.Title, course.Price);
    }

    public async Task<CheckoutDetail?> DetailAsync(string buyerId, Guid id, DateTime now)
    {
        if (await BuyerAsync(buyerId) is null) return null;
        var order = await db.CheckoutOrders.AsNoTracking().Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.PublicId == id && o.BuyerId == buyerId);
        if (order is null) return null;
        var canEdit = order.Status == CheckoutOrderStatus.Pending && order.ExpiresAtUtc > now &&
            !await db.PaymentTransactions.AnyAsync(t => t.CheckoutOrderId == order.Id);
        var status = order.Status == CheckoutOrderStatus.Pending && order.ExpiresAtUtc <= now ? CheckoutOrderStatus.Expired : order.Status;
        return new(order.PublicId, status, order.OriginalAmount, order.DiscountAmount, order.PayableAmount,
            order.ExpiresAtUtc, order.PaidAtUtc, Convert.ToBase64String(order.RowVersion), canEdit,
            order.Items.OrderBy(i => i.CourseId).Select(i => new CheckoutLine(i.CourseId, i.CourseTitle,
                i.OriginalAmount, i.DiscountAmount, i.PayableAmount, i.CouponCode)).ToArray());
    }
}
