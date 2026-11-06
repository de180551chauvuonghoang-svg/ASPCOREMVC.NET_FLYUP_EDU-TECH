using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface ICheckoutPaymentRepository
{
    Task<CheckoutPaymentResult> PrepareAsync(string buyerId, CheckoutPaymentInput input, CheckoutPaymentReceiver receiver, DateTime now);
}
public sealed class CheckoutPaymentRepository(AppDbContext db) : ICheckoutPaymentRepository
{
    public Task<CheckoutPaymentResult> PrepareAsync(string buyerId, CheckoutPaymentInput input, CheckoutPaymentReceiver receiver, DateTime now)
        => CheckoutPaymentDAO.Instance.PrepareAsync(db, buyerId, input, receiver, now);
}
