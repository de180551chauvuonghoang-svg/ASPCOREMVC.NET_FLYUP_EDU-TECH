using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.DataAccess.Configurations
{
    public class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
    {
        public void Configure(EntityTypeBuilder<QuizAttempt> builder)
        {
            builder.ToTable("QuizAttempts");
            builder.Property(a => a.Score).HasColumnType("decimal(5,2)");
        }
    }
}
