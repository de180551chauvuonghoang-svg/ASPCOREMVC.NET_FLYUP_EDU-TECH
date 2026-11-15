using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories;

public interface IOrderRepository
{
    Task<Order> CreateAsync(Order order);
    Task<Order?> GetByCodeAsync(string orderCode);
    Task<Order?> GetByIdAsync(int id);
    Task<Order?> GetUserPendingOrderAsync(string userId, int courseId);
    Task<bool> MarkAsPaidAsync(string orderCode, string? transactionRef);
    Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId);
}
