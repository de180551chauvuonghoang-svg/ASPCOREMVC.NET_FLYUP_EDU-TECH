using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.DataAccess.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(o => o.OrderCode)
            .IsUnique();

        builder.Property(o => o.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(o => o.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(o => o.PaymentMethod)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.TransactionReference)
            .HasMaxLength(100);

        builder.HasOne(o => o.Course)
            .WithMany()
            .HasForeignKey(o => o.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
