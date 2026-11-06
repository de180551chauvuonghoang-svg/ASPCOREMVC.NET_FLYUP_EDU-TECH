using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface ICheckoutRepository
{
    Task<CheckoutResult> CreateAsync(string buyerId, CheckoutInput input, DateTime utcNow);
}

public sealed class CheckoutRepository(AppDbContext db) : ICheckoutRepository
{
    public Task<CheckoutResult> CreateAsync(string buyerId, CheckoutInput input, DateTime utcNow)
        => CheckoutDAO.Instance.CreateAsync(db, buyerId, input, utcNow);
}
