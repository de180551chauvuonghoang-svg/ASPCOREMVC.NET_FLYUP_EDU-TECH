using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.DataAccess.Configurations;

public sealed class SePayReceiptConfiguration : IEntityTypeConfiguration<SePayReceipt>
{
    public void Configure(EntityTypeBuilder<SePayReceipt> builder)
    {
        builder.ToTable("SePayReceipts");
        builder.HasKey(receipt => receipt.TransactionId);
        builder.Property(receipt => receipt.TransactionId).ValueGeneratedNever();
        builder.HasIndex(receipt => receipt.OrderId).IsUnique();
        builder.HasIndex(receipt => receipt.InstructorId);
        builder.Property(receipt => receipt.CourseTitle).HasMaxLength(200);
        builder.Property(receipt => receipt.InstructorId).HasMaxLength(450);
        builder.Property(receipt => receipt.Amount).HasPrecision(18, 2);
    }
}
