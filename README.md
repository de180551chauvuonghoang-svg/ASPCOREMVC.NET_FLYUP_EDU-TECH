# 🚀 .NET Toàn Diện - Project: **EduFlyUp** (Nền tảng học trực tuyến)

## Giới thiệu

**EduFlyUp** là nền tảng học trực tuyến mini được thiết kế chuẩn mực theo mô hình kiến trúc **N-Tier (Layered Architecture)** kết hợp mẫu thiết kế **Singleton DAO & Repository Pattern** theo định hướng chuẩn đào tạo chuyên sâu (tương thích cấu trúc đồ án/kỳ thi thực hành FPT/PRN như mẫu `examAPIandOData`), loại bỏ sự phức tạp không cần thiết của Web API/OData để tập trung tối đa vào **Pure ASP.NET Core 8 MVC**.

> 💡 **Mục tiêu của cấu trúc này:** Tách biệt rõ ràng từng tầng trách nhiệm: Thực thể nghiệp vụ (`BusinessObjects`) ➡️ Tầng truy cập dữ liệu trực tiếp (`DataAccess` + DAO) ➡️ Tầng trừu tượng dữ liệu (`Repositories`) ➡️ Tầng logic nghiệp vụ (`Services`) ➡️ Tầng giao diện người dùng (`Web MVC`).

---

## 🎯 Kiến thức .NET & Các Pattern được áp dụng trong project

| Kỹ năng / Kỹ thuật | Được áp dụng ở đâu |
| :--- | :--- |
| **N-Tier Architecture** | Tổ chức Solution thành 5 dự án độc lập, quản lý phụ thuộc 1 chiều |
| **Singleton DAO Pattern** | `SingletonBase<T>` quản lý các Data Access Object (`CourseDAO`, `LessonDAO`, ...) |
| **Repository Pattern** | `ICourseRepository` che giấu chi tiết DAO, hỗ trợ lỏng liên kết (Loose Coupling) |
| **Service Layer** | `ICourseService` chứa business logic, điều phối các repository |
| **Entity Framework Core 8** | Code-First, Migrations, Fluent API Configuration, Seeding |
| **ASP.NET Core 8 MVC** | Controllers, ViewModels, Razor Views `.cshtml`, Areas (`Admin`) |
| **Identity & Authentication** | `ApplicationUser`, Cookie Authentication, Quản lý Roles (`Admin`, `Teacher`, `Student`) |
| **Modern UI & 3D Interactive** | Typography Inter, Hero Banner Glassmorphism, Thẻ 3D tilt interaction |
| **Tag Helpers & Validation** | `asp-for`, `asp-action`, `ModelState.IsValid`, jQuery Unobtrusive Validation |
| **ViewComponents & Partials** | Tái sử dụng giao diện (`_CourseCard.cshtml`, `FeaturedCoursesViewComponent`) |
| **Custom Middleware** | Request Performance Timing & Global Exception Logging |

---

## 📁 Cấu trúc Solution (N-Tier Repository & DAO Pattern)

