using EduFlyUp.BusinessObjects;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EduFlyUp.DataAccess
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
        public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();
        public DbSet<CheckoutOrder> CheckoutOrders => Set<CheckoutOrder>();
        public DbSet<CheckoutOrderItem> CheckoutOrderItems => Set<CheckoutOrderItem>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
        public DbSet<CheckoutPaymentIntent> CheckoutPaymentIntents => Set<CheckoutPaymentIntent>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile($"appsettings.{env}.json", optional: true);

                if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
                {
                    configBuilder.AddJsonFile("appsettings.Local.json", optional: true);
                }

                var config = configBuilder.AddEnvironmentVariables().Build();

                var rawConn = Environment.GetEnvironmentVariable("DATABASE_URL")
                    ?? config.GetConnectionString("DefaultConnection")
                    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                    ?? "Server=.\\SQLEXPRESS;Database=EduFlyUpDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

                if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                    rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                    rawConn.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                    rawConn.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
                    rawConn.Contains("Port=5432", StringComparison.OrdinalIgnoreCase))
                {
                    var npgsqlConn = rawConn;
                    if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                        rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                    {
                        var uri = new Uri(rawConn);
                        var userInfo = uri.UserInfo.Split(':');
                        var user = userInfo[0];
                        var pass = userInfo.Length > 1 ? userInfo[1] : "";
                        var db = uri.AbsolutePath.TrimStart('/');
                        npgsqlConn = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={db};Username={user};Password={pass};SSL Mode=Prefer;Trust Server Certificate=true;";
                    }
                    optionsBuilder.UseNpgsql(npgsqlConn);
                }
                else
                {
                    optionsBuilder.UseSqlServer(rawConn);
                }
            }
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

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
