using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface ICheckoutCouponRepository
{
    Task<CheckoutCouponResult> ApplyAsync(string buyerId, CheckoutCouponInput input, DateTime utcNow);
}

public sealed class CheckoutCouponRepository(AppDbContext db) : ICheckoutCouponRepository
{
    public Task<CheckoutCouponResult> ApplyAsync(string buyerId, CheckoutCouponInput input, DateTime utcNow)
        => CheckoutCouponDAO.Instance.ApplyAsync(db, buyerId, input, utcNow);
}
