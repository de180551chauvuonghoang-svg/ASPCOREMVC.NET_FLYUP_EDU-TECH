# Kiểm thử nền phân quyền Admin

Chạy tại thư mục gốc repository với .NET SDK hỗ trợ `net8.0` và NuGet khả dụng:

```powershell
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release
```

Project dùng ASP.NET Core TestServer, Identity store thật trên EF Core InMemory và repository course/enrollment giả lập. Không cần khởi động Web hoặc SQL Server; không chạy migration/seed của ứng dụng. Bộ kiểm thử có 49 trường hợp ở thời điểm bàn giao bước 1.

## Các quy tắc đã kiểm tra

- Đăng nhập active/inactive, cookie tồn tại sau khóa/xóa user/đổi stamp, mở lại tài khoản, persistent cookie và lockout do sai mật khẩu.
- Guest và role sai không truy cập Admin; AccessDenied hoạt động; logout có anti-forgery; không redirect tới URL ngoài.
- Instructor không công khai khóa học bằng POST giả mạo, không sửa/xóa khóa học người khác và không thấy khóa học seed/all làm fallback.
- Admin vẫn có quyền đổi publication qua Create/Edit hiện có. Khi Instructor lưu nội dung, đường cập nhật riêng không ghi `IsPublished`, kể cả trường hợp Admin vừa ẩn course.
- Course ẩn phân biệt Enrollment Active/Completed với Cancelled và user/course khác; course công khai không đồng nghĩa có quyền học.
- Route ID không bị form ID ghi đè; thiếu anti-forgery bị từ chối; input và link quản lý trên HTML đúng quyền; form Edit/Delete độc lập.

## Contract cho các bước tiếp theo

### Khóa/mở tài khoản

`AddActiveAccountProtection()` đăng ký SignInManager và cookie events. Mỗi request có cookie đọc lại user và security stamp từ Identity store. Nếu user không còn tồn tại hoặc `IsActive=false`, principal bị từ chối và cookie trình duyệt bị xóa. Request cần xác thực chuyển về Login với thông báo; public content vẫn mở được như Guest. Request đã được xác thực trước khi khóa không bị hủy ngược.

Khi triển khai mutation khóa ở bước 3, phải lưu `IsActive=false` và đổi security stamp qua API Identity trong cùng thao tác được kiểm soát, kiểm tra kết quả lưu/concurrency. Không chỉ đổi bool: một bản sao cookie cũ chưa được gửi trong thời gian khóa có thể dùng lại sau mở khóa nếu stamp vẫn giữ nguyên. Mở khóa không tạo phiên mới; yêu cầu đăng nhập lại. Giữ `IsActive` riêng với lockout do sai mật khẩu, đồng thời bổ sung quy tắc không tự khóa/không khóa Admin cuối cùng.

Cookie validation kiểm tra stamp mỗi request để phát hiện thay đổi ngay; Identity vẫn thực hiện chu kỳ renewal của principal. Đây là thêm truy vấn theo phiên có xác thực, không phải cơ chế online-presence.

### Quyền khóa học và bài học

Inject `ICourseAccessService` khi thêm endpoint:

| Hàm | Mục đích |
| --- | --- |
| `CanManageCourse` | Admin hoặc Instructor sở hữu; hiện ownership lưu username/email, chưa phải Identity ID |
| `CanViewCourseAsync` | Metadata: published hoặc có quyền học/quản lý |
| `CanLearnCourseAsync` | Admin/owner hoặc user có Enrollment Active/Completed cho đúng course |

`Courses/Details/{id}` trả 404 khi không có quyền đọc course ẩn; không đưa Content/VideoUrl vào model công khai. Endpoint lesson/player chưa tồn tại trong bước này. Người triển khai Learner phải nối `CanLearnCourseAsync` trước khi trả nội dung, không dùng quyền xem metadata hoặc `IsEnrolledAsync` cũ vốn chưa lọc Cancelled. Nếu có preview, định nghĩa quyền preview riêng trước khi mở nội dung.

Instructor lưu nội dung qua `UpdateCourseContentAsync` → `UpdateContentAsync`; DAO chỉ map field nội dung, không sửa owner/publication/lesson graph. Không thay bằng cập nhật toàn entity từ form. Publication của Admin hiện vẫn đi qua Create/Edit; action Enable/Disable riêng thuộc bước 2.

## Giới hạn xác minh

Release build và 49 ca kiểm thử đã pass. Chưa xác minh persistence/transaction/constraint trên SQL Server và chưa kiểm tra hiển thị/JavaScript trong browser thật. Test giả lập tình huống Admin ẩn đồng thời chỉ kiểm tra đường cập nhật được chọn; không thay thế thử concurrency trên database. Không có thay đổi schema hoặc migration ở bước 1.
