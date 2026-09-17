using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        // Constructor nhận DbContextOptions — được inject qua DI
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSet = "bảng" trong database
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tự động load tất cả IEntityTypeConfiguration trong assembly này
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        // Override SaveChangesAsync để tự động set UpdatedAt
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
