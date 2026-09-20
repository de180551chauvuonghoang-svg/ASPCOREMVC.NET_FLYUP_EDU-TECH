using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.Infrastructure.Data.Configurations;

/// <summary>
/// Cấu hình mapping giữa Entity Enrollment và bảng Enrollments trong database.
/// Tách biệt khỏi Domain để không làm "bẩn" Entity class bằng EF Core attributes.
/// </summary>
public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        // ── Tên bảng ───────────────────────────────────────────────
        builder.ToTable("Enrollments");

        // ── Primary Key ────────────────────────────────────────────
        builder.HasKey(e => e.Id);

        // ── Cấu hình columns ───────────────────────────────────────

        // UserId dùng string (GUID của ASP.NET Identity)
        // MaxLength(450) là chuẩn khớp với IdentityUser.Id
        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(450);

        // CourseId bắt buộc
        builder.Property(e => e.CourseId)
            .IsRequired();

        // EnrolledAt bắt buộc — thời điểm ghi danh
        builder.Property(e => e.EnrolledAt)
            .IsRequired();

        // CompletedAt nullable — null nghĩa là học viên chưa hoàn thành
        builder.Property(e => e.CompletedAt)
            .IsRequired(false);

        // Status lưu dạng int (enum value) vào DB
        builder.Property(e => e.Status)
            .IsRequired();

        // ── Quan hệ N-1 với Course ─────────────────────────────────
        // Dùng Restrict thay vì Cascade:
        //   - Cascade: xóa Course → tự động xóa toàn bộ Enrollment
        //   - Restrict: KHÔNG cho xóa Course khi còn Enrollment tồn tại
        //   → Bảo toàn lịch sử ghi danh, tránh mất dữ liệu quan trọng
        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Index ───────────────────────────────────────────────────
        // Tăng tốc query: tìm enrollment theo UserId (trang "Khóa học của tôi")
        builder.HasIndex(e => e.UserId);

        // Ngăn 1 User ghi danh cùng 1 Course 2 lần (unique constraint)
        builder.HasIndex(e => new { e.UserId, e.CourseId })
            .IsUnique();
    }
}
