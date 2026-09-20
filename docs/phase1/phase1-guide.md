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
→ Tìm **"Blank Solution"** → Đặt tên `EduFlyUp` → Location: thư mục `baitapprn`

**Bước 2**: Tạo 5 project theo thứ tự sau.
Chuột phải vào Solution → `Add` → `New Project`:

| # | Tên Project | Template cần chọn | Thư mục đặt |
| - | ----------------------------- | ------------------------------------------------- | --------------------------------- |
| 1 | `EduFlyUp.Domain` | Class Library*(net8.0)* | `src/EduFlyUp.Domain` |
| 2 | `EduFlyUp.Application` | Class Library*(net8.0)* | `src/EduFlyUp.Application` |
| 3 | `EduFlyUp.Infrastructure` | Class Library*(net8.0)* | `src/EduFlyUp.Infrastructure` |
| 4 | `EduFlyUp.Web` | **ASP.NET Core Web App (MVC)** *(net8.0)* | `src/EduFlyUp.Web` |
| 5 | `EduFlyUp.UnitTests` | xUnit Test Project*(net8.0)* | `tests/EduFlyUp.UnitTests` |

> ⚠️ Khi tạo `EduFlyUp.Web`: chọn đúng **Model-View-Controller**, bỏ tick **"Configure for HTTPS"** tạm thời để đơn giản hơn khi dev.

**Bước 3**: Xóa file mặc định không cần thiết:

- `EduFlyUp.Domain` → xóa `Class1.cs`
- `EduFlyUp.Application` → xóa `Class1.cs`
- `EduFlyUp.Infrastructure` → xóa `Class1.cs`

**✅ Kết quả mong đợi**: Solution Explorer trông như thế này:

```
EduFlyUp (Solution)
├── src
│   ├── EduFlyUp.Domain
│   ├── EduFlyUp.Application
│   ├── EduFlyUp.Infrastructure
│   └── EduFlyUp.Web
└── tests
    └── EduFlyUp.UnitTests
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

| Project | Cần reference tới |
| ----------------------------- | --------------------------------------------------------------- |
| `EduFlyUp.Application` | ✅ `EduFlyUp.Domain` |
| `EduFlyUp.Infrastructure` | ✅ `EduFlyUp.Application` |
| `EduFlyUp.Web` | ✅ `EduFlyUp.Application` + ✅ `EduFlyUp.Infrastructure` |
| `EduFlyUp.UnitTests` | ✅ `EduFlyUp.Application` |

> ❌ **TUYỆT ĐỐI KHÔNG** để `EduFlyUp.Domain` reference project nào khác
> ❌ **KHÔNG** để `EduFlyUp.Application` reference `EduFlyUp.Infrastructure`

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

### 📋 Cấu trúc thư mục cần tạo trong `EduFlyUp.Domain`

```
EduFlyUp.Domain/
├── Entities/
│   ├── BaseEntity.cs
│   ├── Course.cs
│   ├── Lesson.cs
│   └── Enrollment.cs
│   # ❌ KHÔNG đặt ApplicationUser.cs ở đây!
│   # ApplicationUser kế thừa IdentityUser (thuộc Infrastructure)
└── Enums/
    ├── CourseLevel.cs
    └── EnrollmentStatus.cs
```

> ❗ **Lưu ý quan trọng về `ApplicationUser`**: Nhiều developer hay đặt nhầm `ApplicationUser.cs` vào `Domain/Entities/` vì nó là entity của người dùng. Tuy nhiên `ApplicationUser` kế thừa `IdentityUser` — một class của `Microsoft.AspNetCore.Identity`, là framework concern. Domain layer **không được phép** phụ thuộc vào bất kỳ framework bên ngoài nào. File này sẽ được tạo ở `Infrastructure/Identity/` trong Task 1.5.

### 📝 Code mẫu — `BaseEntity.cs`

```csharp
namespace EduFlyUp.Domain.Entities;

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
namespace EduFlyUp.Domain.Enums;

public enum CourseLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3
}
```

### 📝 Code mẫu — `Course.cs`

```csharp
using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Domain.Entities;

public class Course : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Computed property — tính toán từ Price, KHÔNG lưu vào DB
    // Phải khai báo builder.Ignore(c => c.IsFree) trong CourseConfiguration.cs
    public bool IsFree => Price == 0;

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

### 📋 Cấu trúc thư mục cần tạo trong `EduFlyUp.Domain`

