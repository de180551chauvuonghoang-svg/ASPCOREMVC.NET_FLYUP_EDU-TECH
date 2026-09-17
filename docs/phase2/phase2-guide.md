# 📘 Hướng Dẫn Thực Hành Phase 2 — ASP.NET Core 8 MVC Hiện Đại (Pure Modern MVC)

Chào mừng bạn đến với **Phase 2** của hành trình trở thành **Senior .NET Developer**! 🚀

Ở Phase 1, bạn đã xây dựng hoàn chỉnh tầng nền tảng với Clean Architecture, Repository Pattern và SQL Server. Trong **Phase 2**, bạn sẽ xây dựng toàn bộ giao diện và luồng xử lý người dùng theo đúng **100% Chuẩn Mực ASP.NET Core MVC Hiện Đại**:

- Toàn bộ giao diện nằm trọn vẹn trong thư mục **`Views/`**.
- Sử dụng **Tag Helpers**, **ViewModels**, **Partial Views**, **ViewComponents**, **Areas** và **Modern Design System** đẹp mắt, sang trọng.

---

## 🎯 Mục tiêu cốt lõi của Phase 2

1. **Hiểu sâu sắc vòng đời HTTP Request trong MVC**: Client ➡️ Routing ➡️ Middleware ➡️ Controller ➡️ Action ➡️ ViewModel ➡️ Razor View (`.cshtml`) ➡️ HTML Response.
2. **Nguyên tắc bất di bất dịch**: Tuyệt đối **không rò rỉ Entity ra UI**. 100% tương tác với View thông qua **ViewModels** (chống lỗ hổng *Over-posting Attack*).
3. **Master Tag Helpers**: Loại bỏ hoàn toàn `@Html.TextBoxFor`, viết giao diện chuẩn HTML5 kết hợp C# IntelliSense với `asp-controller`, `asp-action`, `asp-for`, `asp-validation-for`.
4. **Validation 2 lớp an toàn tuyệt đối**:
   - **Client-side**: Xác thực ngay trên trình duyệt bằng jQuery Unobtrusive Validation (không cần load lại trang).
   - **Server-side**: Xác thực bảo mật bằng DataAnnotations và `ModelState.IsValid`.
5. **Tổ chức giao diện tái sử dụng chuẩn MVC**:
   - **Partial Views (`_CourseCard.cshtml`)**: Tái sử dụng thẻ hiển thị khóa học.
   - **ViewComponents (`FeaturedCoursesViewComponent`)**: Tạo widget C# độc lập tự truy vấn database.
6. **Phân hệ Quản trị với Areas**: Tách biệt khu vực người dùng và khu vực quản trị viên (`Areas/Admin`).
7. **Custom Middleware**: Đo lường hiệu năng và xử lý lỗi tập trung.

---

## 🗺️ Bản đồ các Task trong Phase 2

```
Task 2.1 ──► Kiến trúc MVC Hiện đại & Luồng dữ liệu (Request Lifecycle)
    │
Task 2.2 ──► Master Layout & Modern Design System (Font Inter, Modern CSS)
    │
Task 2.3 ──► Thiết kế ViewModels & Data Annotations (Model Layer)
    │
Task 2.4 ──► Xây dựng CoursesController & Các Razor Views (Tag Helpers)
    │
Task 2.5 ──► Form Validation 2 lớp & Thông báo tương tác (TempData Flash Messages)
    │
Task 2.6 ──► Tái sử dụng UI: Partial Views & ViewComponents
    │
Task 2.7 ──► Tổ chức Phân hệ Quản trị với Areas (Admin Dashboard)
    │
Task 2.8 ──► Custom Middleware Pipeline (Logging & Exception Handling)
    │
Task 2.9 ──► Tổng duyệt & Nghiệm thu toàn diện Phase 2
```

---

## Task 2.1 — Kiến trúc MVC Hiện Đại & Vòng Đời Request

