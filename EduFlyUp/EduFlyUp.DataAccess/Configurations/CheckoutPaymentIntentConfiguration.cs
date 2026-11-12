using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.DataAccess.Configurations;

public sealed class CheckoutPaymentIntentConfiguration : IEntityTypeConfiguration<CheckoutPaymentIntent>
{
    public void Configure(EntityTypeBuilder<CheckoutPaymentIntent> b)
    {
        b.ToTable("CheckoutPaymentIntents", t =>
        {
            t.HasCheckConstraint("CK_CheckoutPaymentIntents_Amount", "[Amount] > 0 AND [Amount] <= 9999999999999 AND [Amount] = FLOOR([Amount]) AND [Currency] = 'VND'");
            t.HasCheckConstraint("CK_CheckoutPaymentIntents_Reference", "LEN([Reference]) = 22 AND LEFT([Reference],2) = 'FU' AND [Reference] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z0-9]%'");
        });
        b.Property(x => x.Reference).HasMaxLength(22).IsRequired();
        b.Property(x => x.BankBin).HasMaxLength(6).IsRequired();
        b.Property(x => x.AccountNumber).HasMaxLength(19).IsRequired();
        b.Property(x => x.AccountName).HasMaxLength(50).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => x.CheckoutOrderId).IsUnique();
        b.HasOne(x => x.Order).WithOne().HasForeignKey<CheckoutPaymentIntent>(x => x.CheckoutOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
