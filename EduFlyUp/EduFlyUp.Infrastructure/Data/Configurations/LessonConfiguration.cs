using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình Fluent API cho thực thể Lesson (Bảng Lessons trong Database)
/// </summary>
public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        // 1. Đặt tên bảng
        builder.ToTable("Lessons");

        // 2. Primary Key
        builder.HasKey(l => l.Id);

        // 3. Cấu hình các thuộc tính (Columns)
        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(l => l.Content)
            .HasColumnType("nvarchar(max)"); // Nội dung bài học có thể dài

        builder.Property(l => l.VideoUrl)
            .HasMaxLength(500);

        builder.Property(l => l.DurationMinutes)
            .HasDefaultValue(0);

        builder.Property(l => l.Order)
            .HasDefaultValue(1);

        builder.Property(l => l.IsPreview)
            .HasDefaultValue(false);

        // 4. Cấu hình quan hệ (N - 1 với Course): Nhiều Lesson thuộc 1 Course
        builder.HasOne(l => l.Course)
            .WithMany(c => c.Lessons)
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Cascade); // Xóa Course thì tự động xóa hết Lessons của Course đó
    }
}