### 🧠 Luồng đi của một HTTP Request trong ASP.NET Core 8

```
[Trình duyệt] Gửi: GET /Courses/Details/1
      │
      ▼
┌────────────────────────────────────────────────────────┐
│               Middleware Pipeline                      │
│  Logging ──► ExceptionHandler ──► StaticFiles ──► Routing
└──────────────────────────┬─────────────────────────────┘
                           │ Route khớp: CoursesController.Details(id: 1)
                           ▼
┌────────────────────────────────────────────────────────┐
│               CoursesController (C)                    │
│  1. Nhận id = 1 từ URL (Model Binding)                │
│  2. Gọi _unitOfWork.Courses.GetCourseWithLessonsAsync()│
│  3. Map Entity sang CourseDetailViewModel              │
│  4. return View(viewModel);                            │
└──────────────────────────┬─────────────────────────────┘
                           │ Truyền ViewModel sang View
                           ▼
┌────────────────────────────────────────────────────────┐
│           Views/Courses/Details.cshtml (V)             │
│  Razor Engine biên dịch C# (@Model) + HTML ──► Render  │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
[Trình duyệt] Nhận về mã HTML hiển thị màn hình
```

---

## Task 2.2 — Master Layout & Modern Design System

Không cần thư viện cồng kềnh, chúng ta tích hợp một **Modern Design System** ngay trong `_Layout.cshtml`:

- **Font chữ**: Google Font Inter (chuẩn typography quốc tế, cực kỳ sắc nét).
- **Bộ icon**: Bootstrap Icons 1.11+.
- **Card & Button**: Bo góc mềm (`rounded-4`), đổ bóng mịn (`shadow-sm`, `hover-lift`), màu sắc Indigo hiện đại.

### 📋 File: `EduFlyUp.Web/Views/Shared/_Layout.cshtml`

