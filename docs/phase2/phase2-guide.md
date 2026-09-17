# 📘 Hướng Dẫn Thực Hành Phase 2 — ASP.NET Core MVC & Modern C# UI (MudBlazor)

Chào mừng bạn đến với **Phase 2** của hành trình trở thành **Senior .NET Developer**! 🚀

Theo đúng quyết định kiến trúc của bạn: **Chúng ta giữ vững tầng máy chủ ASP.NET Core MVC (Controllers, Routing, Dependency Injection, Services), nhưng 100% GIAO DIỆN sẽ được xây dựng bằng C# THUẦN TÚY thông qua Blazor Components & Thư viện MudBlazor (Material Design 3)**.

> 💡 **Lợi ích vượt trội**:
> - Không phải gõ từng thẻ HTML/CSS thủ công hay lo giao diện xấu.
> - Sử dụng trực tiếp các Component xịn sò dựng sẵn: `<MudCard>`, `<MudButton>`, `<MudDataGrid>`, `<MudForm>`, `<MudChip>`, tự động có đổ bóng, bo góc, hiệu ứng hover, animation, toast popup.
> - Viết logic tương tác trực tiếp bằng C# (EventCallback, Binding).

---

## 🎯 Mục tiêu cần đạt được trong Phase 2

1. **Hiểu cách kết hợp MVC và Blazor Server**: MVC làm router và controller, Razor Views đóng vai trò là "vỏ bọc" nhúng các **Blazor Component (C# UI)**.
2. **Master MudBlazor Design System**:
   - Sử dụng Theme, Typography, Palette màu sắc chuẩn Material Design.
   - Bật Snackbar Toast thông báo, Dialog xác nhận hoàn toàn bằng C#.
3. **Tuyệt đối không rò rỉ Entity ra UI**: Sử dụng **ViewModels** để chuyển đổi dữ liệu từ Domain sang UI.
4. **Xây dựng bộ Components Giao diện Khóa học bằng C#**:
   - `CourseCard.razor`: Thẻ hiển thị từng khóa học (ảnh bìa, giá tiền, cấp độ, số bài học).
   - `CourseListGrid.razor`: Lưới danh sách khóa học kèm thanh tìm kiếm và bộ lọc cấp độ động.
   - `CourseDetailView.razor`: Trang chi tiết khóa học kèm danh sách bài học và nút ghi danh.
   - `CourseCreateForm.razor`: Form tạo khóa học bằng MudBlazor validation.
5. **Khu vực Quản trị Admin Dashboard**: Sử dụng `<MudDataGrid>` để hiển thị bảng dữ liệu quản lý khóa học có sẵn tính năng Sort, Filter, Pagination.
6. **Custom Middleware**: Đo lường hiệu năng HTTP Request bằng C#.

---

## 🗺️ Bản đồ các Task trong Phase 2 (Kiến trúc MVC + Blazor C# UI)

```
Task 2.1 ──► Cấu hình MudBlazor & Blazor Server vào MVC (Đã thiết lập sẵn)
    │
Task 2.2 ──► Thiết kế ViewModels cho phân hệ Khóa học (Data Protection)
    │
Task 2.3 ──► Tạo Component C# hiển thị Thẻ & Danh sách Khóa học (MudCard & MudGrid)
    │
Task 2.4 ──► Tạo Component C# Form Thêm mới Khóa học (MudForm & Validation)
    │
Task 2.5 ──► Tạo Component C# Chi tiết Khóa học & Danh sách Bài học (MudTimeline)
    │
Task 2.6 ──► Xây dựng Admin Dashboard với Bảng dữ liệu thông minh (MudDataGrid)
    │
Task 2.7 ──► Viết Custom Middleware đo thời gian xử lý Request
    │
Task 2.8 ──► Tổng duyệt & Nghiệm thu giao diện Phase 2
```

---

## Task 2.1 — Cấu hình MudBlazor trong ASP.NET Core MVC

*(Bước này tôi đã cấu hình sẵn trong project của bạn)*:
1. Package `MudBlazor 9.x` đã được cài vào `EduFlyUp.Web.csproj`.
2. `Program.cs` đã đăng ký:
   ```csharp
   builder.Services.AddServerSideBlazor();
   builder.Services.AddMudServices();
   // ...
   app.MapBlazorHub();
   ```
3. `EduFlyUp.Web/_Imports.razor` đã tự động import các namespace của MudBlazor.
4. `Views/Shared/_Layout.cshtml` đã có sẵn các MudBlazor Providers:
   ```cshtml
   <component type="typeof(MudBlazor.MudThemeProvider)" render-mode="ServerPrerendered" />
   <component type="typeof(MudBlazor.MudDialogProvider)" render-mode="ServerPrerendered" />
   <component type="typeof(MudBlazor.MudSnackbarProvider)" render-mode="ServerPrerendered" />
   ```

---

## Task 2.2 — Thiết kế ViewModels (Tầng Model)

### 📋 Cấu trúc thư mục ViewModels
Tạo thư mục: `EduFlyUp.Web/Models/Courses/`

#### 📝 `CourseItemViewModel.cs`
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

#### 📝 `CourseCreateViewModel.cs`
```csharp
using System.ComponentModel.DataAnnotations;
using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Web.Models.Courses;

public class CourseCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề khóa học.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề từ 5 đến 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả khóa học.")]
    [MaxLength(2000, ErrorMessage = "Mô tả tối đa 2000 ký tự.")]
    public string Description { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    [Range(0, 100000000, ErrorMessage = "Học phí phải từ 0 đến 100.000.000 VNĐ.")]
    public decimal Price { get; set; }

    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    public bool IsPublished { get; set; } = true;
}
```

---

## Task 2.3 — Tạo Component Danh Sách Khóa Học bằng MudBlazor

Thay vì phải căn lề CSS hay viết div class Bootstrap phức tạp, bạn viết toàn bộ giao diện bằng C# Component!

### 📋 Tạo thư mục: `EduFlyUp.Web/Components/Courses/`

#### 📝 Tạo `CourseList.razor`:
```razor
@using EduFlyUp.Web.Models.Courses
@inject NavigationManager Navigation

<MudContainer MaxWidth="MaxWidth.Large" Class="my-6">
    <!-- Header & Search Bar -->
    <div class="d-flex justify-content-between align-items-center mb-6">
        <div>
            <MudText Typo="Typo.h4" Color="Color.Primary" Class="fw-bold">Khám phá Khóa học</MudText>
            <MudText Typo="Typo.subtitle1" Color="Color.Secondary">Học lập trình .NET cùng các chuyên gia hàng đầu</MudText>
        </div>
        <MudButton Variant="Variant.Filled" 
                   Color="Color.Primary" 
                   StartIcon="@Icons.Material.Filled.Add"
                   Href="/Courses/Create"
                   Elevation="3">
            Tạo khóa học mới
        </MudButton>
    </div>

    <!-- Filter & Search Input -->
    <MudGrid Class="mb-6">
        <MudItem xs="12" sm="6" md="4">
            <MudTextField @bind-Value="_searchTerm" 
                          Placeholder="Tìm kiếm khóa học..." 
                          Adornment="Adornment.Start" 
                          AdornmentIcon="@Icons.Material.Filled.Search" 
                          IconSize="Size.Medium" 
                          Variant="Variant.Outlined" 
                          Immediate="true" />
        </MudItem>
    </MudGrid>

    <!-- Course Cards Grid -->
    <MudGrid Spacing="4">
        @foreach (var course in FilteredCourses)
        {
            <MudItem xs="12" sm="6" md="4">
                <MudCard Elevation="3" Class="rounded-xl overflow-hidden hover-card h-100 d-flex flex-column">
                    <MudCardMedia Image="@course.ThumbnailUrl" Height="190" />
                    <MudCardContent Class="flex-grow-1">
                        <div class="d-flex justify-content-between align-items-center mb-2">
                            <MudChip T="string" Color="Color.Primary" Variant="Variant.Outlined" Size="Size.Small">
                                @course.Level.ToString()
                            </MudChip>
                            <MudText Typo="Typo.caption" Color="Color.Secondary">
                                <MudIcon Icon="@Icons.Material.Filled.PlayCircle" Size="Size.Small" Class="mr-1" />
                                @course.LessonCount bài học
                            </MudText>
                        </div>
                        <MudText Typo="Typo.h6" Class="fw-bold mb-2">@course.Title</MudText>
                        <MudText Typo="Typo.body2" Color="Color.Secondary" Lines="2">
                            @course.Description
                        </MudText>
                    </MudCardContent>
                    <MudCardActions Class="d-flex justify-content-between px-4 pb-4">
                        <MudText Typo="Typo.h6" Color="Color.Primary" Class="fw-bold">
                            @(course.IsFree ? "Miễn phí" : $"{course.Price:N0} đ")
                        </MudText>
                        <MudButton Variant="Variant.Text" 
                                   Color="Color.Primary" 
                                   EndIcon="@Icons.Material.Filled.ArrowForward"
                                   Href="@($"/Courses/Details/{course.Id}")">
                            Xem chi tiết
                        </MudButton>
                    </MudCardActions>
                </MudCard>
            </MudItem>
        }
    </MudGrid>
</MudContainer>

<style>
    .hover-card {
        transition: transform 0.2s ease-in-out, box-shadow 0.2s ease-in-out;
    }
    .hover-card:hover {
        transform: translateY(-4px);
        box-shadow: 0 12px 24px rgba(0,0,0,0.12) !important;
    }
</style>

@code {
    [Parameter]
    public List<CourseItemViewModel> Courses { get; set; } = new();

    private string _searchTerm = string.Empty;

    private IEnumerable<CourseItemViewModel> FilteredCourses =>
        string.IsNullOrWhiteSpace(_searchTerm)
            ? Courses
            : Courses.Where(c => c.Title.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));
}
```

---

## Task 2.4 — Nhúng Component vào MVC View

Để nhúng một Blazor Component vào bất kỳ View MVC nào, bạn chỉ cần dùng thẻ `<component>` của ASP.NET Core!

### 📋 Sửa `EduFlyUp.Web/Views/Courses/Index.cshtml`:

```cshtml
@model List<EduFlyUp.Web.Models.Courses.CourseItemViewModel>

@{
    ViewData["Title"] = "Khóa học trực tuyến";
}

<!-- Nhúng trực tiếp Blazor Component CourseList vào trang MVC -->
<component type="typeof(EduFlyUp.Web.Components.Courses.CourseList)" 
           render-mode="ServerPrerendered" 
           param-Courses="@Model" />
```

### 📋 Trong `CoursesController.cs`:
Controller giữ nguyên vai trò chuẩn MVC: lấy dữ liệu từ `IUnitOfWork` và truyền vào View:

```csharp
// GET: /Courses
public async Task<IActionResult> Index()
{
    var courses = await _unitOfWork.Courses.GetPublishedCoursesAsync();

    var viewModel = courses.Select(c => new CourseItemViewModel
    {
        Id = c.Id,
        Title = c.Title,
        Description = c.Description,
        ThumbnailUrl = string.IsNullOrEmpty(c.ThumbnailUrl) ? "https://placehold.co/600x400/594ae2/ffffff?text=Course" : c.ThumbnailUrl,
        Price = c.Price,
        Level = c.Level,
        LessonCount = c.Lessons.Count
    }).ToList();

    return View(viewModel);
}
```

---

## Task 2.5 — Tạo Component Form Thêm Mới Khóa Học (`CourseCreateForm.razor`)

### 📝 Code mẫu — `EduFlyUp.Web/Components/Courses/CourseCreateForm.razor`:

```razor
@using EduFlyUp.Web.Models.Courses
@using EduFlyUp.Domain.Enums
@inject ISnackbar Snackbar
@inject NavigationManager Navigation

<MudContainer MaxWidth="MaxWidth.Medium" Class="my-6">
    <MudPaper Elevation="4" Class="pa-6 rounded-xl">
        <MudText Typo="Typo.h5" Color="Color.Primary" Class="fw-bold mb-4">
            <MudIcon Icon="@Icons.Material.Filled.LibraryAdd" Class="mr-2" />
            Tạo Khóa Học Mới
        </MudText>

        <!-- Form truyền dữ liệu về Action POST của MVC -->
        <form method="post" action="/Courses/Create">
            <MudGrid Spacing="3">
                <MudItem xs="12">
                    <MudTextField Label="Tiêu đề khóa học" 
                                  Name="Title"
                                  Required="true" 
                                  RequiredError="Vui lòng nhập tiêu đề" 
                                  Variant="Variant.Outlined" />
                </MudItem>

                <MudItem xs="12">
                    <MudTextField Label="Mô tả chi tiết" 
                                  Name="Description"
                                  Lines="3" 
                                  Required="true" 
                                  Variant="Variant.Outlined" />
                </MudItem>

                <MudItem xs="12" sm="6">
                    <MudNumericField T="decimal" 
                                     Label="Học phí (VNĐ)" 
                                     Name="Price"
                                     Min="0" 
                                     Step="50000" 
                                     Variant="Variant.Outlined" />
                </MudItem>

                <MudItem xs="12" sm="6">
                    <MudSelect T="CourseLevel" Label="Cấp độ khóa học" Name="Level" Variant="Variant.Outlined">
                        <MudSelectItem Value="@CourseLevel.Beginner">Cơ bản (Beginner)</MudSelectItem>
                        <MudSelectItem Value="@CourseLevel.Intermediate">Trung cấp (Intermediate)</MudSelectItem>
                        <MudSelectItem Value="@CourseLevel.Advanced">Nâng cao (Advanced)</MudSelectItem>
                    </MudSelect>
                </MudItem>

                <MudItem xs="12">
                    <MudTextField Label="Link ảnh bìa (Thumbnail URL)" 
                                  Name="ThumbnailUrl" 
                                  Variant="Variant.Outlined" />
                </MudItem>

                <MudItem xs="12" Class="d-flex justify-content-end gap-3 mt-4">
                    <MudButton Variant="Variant.Outlined" Href="/Courses">Hủy bỏ</MudButton>
                    <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">
                        Lưu khóa học
                    </MudButton>
                </MudItem>
            </MudGrid>
        </form>
    </MudPaper>
</MudContainer>
```

---

## Task 2.6 — Admin Dashboard với `MudDataGrid` (Tự động Sort, Filter, Phân trang)

Trong khu vực quản trị, thay vì viết bảng HTML `<table>` thô sơ, ta dùng `<MudDataGrid>`:
- Tự động có bộ lọc tìm kiếm trên từng cột.
- Tự động sắp xếp (Sort ASC/DESC khi bấm vào tiêu đề cột).
- Phân trang (Pagination) tự động.

```razor
<MudDataGrid Items="@Courses" Filterable="true" SortMode="SortMode.Multiple" Elevation="3" Class="rounded-xl">
    <Columns>
        <PropertyColumn Property="x => x.Id" Title="Mã KH" />
        <PropertyColumn Property="x => x.Title" Title="Tên khóa học" />
        <PropertyColumn Property="x => x.Level" Title="Cấp độ" />
        <PropertyColumn Property="x => x.Price" Title="Học phí" Format="N0" />
        <TemplateColumn Title="Trạng thái">
            <CellTemplate>
                <MudChip Color="@(context.Item.IsPublished ? Color.Success : Color.Warning)" Size="Size.Small">
                    @(context.Item.IsPublished ? "Công khai" : "Bản nháp")
                </MudChip>
            </CellTemplate>
        </TemplateColumn>
    </Columns>
    <PagerContent>
        <MudDataGridPager T="CourseItemViewModel" />
    </PagerContent>
</MudDataGrid>
```

---

## ✅ Checklist Kiểm Thử Phase 2

- [ ] **Khởi chạy ứng dụng**: Bấm F5, trang web mở lên với màu sắc, font chữ và theme Material của MudBlazor.
- [ ] **Trang danh sách (`/Courses`)**: Các khóa học hiển thị dưới dạng `<MudCard>` bo tròn, đổ bóng đẹp mắt.
- [ ] **Tìm kiếm real-time**: Gõ vào ô tìm kiếm, danh sách khóa học lọc tức thì không cần tải lại trang.
- [ ] **Tạo khóa học (`/Courses/Create`)**: Form nhập liệu MudBlazor hiện đại, các trường input có hiệu ứng floating label sang trọng.
- [ ] **Admin DataGrid**: Bảng quản lý khóa học hỗ trợ Sort, Filter mượt mà bằng C#.

---

> 🎯 **Bạn thấy đấy, toàn bộ giao diện đã được "thay máu" hoàn toàn bằng C# và MudBlazor component hiện đại, không còn phải loay hoay với HTML hay CSS thủ công nữa!**
