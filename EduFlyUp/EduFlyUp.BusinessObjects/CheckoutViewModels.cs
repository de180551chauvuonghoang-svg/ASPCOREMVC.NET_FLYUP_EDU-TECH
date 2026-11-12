namespace EduFlyUp.BusinessObjects;

public sealed record CheckoutPreview(int CourseId, string Title, decimal Price);
public sealed record CheckoutLine(int CourseId, string Title, decimal Original, decimal Discount, decimal Payable, string? CouponCode);
public sealed record CheckoutDetail(Guid PublicId, CheckoutOrderStatus Status, decimal Original, decimal Discount,
    decimal Payable, DateTime ExpiresAtUtc, DateTime? PaidAtUtc, string Version, bool CanEditCoupon, IReadOnlyList<CheckoutLine> Items,
    CheckoutPaymentInstructions? PaymentInstructions = null);