```cshtml
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - EduFlyUp</title>

    <!-- Google Font: Inter -->
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet">

    <!-- Bootstrap 5 CSS & Icons -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" rel="stylesheet">

    <!-- Modern Design System Styles -->
    <style>
        :root {
            --primary-color: #4f46e5;
            --primary-hover: #4338ca;
            --bg-color: #f8fafc;
            --text-main: #0f172a;
            --text-muted: #64748b;
        }
        body {
            font-family: 'Inter', sans-serif;
            background-color: var(--bg-color);
            color: var(--text-main);
        }
        .navbar-brand { font-weight: 700; color: var(--primary-color) !important; }
        .nav-link { font-weight: 500; color: #475569 !important; }
        .nav-link:hover { color: var(--primary-color) !important; }
        .btn-primary {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
            border-radius: 10px;
            font-weight: 500;
            padding: 8px 20px;
            transition: all 0.2s;
        }
        .btn-primary:hover {
            background-color: var(--primary-hover);
            transform: translateY(-1px);
        }
        .card-modern {
            border: 1px solid #e2e8f0;
            border-radius: 16px;
            background: #ffffff;
            transition: all 0.25s ease-in-out;
        }
        .card-modern:hover {
            transform: translateY(-5px);
            box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.08), 0 8px 10px -6px rgba(0, 0, 0, 0.04);
            border-color: #cbd5e1;
        }
        .badge-level {
            padding: 6px 12px;
            border-radius: 20px;
            font-weight: 600;
            font-size: 0.75rem;
        }
    </style>
    @await RenderSectionAsync("Styles", required: false)
</head>
<body class="d-flex flex-column min-vh-100">
    <!-- Navbar Header -->
    <nav class="navbar navbar-expand-lg navbar-light bg-white border-bottom sticky-top py-3">
        <div class="container">
            <a class="navbar-brand d-flex align-items-center gap-2" asp-controller="Home" asp-action="Index">
                <i class="bi bi-mortarboard-fill fs-3"></i>
                <span class="fs-4">EduFlyUp</span>
            </a>
            <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navContent">
                <span class="navbar-toggler-icon"></span>
            </button>
            <div class="collapse navbar-collapse" id="navContent">
                <ul class="navbar-nav me-auto mb-2 mb-lg-0">
                    <li class="nav-item">
                        <a class="nav-link" asp-controller="Home" asp-action="Index">Trang chủ</a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" asp-controller="Courses" asp-action="Index">Khóa học</a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link text-warning fw-semibold" asp-area="Admin" asp-controller="Dashboard" asp-action="Index">
                            <i class="bi bi-shield-lock me-1"></i>Quản trị
                        </a>
                    </li>
                </ul>
                <div class="d-flex align-items-center gap-2">
                    <a class="btn btn-outline-secondary btn-sm rounded-pill px-3" href="#">Đăng nhập</a>
                    <a class="btn btn-primary btn-sm rounded-pill px-3" href="#">Bắt đầu học</a>
                </div>
            </div>
        </div>
    </nav>

    <!-- Main Content -->
    <main class="container py-4 flex-grow-1">
        <!-- Thông báo Flash Messages từ TempData -->
        @if (TempData["SuccessMessage"] != null)
        {
            <div class="alert alert-success alert-dismissible fade show rounded-3 shadow-sm border-0" role="alert">
                <i class="bi bi-check-circle-fill me-2"></i>@TempData["SuccessMessage"]
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        }
        @if (TempData["ErrorMessage"] != null)
        {
            <div class="alert alert-danger alert-dismissible fade show rounded-3 shadow-sm border-0" role="alert">
                <i class="bi bi-exclamation-triangle-fill me-2"></i>@TempData["ErrorMessage"]
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        }

        @RenderBody()
    </main>

    <!-- Footer -->
    <footer class="bg-white border-top py-4 mt-auto">
        <div class="container text-center text-muted small">
            <p class="mb-1">© @DateTime.UtcNow.Year <strong>EduFlyUp</strong> - Nền tảng học trực tuyến chuẩn Clean Architecture</p>
        </div>
    </footer>

    <!-- Bootstrap 5 JS -->
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

---

## Task 2.3 — Thiết Kế ViewModels (Tầng Model)

Tạo thư mục: `EduFlyUp.Web/Models/Courses/`

### 📝 1. `CourseItemViewModel.cs` (Dùng cho Card & Danh sách)

```csharp
using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Web.Models.Courses;

public class CourseItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;
    public CourseLevel Level { get; set; }
    public int LessonCount { get; set; }
}
```

### 📝 2. `CourseDetailViewModel.cs` (Dùng cho Trang chi tiết)

```csharp
using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Web.Models.Courses;

public class CourseDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;
    public CourseLevel Level { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LessonItemViewModel> Lessons { get; set; } = new();
}

public class LessonItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int Order { get; set; }
    public bool IsPreview { get; set; }
}
```

### 📝 3. `CourseCreateViewModel.cs` (Form Tạo mới kèm Data Annotations)

```csharp
using System.ComponentModel.DataAnnotations;
using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Web.Models.Courses;

