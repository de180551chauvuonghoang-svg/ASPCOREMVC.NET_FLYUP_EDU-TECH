# 🚀 .NET Toàn Diện - Project: **EduFlyUp** (Nền tảng học trực tuyến)

## Giới thiệu

**EduFlyUp** là một nền tảng học trực tuyến mini được thiết kế đặc biệt để cover **toàn bộ kiến thức .NET** quan trọng nhất mà bất kỳ .NET Developer chuyên nghiệp nào cũng cần biết.

> Lý do chọn project này: E-learning là domain có đủ độ phức tạp để áp dụng tất cả các pattern, nhưng không quá phức tạp về nghiệp vụ — rất phù hợp để học .NET một cách bài bản.

---

## 🎯 Kiến thức .NET được cover trong project

| Kỹ năng                                  | Được áp dụng ở đâu                       |
| ------------------------------------------ | ------------------------------------------------ |
| **ASP.NET Core MVC**                 | Toàn bộ cấu trúc ứng dụng                  |
| **Razor Pages**                      | Trang học viên (student-facing)                |
| **Blazor Components**                | Dashboard thống kê real-time, quiz interactive |
| **Entity Framework Core**            | Truy cập database, migration                    |
| **Clean Architecture**               | Tổ chức code theo layers                       |
| **Repository + Unit of Work**        | Data access pattern                              |
| **Dependency Injection**             | Tất cả services                                |
| **Identity & Auth (Cookie + JWT)**   | Đăng nhập, phân quyền Admin/Teacher/Student |
| **Web API**                          | API riêng phục vụ Blazor & frontend           |
| **SignalR**                          | Chat room, thông báo real-time                 |
| **CQRS + MediatR**                   | Command/Query separation                         |
| **AutoMapper**                       | DTO mapping                                      |
| **FluentValidation**                 | Validate dữ liệu đầu vào                    |
| **Middleware**                       | Xử lý lỗi, logging tùy chỉnh                |
| **Background Jobs (Hosted Service)** | Gửi email nhắc nhở                            |
| **File Upload**                      | Upload ảnh bìa khóa học                      |
| **Unit Testing (xUnit)**             | Test services & controllers                      |
| **Caching (Memory Cache)**           | Cache danh sách khóa học                      |
| **Structured Logging (Serilog)**     | Ghi log có cấu trúc ra file/console/Seq       |
| **Options Pattern**                  | Bind cấu hình `appsettings.json` vào class C# |
| **HttpClientFactory**                | Gọi external API an toàn, quản lý lifetime   |
| **Global Exception Handling**        | Middleware xử lý lỗi tập trung, trả ProblemDetails |

---

## 📁 Cấu trúc Project (Clean Architecture)

