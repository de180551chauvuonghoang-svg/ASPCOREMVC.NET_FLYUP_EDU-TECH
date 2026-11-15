using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public class OrderDAO : SingletonBase<OrderDAO>
{
    public async Task<Order> CreateAsync(Order order)
    {
        using var context = new AppDbContext();
        await context.Orders.AddAsync(order);
        await context.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> GetByCodeAsync(string orderCode)
    {
        using var context = new AppDbContext();
        return await context.Orders
            .Include(o => o.Course)
            .FirstOrDefaultAsync(o => o.OrderCode == orderCode);
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        using var context = new AppDbContext();
        return await context.Orders
            .Include(o => o.Course)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order?> GetUserPendingOrderAsync(string userId, int courseId)
    {
        using var context = new AppDbContext();
        return await context.Orders
            .Include(o => o.Course)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.CourseId == courseId && o.Status == OrderStatus.Pending);
    }

    public async Task<bool> MarkAsPaidAsync(string orderCode, string? transactionRef)
    {
        using var context = new AppDbContext();
        var order = await context.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode);
        if (order == null || order.Status == OrderStatus.Paid) return false;

        order.Status = OrderStatus.Paid;
        order.TransactionReference = transactionRef;
        order.PaidAt = DateTime.UtcNow;

        // Tự động kích hoạt ghi danh khóa học cho học viên nếu chưa ghi danh
        var alreadyEnrolled = await context.Enrollments.AnyAsync(e => e.UserId == order.UserId && e.CourseId == order.CourseId);
        if (!alreadyEnrolled)
        {
            await context.Enrollments.AddAsync(new Enrollment
            {
                UserId = order.UserId,
                CourseId = order.CourseId,
                EnrolledAt = DateTime.UtcNow,
                Status = EnrollmentStatus.Active
            });
        }
        else
        {
            // Nếu đã từng có bản ghi nhưng chưa active thì kích hoạt lại
            var existing = await context.Enrollments.FirstOrDefaultAsync(e => e.UserId == order.UserId && e.CourseId == order.CourseId);
            if (existing != null && existing.Status != EnrollmentStatus.Active)
            {
                existing.Status = EnrollmentStatus.Active;
            }
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId)
    {
        using var context = new AppDbContext();
        return await context.Orders
            .Include(o => o.Course)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
}