```
EduFlyUp/
├── EduFlyUp.BusinessObjects/      # 💎 Layer 1: Entities & Enums
│   ├── Entities/
│   │   ├── BaseEntity.cs          # Id, CreatedAt, UpdatedAt
│   │   ├── Course.cs              # Khóa học (Title, Price, Level, Thumbnail...)
│   │   ├── Lesson.cs              # Bài học (Title, Duration, VideoUrl...)
│   │   ├── Enrollment.cs          # Đăng ký khóa học của học viên
│   │   └── ApplicationUser.cs     # Kế thừa IdentityUser (FullName, AvatarUrl, Bio...)
│   └── Enums/
│       ├── CourseLevel.cs         # Beginner, Intermediate, Advanced
│       └── EnrollmentStatus.cs    # Active, Completed, Cancelled
│
├── EduFlyUp.DataAccess/           # 🗄️ Layer 2: EF Core DbContext & Singleton DAOs
│   ├── AppDbContext.cs            # Kế thừa IdentityDbContext<ApplicationUser>
│   ├── SingletonBase.cs           # Generic Thread-safe Singleton Base
│   ├── DAOs/
│   │   ├── CourseDAO.cs           # CRUD Course với DbContext
│   │   ├── LessonDAO.cs           # CRUD Lesson với DbContext
│   │   └── EnrollmentDAO.cs       # CRUD Enrollment với DbContext
│   ├── Configurations/            # Fluent API Entity Configurations
│   ├── Migrations/                # EF Core Database Migrations
│   └── Seed/
│       └── DataSeeder.cs          # Seed Roles, Admin mặc định, Khóa học mẫu
│
├── EduFlyUp.Repositories/         # 🔄 Layer 3: Repositories (Abstraction trên DAO)
│   ├── Interfaces/
│   │   ├── ICourseRepository.cs
│   │   ├── ILessonRepository.cs
│   │   └── IEnrollmentRepository.cs
│   └── Implementations/
│       ├── CourseRepository.cs    # Ủy quyền gọi CourseDAO.Instance
│       ├── LessonRepository.cs    # Ủy quyền gọi LessonDAO.Instance
│       └── EnrollmentRepository.cs# Ủy quyền gọi EnrollmentDAO.Instance
│
├── EduFlyUp.Services/             # ⚙️ Layer 4: Business Logic Services
│   ├── Interfaces/
│   │   ├── ICourseService.cs
│   │   └── ILessonService.cs
│   └── Implementations/
│       ├── CourseService.cs       # Điều phối ICourseRepository
│       └── LessonService.cs       # Điều phối ILessonRepository
│
└── EduFlyUp.Web/                  # 🌐 Layer 5: Presentation (ASP.NET Core MVC)
    ├── Controllers/               # HomeController, CoursesController, AccountController
    ├── Areas/
    │   └── Admin/                 # Phân hệ Quản trị (DashboardController, Views)
    ├── ViewModels/                # CourseCreateViewModel, CourseEditViewModel, Auth ViewModels
    ├── Views/                     # Razor Views (Home, Courses, Account, Shared)
    │   ├── Shared/
    │   │   ├── _Layout.cshtml     # Master Layout hiện đại (Glassmorphism Header/Footer)
    │   │   └── _CourseCard.cshtml # Partial View tái sử dụng
    ├── ViewComponents/            # FeaturedCoursesViewComponent
    ├── Middlewares/               # PerformanceMiddleware
    ├── wwwroot/                   # Static files (CSS, JS, Logo, Hero 3D assets)
    └── Program.cs                 # Dependency Injection & Middleware Pipeline
```

---

## 🔄 Sơ đồ luồng dữ liệu (Request & Data Flow)

Khi người dùng thực hiện một thao tác trên trình duyệt (ví dụ: Xem danh sách khóa học hoặc Tạo khóa học mới), luồng xử lý diễn ra như sau:

```
[ Trình duyệt (Browser) ]
       │  (1) HTTP GET /Courses
       ▼
[ EduFlyUp.Web : CoursesController ]
       │  (2) Gọi ICourseService.GetAllCoursesAsync()
       ▼
[ EduFlyUp.Services : CourseService ]
       │  (3) Kiểm tra Business Logic, gọi ICourseRepository
       ▼
[ EduFlyUp.Repositories : CourseRepository ]
       │  (4) Ủy quyền gọi CourseDAO.Instance.GetAllAsync()
       ▼
[ EduFlyUp.DataAccess : CourseDAO (Singleton) ]
       │  (5) Thực thi LINQ với AppDbContext
       ▼
[ SQL Server Database ]
       │  (6) Trả về dữ liệu Entity
       ▲
[ CourseDAO ➔ CourseRepository ➔ CourseService ]
       │  (7) Trả về danh sách Course Entities / DTOs
       ▲
[ CoursesController ]
       │  (8) Map sang CourseItemViewModel và truyền vào Razor View
       ▼
[ Razor View: Views/Courses/Index.cshtml ]
       │  (9) Render mã HTML5 + CSS + 3D Effects trả về Browser
       ▼
[ Người dùng nhìn thấy giao diện hiển thị ]
```

---

## 🗺️ Lộ trình các Phase triển khai

### [Phase 1 — Nền tảng & Cấu trúc N-Tier Solution](./docs/phase1/phase1-guide.md)
- [x] Tạo Solution theo chuẩn 5 Layer: `BusinessObjects`, `DataAccess`, `Repositories`, `Services`, `Web`.
- [x] Định nghĩa Entities (`Course`, `Lesson`, `Enrollment`, `ApplicationUser`) và Enums.
- [x] Xây dựng `AppDbContext`, Fluent API Configurations, Migrations.
- [x] Cài đặt mẫu `SingletonBase<T>` và các lớp `DAO` (`CourseDAO`, `LessonDAO`, `EnrollmentDAO`).
- [x] Xây dựng tầng `Repositories` ủy quyền cho `DAO`.
- [x] Xây dựng tầng `Services` cung cấp API nghiệp vụ cho ứng dụng.
- [x] Đăng ký DI trong `Program.cs` và Seeding dữ liệu ban đầu.