```
EduFlyUp/
├── src/
│   ├── EduFlyUp.Domain/              # 💡 Layer 1: Entities, Enums, Interfaces
│   │   ├── Entities/
│   │   │   ├── Course.cs
│   │   │   ├── Lesson.cs
│   │   │   ├── Enrollment.cs
│   │   │   ├── Quiz.cs
│   │   │   └── ChatMessage.cs
│   │   │   # ❌ ApplicationUser.cs KHÔNG đặt ở đây
│   │   │   # vì nó kế thừa IdentityUser (Infrastructure concern)
│   │   ├── Enums/
│   │   │   ├── CourseLevel.cs
│   │   │   └── UserRole.cs
│   │   └── Interfaces/
│   │       ├── IRepository.cs
│   │       ├── IUnitOfWork.cs
│   │       ├── ICourseRepository.cs
│   │       └── ICurrentUser.cs       # ✅ Interface trừu tượng, không phụ thuộc Identity
│   │
│   ├── EduFlyUp.Application/         # 💡 Layer 2: Business Logic (CQRS, DTOs, Services)
│   │   ├── Features/
│   │   │   ├── Courses/
│   │   │   │   ├── Commands/       # CreateCourse, UpdateCourse, DeleteCourse
│   │   │   │   ├── Queries/        # GetAllCourses, GetCourseById
│   │   │   │   └── Validators/     # FluentValidation
│   │   │   ├── Enrollments/
│   │   │   ├── Quizzes/
│   │   │   └── Auth/
│   │   ├── DTOs/
│   │   ├── Mappings/               # AutoMapper Profiles
│   │   └── Services/
│   │       ├── IEmailService.cs
│   │       └── ICacheService.cs
│   │
│   ├── EduFlyUp.Infrastructure/      # 💡 Layer 3: EF Core, External Services
│   │   ├── Identity/
│   │   │   └── ApplicationUser.cs    # ✅ Đúng vị trí: kế thừa IdentityUser
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── Seed/               # DataSeeder.cs
│   │   ├── Repositories/
│   │   │   ├── BaseRepository.cs
│   │   │   └── CourseRepository.cs
│   │   ├── Services/
│   │   │   ├── EmailService.cs     # SmtpClient / MailKit
│   │   │   ├── FileUploadService.cs
│   │   │   └── CurrentUserService.cs # ✅ Implement ICurrentUser
│   │   └── BackgroundJobs/
│   │       └── ReminderHostedService.cs
│   │
│   └── EduFlyUp.Web/                 # 💡 Layer 4: ASP.NET Core Web App
│       ├── Controllers/            # MVC Controllers
│       ├── Areas/
│       │   ├── Admin/              # Quản lý hệ thống (MVC)
│       │   └── Teacher/            # Quản lý khóa học (MVC + API)
│       ├── Pages/                  # Razor Pages (Student area)
│       │   ├── Courses/
│       │   ├── Learn/
│       │   └── Profile/
│       ├── Components/             # Blazor Components
│       │   ├── Dashboard/
│       │   ├── QuizPlayer/
│       │   └── LiveChat/
│       ├── Hubs/
│       │   └── ChatHub.cs          # SignalR Hub
│       ├── Middleware/
│       │   ├── ExceptionMiddleware.cs
│       │   └── RequestLoggingMiddleware.cs
│       ├── wwwroot/
│       └── Program.cs
│
└── tests/
    ├── EduFlyUp.UnitTests/           # xUnit tests
    └── EduFlyUp.IntegrationTests/    # Integration tests
```

> **📌 Lý do `ApplicationUser` nằm ở `Infrastructure/Identity/`:**
> `ApplicationUser` kế thừa `IdentityUser` — một class của thư viện `Microsoft.AspNetCore.Identity`.
> Domain layer **không được** phụ thuộc vào bất kỳ framework bên ngoài nào (Clean Architecture rule).
> Thay vào đó, Domain chỉ định nghĩa interface `ICurrentUser` để các layer khác có thể reference mà không phá vỡ dependency rule.

---

## 🗺️ Lộ trình học theo từng Phase

### Phase 1 — Nền tảng & Cấu trúc (Tuần 1-2)

> **Học**: Project setup, Clean Architecture, Dependency Injection, EF Core

- [X] Tạo Solution với nhiều project (.csproj)
- [X] Thiết kế Domain Entities
- [ ] Setup EF Core + SQLite/SQL Server + Migrations
- [ ] Cấu hình Dependency Injection trong `Program.cs`
- [ ] Repository Pattern + Unit of Work
- [ ] Seed dữ liệu mẫu

**👉 Kết quả**: Ứng dụng có database và có thể đọc/ghi dữ liệu

---

### Phase 2 — ASP.NET Core MVC & Modern C# UI (MudBlazor) (Tuần 3-4)

> **Học**: MVC Pattern, Routing, ViewModels, Blazor Server Components, MudBlazor Design System

- [ ] Cấu hình tích hợp MudBlazor vào ASP.NET Core MVC
- [ ] ViewModels chống Over-posting Attack
- [ ] Xây dựng Component C# hiển thị danh sách khóa học (`<MudCard>`, `<MudGrid>`)
- [ ] Form thêm mới khóa học bằng C# (`<MudForm>`, `<MudTextField>`)
- [ ] Quản lý khóa học trong Admin Area bằng `<MudDataGrid>` (Filter, Sort, Pagination tự động)
- [ ] Custom Middleware đo lường hiệu năng HTTP Request

**👉 Kết quả**: Website MVC hiện đại với 100% giao diện được viết bằng C# component đẹp mắt, không dùng HTML/CSS thủ công

---

### Phase 3 — Authentication & Authorization (Tuần 5)

> **Học**: ASP.NET Identity, Cookie Auth, JWT, Role-based Authorization

- [ ] Cài đặt ASP.NET Core Identity
- [ ] Đăng ký / Đăng nhập / Quên mật khẩu
- [ ] Roles: Admin, Teacher, Student
- [ ] Policy-based Authorization
- [ ] JWT cho Web API endpoints