public class CourseCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề khóa học.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 200 ký tự.")]
    [Display(Name = "Tiêu đề khóa học")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chi tiết.")]
    [MaxLength(2000, ErrorMessage = "Mô tả tối đa 2000 ký tự.")]
    [Display(Name = "Mô tả khóa học")]
    public string Description { get; set; } = string.Empty;

    [Url(ErrorMessage = "Đường dẫn hình ảnh không hợp lệ.")]
    [Display(Name = "Link ảnh bìa (Thumbnail URL)")]
    public string? ThumbnailUrl { get; set; }

    [Range(0, 100000000, ErrorMessage = "Học phí phải từ 0 đến 100.000.000 VNĐ.")]
    [Display(Name = "Học phí (VNĐ)")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn cấp độ.")]
    [Display(Name = "Cấp độ")]
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    [Display(Name = "Công khai khóa học ngay")]
    public bool IsPublished { get; set; } = true;
}
```

---

## Task 2.4 — Xây Dựng Controller & Views với Tag Helpers

### 📋 Tạo Controller: `EduFlyUp.Web/Controllers/CoursesController.cs`

```csharp
using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Web.Models.Courses;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

public class CoursesController : Controller
{
    private readonly IUnitOfWork _unitOfWork;

    public CoursesController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // GET: /Courses
    public async Task<IActionResult> Index()
    {
        var courses = await _unitOfWork.Courses.GetPublishedCoursesAsync();

        var viewModel = courses.Select(c => new CourseItemViewModel
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            ThumbnailUrl = string.IsNullOrEmpty(c.ThumbnailUrl) ? "https://placehold.co/600x400/4f46e5/ffffff?text=Course" : c.ThumbnailUrl,
            Price = c.Price,
            Level = c.Level,
            LessonCount = c.Lessons.Count
        }).ToList();

        return View(viewModel);
    }

    // GET: /Courses/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var course = await _unitOfWork.Courses.GetCourseWithLessonsAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        var viewModel = new CourseDetailViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            ThumbnailUrl = string.IsNullOrEmpty(course.ThumbnailUrl) ? "https://placehold.co/600x400/4f46e5/ffffff?text=Course" : course.ThumbnailUrl,
            Price = course.Price,
            Level = course.Level,
            CreatedAt = course.CreatedAt,
            Lessons = course.Lessons.OrderBy(l => l.Order).Select(l => new LessonItemViewModel
            {
                Id = l.Id,
                Title = l.Title,
                DurationMinutes = l.DurationMinutes,
                Order = l.Order,
                IsPreview = l.IsPreview
            }).ToList()
        };

        return View(viewModel);
    }

    // GET: /Courses/Create
    public IActionResult Create()
    {
        return View(new CourseCreateViewModel());
    }

    // POST: /Courses/Create
    [HttpPost]
    [ValidateAntiForgeryToken] // Chống tấn công CSRF
    public async Task<IActionResult> Create(CourseCreateViewModel model)
    {
        // 1. Kiểm tra Server-side validation
        if (!ModelState.IsValid)
        {
            return View(model); // Trả lại form kèm thông báo lỗi
        }

        // 2. Map ViewModel ──► Domain Entity
        var course = new Course
        {
            Title = model.Title,
            Description = model.Description,
            ThumbnailUrl = model.ThumbnailUrl ?? string.Empty,
            Price = model.Price,
            Level = model.Level,
            IsPublished = model.IsPublished,
            InstructorId = "instructor-system-default"
        };

        // 3. Lưu vào Database qua Unit of Work
        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();

        // 4. Flash message và chuyển hướng
        TempData["SuccessMessage"] = $"Khóa học '{course.Title}' đã được tạo thành công!";
        return RedirectToAction(nameof(Index));
    }
}
```

---

### 📋 Tạo View Danh Sách: `EduFlyUp.Web/Views/Courses/Index.cshtml`

```cshtml
@model IEnumerable<EduFlyUp.Web.Models.Courses.CourseItemViewModel>

