using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.DataAccess.Configurations
{
    public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
    {
        public void Configure(EntityTypeBuilder<Certificate> builder)
        {
            builder.ToTable("Certificates");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.CertificateCode)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(c => c.CertificateCode)
                .IsUnique();

            builder.Property(c => c.StudentName)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(c => c.CourseTitle)
                .IsRequired()
                .HasMaxLength(500);

            builder.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(c => c.Course)
                .WithMany()
                .HasForeignKey(c => c.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