**👉 Kết quả**: Hệ thống phân quyền hoàn chỉnh

---

### Phase 4 — CQRS + MediatR + FluentValidation (Tuần 6)

> **Học**: Clean CQRS architecture, MediatR pipeline, Validation

> ⚠️ **Lưu ý quan trọng**: Project này dùng **song song** Repository/UnitOfWork và CQRS/MediatR **chỉ với mục đích học tập** — để bạn thực hành cả hai pattern trong cùng một codebase. Trong **dự án thực tế**, cân nhắc chỉ chọn **một pattern** để tránh trùng lặp trách nhiệm với `DbContext`. Nếu đã dùng CQRS + MediatR, các Handler có thể gọi thẳng `DbContext` mà không cần thêm Repository layer.

- [ ] Cài đặt MediatR
- [ ] Viết Commands (CreateCourseCommand, EnrollCommand)
- [ ] Viết Queries (GetCoursesQuery, GetDashboardStatsQuery)
- [ ] Pipeline Behaviors (Validation, Logging, Caching)
- [ ] FluentValidation validators
- [ ] AutoMapper profiles

**👉 Kết quả**: Business logic sạch, có thể test độc lập

---

### Phase 5 — Web API + Blazor (Tuần 7-8)

> **Học**: RESTful API, Swagger, Blazor Server/WASM, JS Interop

- [ ] Tạo API endpoints cho Course, Enrollment, Quiz
- [ ] Swagger/OpenAPI documentation
- [ ] Blazor Dashboard (thống kê học viên, doanh thu)
- [ ] Blazor Quiz Player (làm bài thi interactive)
- [ ] Kết hợp Blazor trong MVC app (Razor Component)

**👉 Kết quả**: App có cả UI truyền thống và interactive Blazor

---

### Phase 6 — SignalR & Background Services (Tuần 9)

> **Học**: Real-time communication, Hosted Services, IBackgroundService

- [ ] ChatHub với SignalR (chat phòng học)
- [ ] Thông báo real-time khi có học viên đăng ký
- [ ] `IHostedService` gửi email nhắc nhở hàng ngày
- [ ] Progress tracking real-time

**👉 Kết quả**: App có tính năng real-time thực sự

---

### Phase 7 — Caching, File Upload & Email (Tuần 10)

> **Học**: IMemoryCache, IDistributedCache, Multipart Upload, SMTP

- [ ] Memory Cache cho danh sách khóa học
- [ ] Upload ảnh bìa khóa học (lưu local)
- [ ] Email service với MailKit
- [ ] Gửi email xác nhận đăng ký khóa học

**👉 Kết quả**: App production-ready hơn

---

### Phase 8 — Unit Testing & Integration Testing (Tuần 11-12)

> **Học**: xUnit, Moq, Test patterns, Integration testing với WebApplicationFactory

- [ ] Unit test cho Application Services
- [ ] Mock repositories với Moq
- [ ] Integration test API endpoints
- [ ] Test Authentication flows

**👉 Kết quả**: Codebase có test coverage, hiểu cách viết code testable

---

## 🛠️ Tech Stack

| Thành phần   | Công nghệ                    |
| -------------- | ------------------------------ |
| Framework      | ASP.NET Core 8.0 (.NET 8)      |
| ORM            | Entity Framework Core 8        |
| Database       | Microsoft SQL Server           |
| Auth           | ASP.NET Core Identity + JWT    |
| CQRS           | MediatR                        |
| Mapping        | AutoMapper                     |
| Validation     | FluentValidation               |
| Real-time      | SignalR                        |
| Interactive UI | Blazor Server                  |
| Testing        | xUnit + Moq + FluentAssertions |
| API Docs       | Swagger (Swashbuckle)          |
| Email          | MailKit                        |
| UI & Components | MudBlazor (Material Design 3 in C#) |
| Logging        | Serilog + Seq                  |
| Code Review    | CodeRabbit AI (tự động review PR) |

---

## ✅ Bước tiếp theo

Bắt đầu với **Phase 1**:

1. Tạo Solution structure đầy đủ
2. Setup Domain layer với các Entities
3. Cấu hình EF Core + Database
4. Chạy được ứng dụng lần đầu tiên 🎉