---

### [Phase 2 — ASP.NET Core MVC Hiện Đại & ViewModels](./docs/phase2/phase2-guide.md)
- [x] Thiết lập Master Layout hiện đại với font Inter, Glassmorphism header, Modern dark footer.
- [x] Xây dựng `ViewModels` chống tấn công *Over-posting Attack* (`CourseCreateViewModel`, `CourseEditViewModel`, `CourseItemViewModel`).
- [x] Xây dựng `CoursesController` thực hiện đầy đủ luồng CRUD gọi thông qua `CourseService`.
- [x] Validation 2 lớp: Client-side (jQuery Unobtrusive) và Server-side (`ModelState.IsValid`).
- [x] Thiết kế UI component hóa: Partial View `_CourseCard.cshtml` và `FeaturedCoursesViewComponent`.
- [x] Xây dựng phân hệ Quản trị viên `Areas/Admin` (Thống kê số lượng, doanh thu, quản lý danh mục).
- [x] Custom Middleware đo lường hiệu năng xử lý request.

---

### [Phase 3 — Authentication, Authorization & Interactive UI](./docs/phase3/phase3-guide.md)
- [x] Tích hợp ASP.NET Core Identity với `ApplicationUser` tùy biến.
- [x] Cấu hình Cookie Authentication an toàn (HttpOnly, SameSite, Anti-Forgery Token).
- [x] Hệ thống phân quyền theo Role (`Admin`, `Teacher`, `Student`).
- [x] Các màn hình Xác thực: Đăng ký (`Register`), Đăng nhập (`Login`), Đăng xuất (`Logout`).
- [x] Giao diện hiện đại: Hero Banner tương tác, hiệu ứng thẻ nghiêng 3D (Vanilla CSS / 3D transform).
- [x] Data Seeder tự động tạo sẵn tài khoản Quản trị viên và danh mục khóa học mẫu.

---

## 🛠️ Tech Stack & Thư viện sử dụng

| Phân hệ | Công nghệ |
| :--- | :--- |
| **Framework** | .NET 8.0 (C# 12) |
| **Web Framework** | ASP.NET Core MVC 8.0 |
| **ORM** | Entity Framework Core 8.0 (SQL Server Provider, Tools) |
| **Database** | Microsoft SQL Server (LocalDB / Express) |
| **Authentication** | ASP.NET Core Identity (EF Core Stores) + Cookie Auth |
| **Architecture** | N-Tier Layered Architecture (Repository & Singleton DAO Pattern) |
| **Design / Frontend** | Bootstrap 5, Bootstrap Icons, Font Inter, Vanilla CSS 3D Transforms |
| **Validation** | Data Annotations + jQuery Validate Unobtrusive |

---

## 🚀 Hướng dẫn Cài đặt & Chạy dự án

### 1. Yêu cầu môi trường
- .NET 8.0 SDK trở lên.
- Visual Studio 2022 (với workload *ASP.NET and web development*) hoặc Visual Studio Code / Rider.
- Microsoft SQL Server (LocalDB hoặc SQL Server Express).

### 2. Cấu hình Chuỗi kết nối Database
Mở file [appsettings.json](file:///c:/Users/RinHeo/Desktop/baitapprn/EduFlyUp/EduFlyUp.Web/appsettings.json) trong dự án `EduFlyUp.Web`, kiểm tra chuỗi kết nối:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EduFlyUpDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

### 3. Cập nhật Database & Khởi chạy ứng dụng
Mở terminal tại thư mục gốc của project:
```powershell
# Chuyển vào thư mục solution
cd EduFlyUp

# Build kiểm tra toàn bộ 5 project
dotnet build

# Chạy ứng dụng Web (database và dữ liệu mẫu sẽ tự động được khởi tạo tại lần chạy đầu tiên)
dotnet run --project .\EduFlyUp.Web\EduFlyUp.Web.csproj
```

### 4. Tài khoản mặc định để thử nghiệm
Khi ứng dụng khởi chạy lần đầu, `DataSeeder` sẽ tự động tạo tài khoản quản trị:
- **Tài khoản (Email):** `admin@eduflyup.vn`
- **Mật khẩu:** `Admin@123`
- **Vai trò (Role):** `Admin` (truy cập được vào `/Admin/Dashboard`)