```
EduFlyUp.Domain/
└── Interfaces/
    ├── IRepository.cs       ← Generic repository (dùng được cho mọi entity)
    ├── ICourseRepository.cs ← Specific repository (chỉ cho Course)
    ├── IUnitOfWork.cs       ← Quản lý transaction
    └── ICurrentUser.cs      ← Trừu tượng hóa user hiện tại (không phụ thuộc Identity)
```

### 📝 Code mẫu — `IRepository.cs`

```csharp
using System.Linq.Expressions;

namespace EduFlyUp.Domain.Interfaces;

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
using EduFlyUp.Domain.Entities;

namespace EduFlyUp.Domain.Interfaces;

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
using EduFlyUp.Domain.Entities;

namespace EduFlyUp.Domain.Interfaces;

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

### 📝 Code mẫu — `ICurrentUser.cs`

```csharp
namespace EduFlyUp.Domain.Interfaces;

// Interface trừu tượng hóa "người dùng hiện tại"
// Mục đích: Application layer có thể biết UserId mà không cần biết về Identity/HttpContext
// → Tuân thủ Dependency Inversion: Domain định nghĩa hợp đồng,
//   Infrastructure (CurrentUserService) thực thi qua IHttpContextAccessor
public interface ICurrentUser
{
    // Id của user đang đăng nhập (null nếu chưa đăng nhập)
    string? UserId { get; }

    // Tên đăng nhập
    string? UserName { get; }

    // Kiểm tra user đã đăng nhập chưa
    bool IsAuthenticated { get; }

    // Kiểm tra role
    bool IsInRole(string role);
}
```

> 💡 `IDisposable` giúp `IUnitOfWork` dọn dẹp tài nguyên (database connection) khi dùng xong — gọi qua `using` statement.

> 💡 **Tại sao `ICurrentUser` nằm ở Domain?** Vì Application layer cần biết UserId để thực thi business logic (VD: "chỉ Instructor mới được sửa Course của mình"). Nếu để `ICurrentUser` ở Infrastructure thì Application sẽ phải reference Infrastructure — vi phạm Dependency Rule.

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

### 📋 Cài NuGet packages cho `EduFlyUp.Infrastructure`

Chuột phải vào `EduFlyUp.Infrastructure` → `Manage NuGet Packages`:

| Package                                     | Phiên bản | Mục đích             |
| ------------------------------------------- | ----------- | ----------------------- |
| `Microsoft.EntityFrameworkCore`           | 8.x         | EF Core core            |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.x         | Provider cho SQL Server |
| `Microsoft.EntityFrameworkCore.Tools`     | 8.x         | Tạo Migration qua CLI  |

Cài thêm cho `EduFlyUp.Web`:

| Package | Phiên bản | Mục đích |
| ---------------------------------------- | ----------- | --------------------------------------- |
| `Microsoft.EntityFrameworkCore.Design` | 8.x | Hỗ trợ tạo Migration từ Web project |

### 📋 Cấu trúc thư mục trong `EduFlyUp.Infrastructure`

```
EduFlyUp.Infrastructure/
├── Identity/
│   └── ApplicationUser.cs       ← ✅ Đúng vị trí: kế thừa IdentityUser
└── Data/
    ├── AppDbContext.cs
    ├── Configurations/
    │   ├── CourseConfiguration.cs
    │   ├── LessonConfiguration.cs
    │   └── EnrollmentConfiguration.cs   ← ✅ Config FK, cascade cho Enrollment
    └── Seed/
        └── DataSeeder.cs
