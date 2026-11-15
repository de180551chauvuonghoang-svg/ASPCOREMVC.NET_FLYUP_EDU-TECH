using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public class OrderRepository : IOrderRepository
{
    public Task<Order> CreateAsync(Order order) => OrderDAO.Instance.CreateAsync(order);
    public Task<Order?> GetByCodeAsync(string orderCode) => OrderDAO.Instance.GetByCodeAsync(orderCode);
    public Task<Order?> GetByIdAsync(int id) => OrderDAO.Instance.GetByIdAsync(id);
    public Task<Order?> GetUserPendingOrderAsync(string userId, int courseId) => OrderDAO.Instance.GetUserPendingOrderAsync(userId, courseId);
    public Task<bool> MarkAsPaidAsync(string orderCode, string? transactionRef) => OrderDAO.Instance.MarkAsPaidAsync(orderCode, transactionRef);
    public Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId) => OrderDAO.Instance.GetOrdersByUserIdAsync(userId);
}
