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

    public async Task<bool> MarkAsPaidAsync(AppDbContext context, string orderCode, long transactionId, decimal amount, string? transactionRef)
    {
        if (transactionId <= 0 || amount <= 0 || decimal.Truncate(amount) != amount)
            return false;

        await using var transaction = await context.Database.BeginTransactionAsync();
        await PayoutWriteLock.AcquireAsync(context);
        var order = await context.Orders.Include(o => o.Course).SingleOrDefaultAsync(o => o.OrderCode == orderCode);
        if (order is null || order.PaymentMethod != "VietQR_SePay" || order.Amount != amount)
            return false;

        var previous = await context.SePayReceipts.FindAsync(transactionId);
        if (previous is not null)
            return previous.OrderId == order.Id && previous.Amount == amount && order.Status == OrderStatus.Paid;

        if (order.Status != OrderStatus.Pending || string.IsNullOrWhiteSpace(order.Course.InstructorId) ||
            order.Course.InstructorId.Length > 450 || string.IsNullOrWhiteSpace(order.Course.Title))
            return false;

        // Khóa học cũ có thể lưu username. Quy đổi sang ID để đổi email không làm chuyển khoản thu.
        var owner = order.Course.InstructorId;
        var normalizedOwner = owner.ToUpperInvariant();
        var ownerIds = await context.Users.AsNoTracking()
            .Where(user => user.Id == owner || user.NormalizedUserName == normalizedOwner)
            .Select(user => user.Id).Take(2).ToListAsync();
        if (ownerIds.Count != 1)
            return false;

        var now = DateTime.UtcNow;
        order.Status = OrderStatus.Paid;
        order.TransactionReference = !string.IsNullOrWhiteSpace(transactionRef) && transactionRef.Length <= 100
            ? transactionRef : transactionId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        order.PaidAt = now;
        context.SePayReceipts.Add(new SePayReceipt
        {
            TransactionId = transactionId,
            OrderId = order.Id,
            CourseId = order.CourseId,
            CourseTitle = order.Course.Title,
            InstructorId = ownerIds[0],
            Amount = amount,
            VerifiedAtUtc = now
        });
        var enrollment = await context.Enrollments.FirstOrDefaultAsync(e => e.UserId == order.UserId && e.CourseId == order.CourseId);
        if (enrollment is null)
        {
            await context.Enrollments.AddAsync(new Enrollment
            {
                UserId = order.UserId,
                CourseId = order.CourseId,
                EnrolledAt = now,
                Status = EnrollmentStatus.Active
            });
        }
        else if (enrollment.Status == EnrollmentStatus.Cancelled)
        {
            enrollment.Status = EnrollmentStatus.Active;
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
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
