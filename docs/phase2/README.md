# 🚀 Phase 2 — ASP.NET Core 8 MVC Hiện Đại (Pure Modern MVC)

Tài liệu chi tiết đầy đủ từng bước thực hành cho Phase 2 chuẩn 100% ASP.NET Core MVC đã được biên soạn tại:

👉 **[Xem Hướng Dẫn Chi Tiết Phase 2 (phase2-guide.md)](./phase2-guide.md)**

---

## 📌 Các trụ cột công nghệ trong Phase 2:
1. **Kiến trúc MVC & Vòng đời HTTP Request** (Controller ➡️ ViewModel ➡️ Razor View `.cshtml`).
2. **Master Layout & Modern Design System** (Font Inter, bảng màu Indigo cao cấp, Card bo tròn đổ bóng).
3. **ViewModels & Chống Over-posting Attack** (`CourseCreateViewModel`, `CourseItemViewModel`, `CourseDetailViewModel`).
4. **Tag Helpers chính thống của ASP.NET Core** (`asp-controller`, `asp-action`, `asp-for`, `asp-validation-for`).
5. **Form Validation 2 lớp** (Client-side bằng jQuery Validation + Server-side bằng `ModelState.IsValid`).
6. **Tái sử dụng UI chuẩn MVC**:
   - **Partial Views** (`_CourseCard.cshtml`)
   - **ViewComponents**
7. **Phân hệ Quản trị với Areas** (`Areas/Admin/Controllers/DashboardController`).
8. **Custom Middleware**: Đo lường hiệu năng thời gian xử lý request.