@{
    ViewData["Title"] = "Danh sách Khóa học";
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <h2 class="fw-bold mb-1">Khám phá Khóa học</h2>
        <p class="text-muted">Chương trình đào tạo .NET chuyên sâu từ chuyên gia</p>
    </div>
    <a asp-action="Create" class="btn btn-primary shadow-sm">
        <i class="bi bi-plus-lg me-1"></i>Tạo khóa học mới
    </a>
</div>

<div class="row g-4">
    @if (!Model.Any())
    {
        <div class="col-12 text-center py-5">
            <i class="bi bi-inbox fs-1 text-muted"></i>
            <p class="mt-2 text-muted">Chưa có khóa học nào được đăng tải.</p>
        </div>
    }
    else
    {
        @foreach (var item in Model)
        {
            <div class="col-md-6 col-lg-4">
                <!-- Tái sử dụng Partial View hiển thị Card khóa học -->
                <partial name="_CourseCard" model="item" />
            </div>
        }
    }
</div>
```

---

## Task 2.5 — Tái Sử Dụng UI: Partial Views & ViewComponents

### 1. Tạo Partial View: `EduFlyUp.Web/Views/Shared/_CourseCard.cshtml`

Tạo file `_CourseCard.cshtml` trong `Views/Shared/`:

```cshtml
@model EduFlyUp.Web.Models.Courses.CourseItemViewModel

<div class="card card-modern h-100 shadow-sm overflow-hidden">
    <img src="@Model.ThumbnailUrl" class="card-img-top" alt="@Model.Title" style="height: 190px; object-fit: cover;">
    <div class="card-body d-flex flex-column p-4">
        <div class="d-flex justify-content-between align-items-center mb-2">
            <span class="badge bg-primary-subtle text-primary border border-primary-subtle badge-level">
                @Model.Level
            </span>
            <span class="text-muted small">
                <i class="bi bi-play-circle me-1 text-primary"></i>@Model.LessonCount bài học
            </span>
        </div>
        <h5 class="card-title fw-bold text-truncate mb-2">@Model.Title</h5>
        <p class="card-text text-muted small flex-grow-1" style="display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">
            @Model.Description
        </p>
        <hr class="my-3 text-muted opacity-25">
        <div class="d-flex justify-content-between align-items-center">
            <div>
                @if (Model.IsFree)
                {
                    <span class="fw-bold text-success fs-5">Miễn phí</span>
                }
                else
                {
                    <span class="fw-bold text-primary fs-5">@Model.Price.ToString("N0") đ</span>
                }
            </div>
            <a asp-action="Details" asp-route-id="@Model.Id" class="btn btn-outline-primary btn-sm rounded-pill px-3">
                Chi tiết <i class="bi bi-arrow-right"></i>
            </a>
        </div>
    </div>
</div>
```

---

### 📋 2. Tạo View Thêm Mới: `EduFlyUp.Web/Views/Courses/Create.cshtml`

```cshtml
@model EduFlyUp.Web.Models.Courses.CourseCreateViewModel

@{
    ViewData["Title"] = "Tạo khóa học mới";
}

<div class="row justify-content-center">
    <div class="col-lg-8">
        <div class="card card-modern p-4 p-md-5 shadow-sm">
            <div class="d-flex align-items-center gap-2 mb-4">
                <a asp-action="Index" class="btn btn-outline-secondary btn-sm rounded-circle">
                    <i class="bi bi-arrow-left"></i>
                </a>
                <h3 class="fw-bold mb-0">Tạo Khóa Học Mới</h3>
            </div>

            <form asp-action="Create" method="post">
                @Html.AntiForgeryToken()

                <!-- Validation Summary (Hiện nếu có lỗi chung) -->
                <div asp-validation-summary="ModelOnly" class="alert alert-danger" role="alert"></div>

                <div class="mb-3">
                    <label asp-for="Title" class="form-label fw-semibold"></label>
                    <input asp-for="Title" class="form-control form-control-lg rounded-3" placeholder="Nhập tên khóa học..." />
                    <span asp-validation-for="Title" class="text-danger small"></span>
                </div>

                <div class="mb-3">
                    <label asp-for="Description" class="form-label fw-semibold"></label>
                    <textarea asp-for="Description" class="form-control rounded-3" rows="4" placeholder="Mô tả nội dung khóa học..."></textarea>
                    <span asp-validation-for="Description" class="text-danger small"></span>
                </div>

                <div class="row g-3 mb-3">
                    <div class="col-md-6">
                        <label asp-for="Price" class="form-label fw-semibold"></label>
                        <div class="input-group">
                            <input asp-for="Price" type="number" class="form-control rounded-start-3" />
                            <span class="input-group-text">VNĐ</span>
                        </div>
                        <span asp-validation-for="Price" class="text-danger small"></span>
                    </div>

                    <div class="col-md-6">
                        <label asp-for="Level" class="form-label fw-semibold"></label>
                        <select asp-for="Level" asp-items="Html.GetEnumSelectList<EduFlyUp.Domain.Enums.CourseLevel>()" class="form-select rounded-3"></select>
                        <span asp-validation-for="Level" class="text-danger small"></span>
                    </div>
                </div>

                <div class="mb-3">
                    <label asp-for="ThumbnailUrl" class="form-label fw-semibold"></label>
                    <input asp-for="ThumbnailUrl" class="form-control rounded-3" placeholder="https://example.com/image.jpg" />
                    <span asp-validation-for="ThumbnailUrl" class="text-danger small"></span>
                </div>

                <div class="form-check form-switch mb-4">
                    <input asp-for="IsPublished" class="form-check-input" type="checkbox" />
                    <label asp-for="IsPublished" class="form-check-label fw-semibold"></label>
                </div>

                <div class="d-flex justify-content-end gap-2">
                    <a asp-action="Index" class="btn btn-outline-secondary px-4">Hủy bỏ</a>
                    <button type="submit" class="btn btn-primary px-4">
                        <i class="bi bi-save me-1"></i>Lưu khóa học
                    </button>
                </div>
            </form>
        </div>
    </div>
</div>

@section Scripts {
    <!-- Bật kiểm tra lỗi ngay trên trình duyệt (Client-side validation) -->
    <partial name="_ValidationScriptsPartial" />
}
```

---

## Task 2.6 — Phân Hệ Quản Trị với Areas (Admin Area)

### 📋 1. Cấu trúc thư mục Areas

Tạo thư mục trong `EduFlyUp.Web`:

```
EduFlyUp.Web/
└── Areas/
    └── Admin/
        ├── Controllers/
        │   └── DashboardController.cs
        └── Views/
            ├── _ViewStart.cshtml
            └── Dashboard/
                └── Index.cshtml
```

### 📝 2. `DashboardController.cs`

```csharp
using EduFlyUp.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Areas.Admin.Controllers;

[Area("Admin")] // Bắt buộc để nhận diện Area
public class DashboardController : Controller
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public IActionResult Index()
    {
        var courses = _unitOfWork.Courses.GetAll().ToList();

        ViewBag.TotalCourses = courses.Count;
        ViewBag.PublishedCourses = courses.Count(c => c.IsPublished);
        ViewBag.TotalLessons = _unitOfWork.Lessons.GetAll().Count();

        return View(courses);
    }
}
```

### 📝 3. Đăng ký Area Route trong `Program.cs`

```csharp
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

---

## Task 2.7 — Custom Middleware Đo Thời Gian Request

Tạo file: `EduFlyUp.Web/Middleware/RequestTimingMiddleware.cs`:

```csharp
using System.Diagnostics;

namespace EduFlyUp.Web.Middleware;

public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        _logger.LogInformation("⚡ [PERF] {Method} {Path} thực thi trong {Elapsed} ms",
            context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds);
    }
}
```

---

## ✅ Checklist Nghiệm Thu Phase 2

- [ ] Toàn bộ code giao diện nằm 100% trong thư mục **`Views/`** (chuẩn MVC).
- [ ] Mọi form sử dụng **Tag Helpers** (`asp-for`, `asp-action`, `asp-validation-for`).
- [ ] Bấm nút Submit khi form trống ➡️ Hiển thị thông báo lỗi màu đỏ **ngay lập tức** (Client-side validation).
- [ ] Thêm mới khóa học thành công ➡️ Redirect về Index và hiện thông báo xanh của `TempData["SuccessMessage"]`.
- [ ] Khóa học hiển thị đẹp mắt qua Partial View `_CourseCard.cshtml`.
- [ ] Truy cập `/Admin/Dashboard` ➡️ Mở trang quản trị viên với số liệu thống kê từ SQL Server.
