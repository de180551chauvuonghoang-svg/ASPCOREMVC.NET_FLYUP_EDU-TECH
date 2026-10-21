using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.DataAccess.Configurations
{
    public class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
    {
        public void Configure(EntityTypeBuilder<PayoutRequest> builder)
        {
            builder.ToTable("PayoutRequests");
            builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        }
    }
}