```

### 📝 Code mẫu — `AppDbContext.cs`

```csharp
using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Infrastructure.Data;

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
using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.Infrastructure.Data.Configurations;

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

        // ⚠️ QUAN TRỌNG: IsFree là computed property (tính từ Price)
        // Phải khai báo Ignore để EF Core không tạo cột "IsFree" trong DB
        builder.Ignore(c => c.IsFree);
    }
}
```

### 📝 Code mẫu — `LessonConfiguration.cs`

```csharp
using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.Infrastructure.Data.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        // Tên bảng trong database
        builder.ToTable("Lessons");

        // Primary Key
        builder.HasKey(l => l.Id);

        // Cấu hình các columns
        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(l => l.Content)
            .HasColumnType("nvarchar(max)"); // Hỗ trợ nội dung bài học dài

        builder.Property(l => l.VideoUrl)
            .HasMaxLength(500);

        builder.Property(l => l.DurationMinutes)
            .HasDefaultValue(0);

        builder.Property(l => l.Order)
            .HasDefaultValue(1);

        builder.Property(l => l.IsPreview)
            .HasDefaultValue(false);

        // Cấu hình quan hệ (N - 1 với Course): Nhiều Lesson thuộc 1 Course
        builder.HasOne(l => l.Course)
            .WithMany(c => c.Lessons)
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Cascade); // Xóa Course → tự động xóa toàn bộ Lessons liên quan
    }
}
```

### 📝 Code mẫu — `EnrollmentConfiguration.cs`

```csharp
using EduFlyUp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduFlyUp.Infrastructure.Data.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments");

        builder.HasKey(e => e.Id);

        // UserId dùng string (GUID của ASP.NET Identity)
        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(450); // Đúng với độ dài GUID của Identity

        builder.Property(e => e.EnrolledAt)
            .IsRequired();

        // CompletedAt nullable — null nghĩa là chưa hoàn thành
        builder.Property(e => e.CompletedAt)
            .IsRequired(false);

        // Status lưu dạng int vào DB
        builder.Property(e => e.Status)
            .IsRequired();

        // Quan hệ N-1 với Course
        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict); // Không cascade: tránh xóa enrollment khi xóa course
    }
}
```

> 💡 **Giải thích các thiết lập quan trọng:**
>
> - `HasMaxLength(300)`: Giới hạn độ dài chuỗi tương ứng kiểu `NVARCHAR(300)` trong SQL Server, tránh lãng phí dung lượng.
> - `HasDefaultValue(0)`: Thiết lập giá trị mặc định cho cột khi insert nếu không truyền giá trị.
> - `OnDelete(DeleteBehavior.Cascade)`: Khi xóa một khóa học, toàn bộ bài học thuộc khóa học đó cũng sẽ bị xóa theo (ràng buộc toàn vẹn dữ liệu).
> - `OnDelete(DeleteBehavior.Restrict)` trên Enrollment: Không xóa dây chuyền — tránh xóa lịch sử ghi danh.

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

### 📋 Cấu trúc thư mục trong `EduFlyUp.Infrastructure`

```
EduFlyUp.Infrastructure/
└── Repositories/
    ├── BaseRepository.cs      ← Implement IRepository<T> generic
    ├── CourseRepository.cs    ← Implement ICourseRepository
    └── UnitOfWork.cs          ← Implement IUnitOfWork
```

### 📝 Code mẫu — `BaseRepository.cs`

```csharp
using System.Linq.Expressions;
using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Infrastructure.Repositories;

public class BaseRepository<T> : IRepository<T> where T : BaseEntity
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
using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Infrastructure.Repositories;

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
using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;

namespace EduFlyUp.Infrastructure.Repositories;

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
using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Enums;
using EduFlyUp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Infrastructure.Data.Seed;

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

### 📝 Tạo Extension Method — `InfrastructureServiceExtensions.cs` trong `EduFlyUp.Infrastructure`

```csharp
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;
using EduFlyUp.Infrastructure.Repositories;
using EduFlyUp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Infrastructure;

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
                b => b.MigrationsAssembly("EduFlyUp.Infrastructure") // Migration nằm ở Infra
            ));

        // Đăng ký Repository và UnitOfWork
        // Scoped: mỗi HTTP request có 1 instance riêng
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICourseRepository, CourseRepository>();

        // Đăng ký ICurrentUser — inject IHttpContextAccessor để lấy user từ HttpContext
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        return services;
    }
}
```

### 📝 Cập nhật `Program.cs` trong `EduFlyUp.Web`

```csharp
using EduFlyUp.Infrastructure;
using EduFlyUp.Infrastructure.Data;
using EduFlyUp.Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

// ──────────────── Đăng ký Services ────────────────
builder.Services.AddControllersWithViews();

// Gọi extension method — đăng ký DbContext + Repositories + ICurrentUser
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
# Bước 1: Chọn Default project là EduFlyUp.Infrastructure
# Bước 2: Chạy lệnh tạo migration đầu tiên
Add-Migration InitialCreate -Project EduFlyUp.Infrastructure -StartupProject EduFlyUp.Web

# Bước 3: Áp dụng migration lên SQL Server
Update-Database -Project EduFlyUp.Infrastructure -StartupProject EduFlyUp.Web
```

> 💡 Sau khi chạy xong, kiểm tra SQL Server Management Studio — database `EduFlyUpDb` đã được tạo với các bảng `Courses`, `Lessons`, `Enrollments`.

