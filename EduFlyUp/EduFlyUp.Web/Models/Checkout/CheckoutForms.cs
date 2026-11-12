using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Checkout;

public sealed class CheckoutCreateForm
{
    [Range(1, int.MaxValue)] public int CourseId { get; set; }
    public Guid SubmissionKey { get; set; }
    public CheckoutPreview? Preview { get; set; }
}

public sealed class CheckoutCouponForm
{
    [StringLength(120)] public string? Code { get; set; }
    [Required, StringLength(32)] public string Version { get; set; } = "";
}

public sealed class CheckoutPaymentForm
{
    [Required, StringLength(32)] public string Version { get; set; } = "";
}
