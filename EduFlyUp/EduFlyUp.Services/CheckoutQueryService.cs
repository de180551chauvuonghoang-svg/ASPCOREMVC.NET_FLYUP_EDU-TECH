using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface ICheckoutQueryService
{
    Task<CheckoutPreview?> PreviewAsync(ClaimsPrincipal user, int courseId);
    Task<CheckoutDetail?> DetailAsync(ClaimsPrincipal user, Guid id);
}

public sealed class CheckoutQueryService(ICheckoutQueryRepository repository, TimeProvider time) : ICheckoutQueryService
{
    private static string? Buyer(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true && user.IsInRole("Student")
        ? user.FindFirst(ClaimTypes.NameIdentifier)?.Value : null;
    public Task<CheckoutPreview?> PreviewAsync(ClaimsPrincipal user, int courseId) => Buyer(user) is { Length: > 0 and <= 450 } buyer && courseId > 0
        ? repository.PreviewAsync(buyer, courseId) : Task.FromResult<CheckoutPreview?>(null);
    public Task<CheckoutDetail?> DetailAsync(ClaimsPrincipal user, Guid id) => Buyer(user) is { Length: > 0 and <= 450 } buyer && id != Guid.Empty
        ? repository.DetailAsync(buyer, id, time.GetUtcNow().UtcDateTime) : Task.FromResult<CheckoutDetail?>(null);
}