---

## ✅ Checklist tổng kết Phase 1

### Task 1.1 — Solution Structure

- [ ] Tạo Blank Solution `EduFlyUp`
- [ ] Tạo đủ 5 projects đúng template và location
- [ ] Xóa các `Class1.cs` mặc định

### Task 1.2 — Project References

- [ ] `EduFlyUp.Application` → `EduFlyUp.Domain`
- [ ] `EduFlyUp.Infrastructure` → `EduFlyUp.Application`
- [ ] `EduFlyUp.Web` → `EduFlyUp.Application` + `EduFlyUp.Infrastructure`
- [ ] `EduFlyUp.UnitTests` → `EduFlyUp.Application`
- [ ] Build solution thành công (0 errors)

### Task 1.3 — Domain Entities

- [ ] `BaseEntity.cs`
- [ ] `Course.cs`
- [ ] `Lesson.cs` *(tự viết)*
- [ ] `Enrollment.cs` *(tự viết)*
- [ ] `CourseLevel.cs` (enum)
- [ ] `EnrollmentStatus.cs` (enum) *(tự viết)*
- [ ] ❌ **KHÔNG tạo** `ApplicationUser.cs` ở `Domain/Entities/` (sẽ tạo ở Phase 3)

### Task 1.4 — Domain Interfaces

- [ ] `IRepository.cs`
- [ ] `ICourseRepository.cs`
- [ ] `IUnitOfWork.cs`
- [ ] `ICurrentUser.cs` *(interface trừu tượng cho user hiện tại)*

### Task 1.5 — EF Core Setup

- [ ] Cài NuGet packages cho `EduFlyUp.Infrastructure` và `EduFlyUp.Web`
- [ ] `AppDbContext.cs`
- [ ] `CourseConfiguration.cs`
- [ ] `LessonConfiguration.cs` *(tự viết)*
- [ ] `EnrollmentConfiguration.cs` *(tự viết)*

### Task 1.6 — Repository Pattern

- [ ] `BaseRepository.cs`
- [ ] `CourseRepository.cs`
- [ ] `UnitOfWork.cs`

### Task 1.7 — Data Seeder

- [ ] `DataSeeder.cs` với ít nhất 2 courses, 3 lessons

### Task 1.8 — DI & Migration

- [ ] Cấu hình connection string trong `appsettings.json`
- [ ] `InfrastructureServiceExtensions.cs` (bao gồm đăng ký `ICurrentUser`)
- [ ] Cập nhật `Program.cs`
- [ ] Chạy `Add-Migration InitialCreate` thành công
- [ ] Chạy `Update-Database` thành công
- [ ] Kiểm tra database `EduFlyUpDb` đã được tạo trong SQL Server
- [ ] Chạy app (`F5`) — không có lỗi, dữ liệu seed thành công

---

## 🔑 Kiến thức quan trọng cần nhớ sau Phase 1

| Khái niệm | Giải thích ngắn |
| ------------------------------ | ------------------------------------------------------------------- |
| **Clean Architecture** | Dependencies chỉ trỏ vào trong — Domain là trung tâm |
| **Entity** | Object có ID riêng biệt, map 1-1 với bảng DB |
| **IRepository\<T\>** | Hợp đồng truy cập data — Application không biết về DB |
| **IUnitOfWork** | Gom nhiều thao tác vào 1 transaction |
| **ICurrentUser** | Interface trừu tượng ở Domain — Infrastructure implement qua HttpContext |
| **ApplicationUser** | Kế thừa IdentityUser → đặt ở `Infrastructure/Identity/`, không phải Domain |
| **DbContext** | Trung tâm EF Core — quản lý kết nối và tracking |
| **IEntityTypeConfiguration** | Cấu hình mapping Entity ↔ Table, tách biệt khỏi Domain |
| **Migration** | Lịch sử thay đổi schema DB — có thể rollback |
| **DI Container** | Quản lý vòng đời objects — Singleton/Scoped/Transient |
| **Extension Method** | `AddInfrastructure()` — giữ `Program.cs` gọn gàng |
| **Computed Property** | `IsFree => Price == 0` — không lưu DB, phải `Ignore()` trong Configuration |

---

> 📌 **Bước tiếp theo**: Khi hoàn thành Phase 1, báo mentor để review và bắt đầu **Phase 2 — ASP.NET Core MVC & Razor Pages**
