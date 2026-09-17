using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.Infrastructure.Data.Configurations
{
    // IEntityTypeConfiguration: cấu hình mapping giữa Entity và Table
    // Tốt hơn dùng [Attribute] vì: tách biệt, không làm bẩn Domain layer
    public class CourseConfiguration : IEntityTypeConfiguration<Course>
    {
        public void Configure(EntityTypeBuilder<Course> builder)
        {
            // Tên bảng
            builder.ToTable("Courses");

            // Primary Key (EF Core tự nhận "Id" nhưng khai báo rõ cho chắc)
            builder.HasKey(c => c.Id);

            // Cấu hình từng column
            builder.Property(c => c.Title)
                .IsRequired()           // NOT NULL
                .HasMaxLength(200);     // VARCHAR(200)

            builder.Property(c => c.Description)
                .HasMaxLength(2000);

            builder.Property(c => c.Price)
                .HasColumnType("decimal(18,2)");  // Đơn vị tiền tệ cần precision

            // Cấu hình quan hệ: 1 Course có nhiều Lesson
            builder.HasMany(c => c.Lessons)
                .WithOne(l => l.Course)
                .HasForeignKey(l => l.CourseId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Course → xóa luôn Lessons

            // Ignore computed property — không lưu vào DB
            builder.Ignore(c => c.IsFree);
        }
    }
}
