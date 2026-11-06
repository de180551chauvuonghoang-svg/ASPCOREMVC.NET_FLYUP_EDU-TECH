namespace EduFlyUp.BusinessObjects;

// Không nhận giá, người mua, trạng thái thanh toán hoặc giảng viên từ client.
public sealed record CheckoutInput(Guid SubmissionKey, IReadOnlyList<int>? CourseIds);
public enum CheckoutChange
{
    Created, AlreadySubmitted, Invalid, Forbidden, CourseUnavailable,
    AlreadyEnrolled, PendingExists, SubmissionConflict, Unavailable
}
public sealed record CheckoutCreated(Guid PublicId, CheckoutOrderStatus Status, decimal PayableAmount, DateTime ExpiresAtUtc);
public sealed record CheckoutResult(CheckoutChange Change, CheckoutCreated? Order = null);

public static class CheckoutPolicy
{
    public const int MaxCourses = 20;
    public const decimal MaxVndAmount = 9999999999999m;
    public static bool Valid(CheckoutInput? input) => input is not null && input.SubmissionKey != Guid.Empty &&
        input.CourseIds is { Count: > 0 and <= MaxCourses } ids && ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count;
}
