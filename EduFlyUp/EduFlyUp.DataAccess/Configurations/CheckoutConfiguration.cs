using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.DataAccess.Configurations;

public sealed class CheckoutOrderConfiguration : IEntityTypeConfiguration<CheckoutOrder>
{
    public void Configure(EntityTypeBuilder<CheckoutOrder> b)
    {
        b.ToTable("CheckoutOrders", t =>
        {
            t.HasCheckConstraint("CK_CheckoutOrders_Amounts", "[OriginalAmount] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [OriginalAmount] AND [PayableAmount] = [OriginalAmount] - [DiscountAmount]");
            t.HasCheckConstraint("CK_CheckoutOrders_Status", "[Status] IN (0,1,2,3) AND (([Status] = 1 AND [PaidAtUtc] IS NOT NULL) OR ([Status] <> 1 AND [PaidAtUtc] IS NULL))");
            t.HasCheckConstraint("CK_CheckoutOrders_Currency", "[Currency] = 'VND'");
            t.HasCheckConstraint("CK_CheckoutOrders_Keys", "[SubmissionKey] <> '00000000-0000-0000-0000-000000000000' AND [PublicId] <> '00000000-0000-0000-0000-000000000000'");
        });
        b.Property(x => x.BuyerId).HasMaxLength(450).IsRequired();
        b.Property(x => x.BuyerName).HasMaxLength(256).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.OriginalAmount).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.PayableAmount).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.PublicId).IsUnique();
        b.HasIndex(x => new { x.BuyerId, x.SubmissionKey }).IsUnique();
        b.HasIndex(x => new { x.BuyerId, x.CreatedAt });
        b.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.CheckoutOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Transactions).WithOne(x => x.Order).HasForeignKey(x => x.CheckoutOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CheckoutOrderItemConfiguration : IEntityTypeConfiguration<CheckoutOrderItem>
{
    public void Configure(EntityTypeBuilder<CheckoutOrderItem> b)
    {
        b.ToTable("CheckoutOrderItems", t =>
        {
            t.HasCheckConstraint("CK_CheckoutOrderItems_Amounts", "[OriginalAmount] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [OriginalAmount] AND [PayableAmount] = [OriginalAmount] - [DiscountAmount]");
            t.HasCheckConstraint("CK_CheckoutOrderItems_Course", "[CourseId] > 0");
            t.HasCheckConstraint("CK_CheckoutOrderItems_Coupon", "([CouponId] IS NULL AND [CouponCode] IS NULL AND [CouponPercent] IS NULL AND [DiscountAmount] = 0) OR ([CouponId] IS NOT NULL AND [CouponId] > 0 AND [CouponCode] IS NOT NULL AND [CouponPercent] IS NOT NULL AND [CouponPercent] BETWEEN 1 AND 90)");
        });
        b.Property(x => x.CourseTitle).HasMaxLength(200).IsRequired();
        b.Property(x => x.InstructorId).HasMaxLength(450).IsRequired();
        b.Property(x => x.CouponCode).HasMaxLength(40);
        b.Property(x => x.OriginalAmount).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.PayableAmount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.CheckoutOrderId, x.CourseId }).IsUnique();
        // Không cascade từ Course/Coupon/User: phải giữ snapshot khi dữ liệu gốc bị xóa/đổi.
        b.HasIndex(x => x.InstructorId);
    }
}

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions", t =>
        {
            t.HasCheckConstraint("CK_PaymentTransactions_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_PaymentTransactions_Currency", "[Currency] = 'VND'");
            t.HasCheckConstraint("CK_PaymentTransactions_Status", "[Status] IN (0,1,2) AND (([Status] = 1 AND [VerifiedAtUtc] IS NOT NULL) OR ([Status] <> 1 AND [VerifiedAtUtc] IS NULL))");
            t.HasCheckConstraint("CK_PaymentTransactions_Reference", "LEN(LTRIM(RTRIM([Provider]))) > 0 AND LEN(LTRIM(RTRIM([ProviderTransactionId]))) > 0");
        });
        b.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        b.Property(x => x.ProviderTransactionId).HasMaxLength(128).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Provider, x.ProviderTransactionId }).IsUnique();
    }
}
