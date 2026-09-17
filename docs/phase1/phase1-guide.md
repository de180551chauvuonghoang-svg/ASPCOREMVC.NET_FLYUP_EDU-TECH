# 📦 Phase 1 — Nền tảng & Cấu trúc Project

> **Thời gian**: Tuần 1–2
> **Mục tiêu**: Hiểu Clean Architecture, tạo Solution đúng chuẩn, setup EF Core + SQL Server, Repository Pattern
> **Công cụ**: Visual Studio 2022, .NET 8, SQL Server

---

## 🗂️ Mục lục

1. [Task 1.1 — Tạo Solution Structure](#task-11--tạo-solution-structure)
2. [Task 1.2 — Thiết lập Project References](#task-12--thiết-lập-project-references)
3. [Task 1.3 — Domain Layer: Entities &amp; Enums](#task-13--domain-layer-entities--enums)
4. [Task 1.4 — Domain Layer: Interfaces](#task-14--domain-layer-interfaces)
5. [Task 1.5 — Infrastructure: EF Core + DbContext](#task-15--infrastructure-ef-core--dbcontext)
6. [Task 1.6 — Infrastructure: Repository Pattern](#task-16--infrastructure-repository-pattern)
7. [Task 1.7 — Infrastructure: Data Seeder](#task-17--infrastructure-data-seeder)
8. [Task 1.8 — Web: Đăng ký DI + Chạy Migration](#task-18--web-đăng-ký-di--chạy-migration)
9. [Checklist tổng kết Phase 1](#checklist-tổng-kết-phase-1)

---

## Task 1.1 — Tạo Solution Structure

### 🧠 Lý thuyết: Tại sao cần nhiều project?

Trong .NET, một **Solution** (`.sln`) chứa nhiều **Project** (`.csproj`).Chia nhiều project giúp:

- **Enforce kiến trúc**: Compiler báo lỗi nếu bạn vi phạm dependency rule
- **Tách biệt trách nhiệm**: Mỗi project làm đúng 1 việc
- **Dễ test**: Test project chỉ cần reference Application, không cần kéo theo Web

### 📋 Hướng dẫn thực hành

**Bước 1**: Mở Visual Studio 2022 → `File` → `New` → `Project`
→ Tìm **"Blank Solution"** → Đặt tên `EduHub` → Location: thư mục `baitapprn`

**Bước 2**: Tạo 5 project theo thứ tự sau.
Chuột phải vào Solution → `Add` → `New Project`:

| # | Tên Project              | Template cần chọn                               | Thư mục đặt               |
| - | ------------------------- | ------------------------------------------------- | ----------------------------- |
| 1 | `EduHub.Domain`         | Class Library*(net8.0)*                         | `src/EduHub.Domain`         |
| 2 | `EduHub.Application`    | Class Library*(net8.0)*                         | `src/EduHub.Application`    |
| 3 | `EduHub.Infrastructure` | Class Library*(net8.0)*                         | `src/EduHub.Infrastructure` |
| 4 | `EduHub.Web`            | **ASP.NET Core Web App (MVC)** *(net8.0)* | `src/EduHub.Web`            |
| 5 | `EduHub.UnitTests`      | xUnit Test Project*(net8.0)*                    | `tests/EduHub.UnitTests`    |

> ⚠️ Khi tạo `EduHub.Web`: chọn đúng **Model-View-Controller**, bỏ tick **"Configure for HTTPS"** tạm thời để đơn giản hơn khi dev.

**Bước 3**: Xóa file mặc định không cần thiết:

- `EduHub.Domain` → xóa `Class1.cs`
- `EduHub.Application` → xóa `Class1.cs`
- `EduHub.Infrastructure` → xóa `Class1.cs`

**✅ Kết quả mong đợi**: Solution Explorer trông như thế này:

```
EduHub (Solution)
├── src
│   ├── EduHub.Domain
│   ├── EduHub.Application
│   ├── EduHub.Infrastructure
│   └── EduHub.Web
└── tests
    └── EduHub.UnitTests
```

---

## Task 1.2 — Thiết lập Project References

### 🧠 Lý thuyết: Dependency Rule

Đây là **linh hồn** của Clean Architecture. Compiler sẽ enforce luật này cho bạn.

```
        ┌──────────┐
        │  Domain  │  ← Không reference ai cả
        └────▲─────┘
             │
        ┌────┴─────────┐
        │  Application │  ← Chỉ reference Domain
        └────▲──────▲──┘
             │      │
   ┌─────────┴─┐  ┌─┴────────┐
   │   Infra   │  │   Web    │  ← reference Application
   └───────────┘  └──────────┘
                       │
                  ┌────┴──────┐
                  │ UnitTests │  ← reference Application
                  └───────────┘
```

### 📋 Hướng dẫn thực hành

Chuột phải vào project → `Add` → `Project Reference` → tick project cần reference → OK

| Project                   | Cần reference tới                                     |
| ------------------------- | ------------------------------------------------------- |
| `EduHub.Application`    | ✅`EduHub.Domain`                                     |
| `EduHub.Infrastructure` | ✅`EduHub.Application`                                |
| `EduHub.Web`            | ✅`EduHub.Application` + ✅ `EduHub.Infrastructure` |
| `EduHub.UnitTests`      | ✅`EduHub.Application`                                |

> ❌ **TUYỆT ĐỐI KHÔNG** để `EduHub.Domain` reference project nào khác
> ❌ **KHÔNG** để `EduHub.Application` reference `EduHub.Infrastructure`

**✅ Verify**: Build toàn bộ solution bằng `Ctrl + Shift + B` — phải thành công (0 errors)

---

## Task 1.3 — Domain Layer: Entities & Enums

### 🧠 Lý thuyết: Entity là gì?

```
Entity   = Đối tượng có Identity (ID riêng) — VD: Course có Id = 5
DTO      = Đối tượng chuyển dữ liệu, không có hành vi — VD: CourseDto
Value Object = Đối tượng so sánh bằng giá trị, không có ID — VD: Money, Address
```

**Domain layer không được có bất kỳ NuGet package nào** (ngoại lệ: một số annotation thuần túy).
Đây là C# thuần túy.

### 📋 Cấu trúc thư mục cần tạo trong `EduHub.Domain`

```
EduHub.Domain/
├── Entities/
│   ├── BaseEntity.cs
│   ├── Course.cs
│   ├── Lesson.cs
│   └── Enrollment.cs
└── Enums/
    ├── CourseLevel.cs
    └── EnrollmentStatus.cs
```

### 📝 Code mẫu — `BaseEntity.cs`

```csharp
namespace EduHub.Domain.Entities;

// Abstract: không thể tạo instance trực tiếp, chỉ để kế thừa
// Mọi Entity đều có Id, CreatedAt, UpdatedAt — viết 1 lần ở đây
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }  // ? = nullable, có thể null
}
```

### 📝 Code mẫu — `CourseLevel.cs`

```csharp
namespace EduHub.Domain.Enums;

public enum CourseLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3
}
```

### 📝 Code mẫu — `Course.cs`

```csharp
using EduHub.Domain.Enums;

namespace EduHub.Domain.Entities;

public class Course : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;           // Computed property, không lưu DB
    public CourseLevel Level { get; set; }
    public string InstructorId { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = false;

    // Navigation Properties — EF Core dùng để JOIN bảng
    // "virtual" cho phép EF Core Lazy Loading (tải dữ liệu khi cần)
    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
```

### 🎯 Bài tập — Tự viết các file sau

**`EnrollmentStatus.cs`** (enum):

- Các giá trị: `Active`, `Completed`, `Cancelled`

**`Lesson.cs`** (entity kế thừa BaseEntity):

- Properties cần có: `Title`, `Content`, `VideoUrl`, `DurationMinutes` (int), `Order` (int — thứ tự bài học), `IsPreview` (bool — bài học xem thử miễn phí không?)
- Foreign key: `CourseId` (int)
- Navigation property: `Course` (ngược lại về Course)

**`Enrollment.cs`** (entity kế thừa BaseEntity):

- Properties: `UserId` (string — dùng string vì ASP.NET Identity dùng GUID), `CourseId` (int), `EnrolledAt` (DateTime), `CompletedAt` (DateTime? — nullable), `Status` (EnrollmentStatus)
- Navigation properties: `Course`

> 💡 **Tip**: Dùng `Ctrl + Shift + N` trong VS2022 để tạo file mới, hoặc chuột phải vào folder → `Add` → `Class`

---

## Task 1.4 — Domain Layer: Interfaces

### 🧠 Lý thuyết: Interface & Dependency Inversion

**Vấn đề**: Application cần lấy dữ liệu từ database. Nhưng Application layer không được phép biết về EF Core hay SQL Server (vi phạm Dependency Rule).

**Giải pháp**: **Interface** (hợp đồng).

```
Application định nghĩa "HỢP ĐỒNG": IRepository
Infrastructure "KÝ HỢP ĐỒNG": class Repository : IRepository

→ Application chỉ biết hợp đồng, không biết cách thực hiện
→ Có thể swap Database mà không cần sửa Application
```

**Dependency Inversion Principle (DIP)**: High-level modules (Application) không phụ thuộc vào low-level modules (Infrastructure). Cả hai phụ thuộc vào abstractions (Interface).

### 📋 Cấu trúc thư mục cần tạo trong `EduHub.Domain`

```
EduHub.Domain/
└── Interfaces/
    ├── IRepository.cs       ← Generic repository (dùng được cho mọi entity)
    ├── ICourseRepository.cs ← Specific repository (chỉ cho Course)
    └── IUnitOfWork.cs       ← Quản lý transaction
```

### 📝 Code mẫu — `IRepository.cs`

```csharp
using System.Linq.Expressions;

namespace EduHub.Domain.Interfaces;

// T phải là class và kế thừa BaseEntity
// Generic interface: dùng cho Course, Lesson, Enrollment đều được
public interface IRepository<T> where T : class
{
    // Trả về tất cả records (IQueryable cho phép thêm .Where(), .OrderBy() sau)
    IQueryable<T> GetAll();

    // Tìm theo Id — async vì I/O operation
    Task<T?> GetByIdAsync(int id);

    // Tìm theo điều kiện — Expression<Func<T, bool>> là lambda predicate
    // VD: GetAsync(c => c.IsPublished == true)
    Task<T?> GetAsync(Expression<Func<T, bool>> predicate);

    // Thêm mới
    Task AddAsync(T entity);

    // Cập nhật
    void Update(T entity);

    // Xóa
    void Delete(T entity);

    // Kiểm tra tồn tại
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
}
```

### 📝 Code mẫu — `ICourseRepository.cs`

```csharp
using EduHub.Domain.Entities;

namespace EduHub.Domain.Interfaces;

// Kế thừa IRepository<Course> → có sẵn tất cả methods của IRepository
// Thêm các methods đặc thù chỉ Course mới cần
public interface ICourseRepository : IRepository<Course>
{
    // Lấy khóa học kèm theo danh sách bài học (Eager Loading)
    Task<Course?> GetCourseWithLessonsAsync(int courseId);

    // Lấy danh sách khóa học theo giảng viên
    Task<IEnumerable<Course>> GetByInstructorAsync(string instructorId);

    // Lấy khóa học đã published (công khai)
    Task<IEnumerable<Course>> GetPublishedCoursesAsync();
}
```

### 📝 Code mẫu — `IUnitOfWork.cs`

```csharp
namespace EduHub.Domain.Interfaces;

// Unit of Work: gom nhiều thao tác DB vào 1 transaction
// VD: Tạo Enrollment + trừ số slot còn lại → phải commit cùng lúc
// Nếu 1 cái lỗi → rollback toàn bộ
public interface IUnitOfWork : IDisposable
{
    ICourseRepository Courses { get; }
    IRepository<Lesson> Lessons { get; }
    IRepository<Enrollment> Enrollments { get; }

    // Lưu tất cả thay đổi vào DB — trả về số records bị ảnh hưởng
    Task<int> SaveChangesAsync();
}
```

> 💡 `IDisposable` giúp `IUnitOfWork` dọn dẹp tài nguyên (database connection) khi dùng xong — gọi qua `using` statement.

---

## Task 1.5 — Infrastructure: EF Core + DbContext

### 🧠 Lý thuyết: Entity Framework Core

**EF Core** là ORM (Object-Relational Mapper) — giúp bạn làm việc với database bằng C# thay vì SQL thô.

```
C# Code                         SQL được tạo tự động
─────────────────────────────   ──────────────────────────────────
dbContext.Courses.ToList()   →  SELECT * FROM Courses
dbContext.Courses.Add(course)→  INSERT INTO Courses (...)
course.Title = "New Title"   →  UPDATE Courses SET Title = 'New Title'
dbContext.Courses.Remove(c)  →  DELETE FROM Courses WHERE Id = ...
```

**DbContext** là trung tâm của EF Core: quản lý kết nối, tracking thay đổi, tạo migration.

### 📋 Cài NuGet packages cho `EduHub.Infrastructure`

Chuột phải vào `EduHub.Infrastructure` → `Manage NuGet Packages`:

| Package                                     | Phiên bản | Mục đích             |
| ------------------------------------------- | ----------- | ----------------------- |
| `Microsoft.EntityFrameworkCore`           | 8.x         | EF Core core            |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.x         | Provider cho SQL Server |
| `Microsoft.EntityFrameworkCore.Tools`     | 8.x         | Tạo Migration qua CLI  |

Cài thêm cho `EduHub.Web`:

| Package                                  | Phiên bản | Mục đích                             |
| ---------------------------------------- | ----------- | --------------------------------------- |
| `Microsoft.EntityFrameworkCore.Design` | 8.x         | Hỗ trợ tạo Migration từ Web project |

### 📋 Cấu trúc thư mục trong `EduHub.Infrastructure`

```
EduHub.Infrastructure/
└── Data/
    ├── AppDbContext.cs
    ├── Configurations/
    │   ├── CourseConfiguration.cs
    │   └── LessonConfiguration.cs
    └── Seed/
        └── DataSeeder.cs
```

### 📝 Code mẫu — `AppDbContext.cs`

```csharp
using EduHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduHub.Infrastructure.Data;

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
```

### 📝 Code mẫu — `CourseConfiguration.cs`

```csharp
using EduHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduHub.Infrastructure.Data.Configurations;

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
```

### 🎯 Bài tập — Tự viết `LessonConfiguration.cs`

Cấu hình cho bảng `Lessons`:

- `Title`: required, max 300 ký tự
- `VideoUrl`: max 500 ký tự
- `DurationMinutes`: default value = 0
- Quan hệ ngược lại với Course đã được định nghĩa ở `CourseConfiguration`

---

## Task 1.6 — Infrastructure: Repository Pattern

### 🧠 Lý thuyết: Repository Pattern

```
Không có Repository:          Có Repository:
─────────────────────         ─────────────────────
Controller → DbContext        Controller → IRepository
               ↓                              ↓
            SQL Server         (không quan tâm dưới là gì)
                                              ↓
                                         DbContext → SQL Server
```

**Lợi ích**:

- Unit test: mock `IRepository` mà không cần database
- Tập trung query phức tạp vào 1 nơi
- Swap database dễ dàng

### 📋 Cấu trúc thư mục trong `EduHub.Infrastructure`

```
EduHub.Infrastructure/
└── Repositories/
    ├── BaseRepository.cs      ← Implement IRepository<T> generic
    ├── CourseRepository.cs    ← Implement ICourseRepository
    └── UnitOfWork.cs          ← Implement IUnitOfWork
```

### 📝 Code mẫu — `BaseRepository.cs`

```csharp
using System.Linq.Expressions;
using EduHub.Domain.Interfaces;
using EduHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EduHub.Infrastructure.Repositories;

public class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public BaseRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public IQueryable<T> GetAll() => _dbSet.AsQueryable();

    public async Task<T?> GetByIdAsync(int id) 
        => await _dbSet.FindAsync(id);

    public async Task<T?> GetAsync(Expression<Func<T, bool>> predicate)
        => await _dbSet.FirstOrDefaultAsync(predicate);

    public async Task AddAsync(T entity) 
        => await _dbSet.AddAsync(entity);

    public void Update(T entity) 
        => _dbSet.Update(entity);

    public void Delete(T entity) 
        => _dbSet.Remove(entity);

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        => await _dbSet.AnyAsync(predicate);
}
```

### 📝 Code mẫu — `CourseRepository.cs`

```csharp
using EduHub.Domain.Entities;
using EduHub.Domain.Interfaces;
using EduHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EduHub.Infrastructure.Repositories;

public class CourseRepository : BaseRepository<Course>, ICourseRepository
{
    public CourseRepository(AppDbContext context) : base(context) { }

    public async Task<Course?> GetCourseWithLessonsAsync(int courseId)
        => await _context.Courses
            .Include(c => c.Lessons.OrderBy(l => l.Order)) // Eager loading + sort
            .FirstOrDefaultAsync(c => c.Id == courseId);

    public async Task<IEnumerable<Course>> GetByInstructorAsync(string instructorId)
        => await _context.Courses
            .Where(c => c.InstructorId == instructorId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Course>> GetPublishedCoursesAsync()
        => await _context.Courses
            .Where(c => c.IsPublished)
            .Include(c => c.Lessons)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
}
```

### 📝 Code mẫu — `UnitOfWork.cs`

```csharp
using EduHub.Domain.Entities;
using EduHub.Domain.Interfaces;
using EduHub.Infrastructure.Data;

namespace EduHub.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    // Lazy init: chỉ tạo Repository khi cần dùng lần đầu
    private ICourseRepository? _courses;
    private IRepository<Lesson>? _lessons;
    private IRepository<Enrollment>? _enrollments;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public ICourseRepository Courses
        => _courses ??= new CourseRepository(_context);

    public IRepository<Lesson> Lessons
        => _lessons ??= new BaseRepository<Lesson>(_context);

    public IRepository<Enrollment> Enrollments
        => _enrollments ??= new BaseRepository<Enrollment>(_context);

    public async Task<int> SaveChangesAsync()
        => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
```

---

## Task 1.7 — Infrastructure: Data Seeder

### 🧠 Lý thuyết: Seed Data

Seed Data = dữ liệu mẫu được tạo sẵn khi khởi động app lần đầu.
Giúp bạn test mà không cần nhập tay từng record.

### 📝 Code mẫu — `DataSeeder.cs`

```csharp
using EduHub.Domain.Entities;
using EduHub.Domain.Enums;
using EduHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EduHub.Infrastructure.Data.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // Chạy migration trước khi seed (an toàn khi deploy)
        await context.Database.MigrateAsync();

        // Chỉ seed nếu chưa có dữ liệu (tránh trùng lặp)
        if (await context.Courses.AnyAsync()) return;

        var courses = new List<Course>
        {
            new Course
            {
                Title = "Lập trình C# từ cơ bản đến nâng cao",
                Description = "Khóa học toàn diện về C# cho người mới bắt đầu",
                Price = 299000,
                Level = CourseLevel.Beginner,
                IsPublished = true,
                InstructorId = "seed-instructor-1",
                Lessons = new List<Lesson>
                {
                    new Lesson { Title = "Giới thiệu C#", Order = 1, DurationMinutes = 15, IsPreview = true },
                    new Lesson { Title = "Biến và kiểu dữ liệu", Order = 2, DurationMinutes = 25 },
                    new Lesson { Title = "Vòng lặp và điều kiện", Order = 3, DurationMinutes = 30 },
                }
            },
            new Course
            {
                Title = "ASP.NET Core Web API",
                Description = "Xây dựng RESTful API chuyên nghiệp với ASP.NET Core",
                Price = 499000,
                Level = CourseLevel.Intermediate,
                IsPublished = true,
                InstructorId = "seed-instructor-1",
            }
        };

        await context.Courses.AddRangeAsync(courses);
        await context.SaveChangesAsync();
    }
}
```

---

## Task 1.8 — Web: Đăng ký DI + Chạy Migration

### 🧠 Lý thuyết: Dependency Injection Container

ASP.NET Core có sẵn DI Container. Bạn **đăng ký** service một lần trong `Program.cs`, sau đó **inject** (tiêm) vào bất kỳ đâu qua constructor.

```
3 loại lifetime:
─────────────────────────────────────────────────────
Singleton   → Tạo 1 lần, dùng suốt đời app         (VD: config, cache)
Scoped      → Tạo mới mỗi HTTP request              (VD: DbContext, UnitOfWork)
Transient   → Tạo mới mỗi lần được inject           (VD: lightweight service)
```

### 📝 Cấu hình `appsettings.json` trong `EduHub.Web`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EduHubDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

> 💡 Nếu bạn dùng SQL Server đầy đủ (không phải LocalDB), thay connection string thành:
> `"Server=.;Database=EduHubDb;Trusted_Connection=True;TrustServerCertificate=True"`

### 📝 Tạo Extension Method — `InfrastructureServiceExtensions.cs` trong `EduHub.Infrastructure`

```csharp
using EduHub.Domain.Interfaces;
using EduHub.Infrastructure.Data;
using EduHub.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EduHub.Infrastructure;

// Extension method giúp Program.cs gọn hơn: services.AddInfrastructure(config)
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Đăng ký DbContext với SQL Server
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("EduHub.Infrastructure") // Migration nằm ở Infra
            ));

        // Đăng ký Repository và UnitOfWork
        // Scoped: mỗi HTTP request có 1 instance riêng
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICourseRepository, CourseRepository>();

        return services;
    }
}
```

### 📝 Cập nhật `Program.cs` trong `EduHub.Web`

```csharp
using EduHub.Infrastructure;
using EduHub.Infrastructure.Data;
using EduHub.Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

// ──────────────── Đăng ký Services ────────────────
builder.Services.AddControllersWithViews();

// Gọi extension method — đăng ký DbContext + Repositories
builder.Services.AddInfrastructure(builder.Configuration);

// ──────────────── Build App ────────────────
var app = builder.Build();

// ──────────────── Seed Data khi khởi động ────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DataSeeder.SeedAsync(context);
}

// ──────────────── Middleware Pipeline ────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### 📋 Tạo Migration và cập nhật Database

Mở **Package Manager Console** (Tools → NuGet Package Manager → Package Manager Console):

```powershell
# Bước 1: Chọn Default project là EduHub.Infrastructure
# Bước 2: Chạy lệnh tạo migration đầu tiên
Add-Migration InitialCreate -Project EduHub.Infrastructure -StartupProject EduHub.Web

# Bước 3: Áp dụng migration lên SQL Server
Update-Database -Project EduHub.Infrastructure -StartupProject EduHub.Web
```

> 💡 Sau khi chạy xong, kiểm tra SQL Server Management Studio — database `EduHubDb` đã được tạo với các bảng `Courses`, `Lessons`, `Enrollments`.

---

## ✅ Checklist tổng kết Phase 1

### Task 1.1 — Solution Structure

- [ ] Tạo Blank Solution `EduHub`
- [ ] Tạo đủ 5 projects đúng template và location
- [ ] Xóa các `Class1.cs` mặc định

### Task 1.2 — Project References

- [ ] `Application` → `Domain`
- [ ] `Infrastructure` → `Application`
- [ ] `Web` → `Application` + `Infrastructure`
- [ ] `UnitTests` → `Application`
- [ ] Build solution thành công (0 errors)

### Task 1.3 — Domain Entities

- [ ] `BaseEntity.cs`
- [ ] `Course.cs`
- [ ] `Lesson.cs` *(tự viết)*
- [ ] `Enrollment.cs` *(tự viết)*
- [ ] `CourseLevel.cs` (enum)
- [ ] `EnrollmentStatus.cs` (enum) *(tự viết)*

### Task 1.4 — Domain Interfaces

- [ ] `IRepository.cs`
- [ ] `ICourseRepository.cs`
- [ ] `IUnitOfWork.cs`

### Task 1.5 — EF Core Setup

- [ ] Cài NuGet packages cho `Infrastructure` và `Web`
- [ ] `AppDbContext.cs`
- [ ] `CourseConfiguration.cs`
- [ ] `LessonConfiguration.cs` *(tự viết)*

### Task 1.6 — Repository Pattern

- [ ] `BaseRepository.cs`
- [ ] `CourseRepository.cs`
- [ ] `UnitOfWork.cs`

### Task 1.7 — Data Seeder

- [ ] `DataSeeder.cs` với ít nhất 2 courses, 3 lessons

### Task 1.8 — DI & Migration

- [ ] Cấu hình connection string trong `appsettings.json`
- [ ] `InfrastructureServiceExtensions.cs`
- [ ] Cập nhật `Program.cs`
- [ ] Chạy `Add-Migration InitialCreate` thành công
- [ ] Chạy `Update-Database` thành công
- [ ] Kiểm tra database đã được tạo trong SQL Server
- [ ] Chạy app (`F5`) — không có lỗi, dữ liệu seed thành công

---

## 🔑 Kiến thức quan trọng cần nhớ sau Phase 1

| Khái niệm                  | Giải thích ngắn                                            |
| ---------------------------- | ------------------------------------------------------------- |
| **Clean Architecture** | Dependencies chỉ trỏ vào trong — Domain là trung tâm    |
| **Entity**             | Object có ID riêng biệt, map 1-1 với bảng DB             |
| **IRepository\<T\>**   | Hợp đồng truy cập data — Application không biết về DB |
| **IUnitOfWork**        | Gom nhiều thao tác vào 1 transaction                       |
| **DbContext**          | Trung tâm EF Core — quản lý kết nối và tracking        |
| **Migration**          | Lịch sử thay đổi schema DB — có thể rollback           |
| **DI Container**       | Quản lý vòng đời objects — Singleton/Scoped/Transient   |
| **Extension Method**   | `AddInfrastructure()` — giữ `Program.cs` gọn gàng     |

---

> 📌 **Bước tiếp theo**: Khi hoàn thành Phase 1, báo mentor để review và bắt đầu **Phase 2 — ASP.NET Core MVC & Razor Pages**
