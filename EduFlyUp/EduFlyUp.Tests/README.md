# Kiểm thử phần Admin

Chạy tại thư mục gốc repository với .NET SDK hỗ trợ `net8.0` và NuGet khả dụng:

```powershell
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release
```

Project dùng ASP.NET Core TestServer và Identity store thật trên EF Core InMemory. Các test bước 1 dùng repository course/enrollment giả lập; test Admin bước 2 chạy repository Admin thật trên InMemory. Một test dùng provider SQL Server để biên dịch các truy vấn thực tế, không mở kết nối database. Không chạy migration/seed của ứng dụng. Bộ kiểm thử hiện có 65 trường hợp: 49 từ bước 1 và 16 thêm ở bước 2.

## Các quy tắc đã kiểm tra

- Đăng nhập active/inactive, cookie tồn tại sau khóa/xóa user/đổi stamp, mở lại tài khoản, persistent cookie và lockout do sai mật khẩu.
- Guest và role sai không truy cập Admin; AccessDenied hoạt động; logout có anti-forgery; không redirect tới URL ngoài.
- Instructor không công khai khóa học bằng POST giả mạo, không sửa/xóa khóa học người khác và không thấy khóa học seed/all làm fallback.
- Admin vẫn có quyền đổi publication qua Create/Edit hiện có. Khi Instructor lưu nội dung, đường cập nhật riêng không ghi `IsPublished`, kể cả trường hợp Admin vừa ẩn course.
- Course ẩn phân biệt Enrollment Active/Completed với Cancelled và user/course khác; course công khai không đồng nghĩa có quyền học.
- Route ID không bị form ID ghi đè; thiếu anti-forgery bị từ chối; input và link quản lý trên HTML đúng quyền; form Edit/Delete độc lập.
- Admin list lọc đồng thời theo tiêu đề, giảng viên, cấp độ, publication; phân trang ổn định theo CreatedAt/Id, giữ bộ lọc khi chuyển trang, giới hạn page và kết quả rỗng.
- Tên/email giảng viên được tìm theo username hiện có; dữ liệu seed không khớp user vẫn có fallback và vẫn lọc được.
- Enable/Disable yêu cầu Admin còn hoạt động, POST và token; ID sai trả 404, returnUrl ngoài bị bỏ qua. Gửi lặp không toggle và không đổi timestamp nếu trạng thái đã đúng.
- Mutation bỏ qua các field ngoài ID trên route; giữ mô tả, giá, owner, bài học và enrollment. Quyền đọc của Guest/học viên sau mutation vẫn theo contract bước 1.
- Chi tiết Admin đọc course ẩn, bài học theo thứ tự, có empty state; Content được encode, VideoUrl chỉ hiển thị văn bản.

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

Instructor lưu nội dung qua `UpdateCourseContentAsync` → `UpdateContentAsync`; DAO chỉ map field nội dung, không sửa owner/publication/lesson graph. Không thay bằng cập nhật toàn entity từ form. Admin có action Enable/Disable riêng; Create/Edit cũ của Admin vẫn giữ quyền publication.

### Tầng dữ liệu quản lý course Admin

`AdminCourseService` chuẩn hóa bộ lọc và gọi `IAdminCourseRepository`. Repository mới dùng `AppDbContext` scoped từ DI; lọc, count, sort, skip/take và lesson count được thực thi tại DB. Các DAO hiện có không bị refactor. InstructorId vẫn là username/email; không join với Identity ID. Danh sách giảng viên bao gồm owner từ course seed và course do Admin tạo.

| Method | Route | Hành vi |
| --- | --- | --- |
| GET | `/Admin/Courses` | Search, Instructor, Level, IsPublished, Page; page size 10; page ngoài phạm vi được giới hạn; bộ lọc sai kiểu/enum trả 400 |
| GET | `/Admin/CourseDetails/{id}` | Metadata và nội dung bài học riêng cho Admin; ID không tồn tại trả 404 |
| POST | `/Admin/EnableCourse/{id}` | Đặt IsPublished=true |
| POST | `/Admin/DisableCourse/{id}` | Đặt IsPublished=false, không xóa nội dung/ghi danh |

POST kiểm tra anti-forgery. Sau khi lưu, returnUrl local được giữ để quay lại danh sách với bộ lọc hiện tại; nếu không có hoặc là URL ngoài, quay về chi tiết course.

## Giới hạn xác minh

Release build và 65 ca kiểm thử đã pass. Đã kiểm tra trình duyệt với dữ liệu mẫu InMemory: danh sách, chi tiết, công khai → confirm ẩn, thông báo, bộ lọc, bố cục desktop/mobile; console không có warning/error trong luồng đã xem. Biên dịch truy vấn bằng provider SQL Server đã pass, nhưng chưa xác minh persistence/transaction/constraint trên SQL Server: kết nối SQL Express trong phiên tự động bị chặn bởi xác thực Windows/TLS. Không có thay đổi schema hoặc migration ở bước 1/2.

Trước nghiệm thu trên DB của nhóm, dùng database phát triển riêng, cấu hình `ConnectionStrings__DefaultConnection`, chạy từ `EduFlyUp/EduFlyUp.Web` để DAO và Identity cùng cấu hình. Startup tự migrate/seed. Kiểm tra Admin bật/ẩn course, tải lại `/Courses`, thử URL course ẩn với Guest/owner/học viên Active/Completed/Cancelled, đối chiếu lesson/enrollment trước và sau thao tác. Test InMemory và biên dịch SQL không thay thế bước này, cũng không thay thế thử concurrency trên database.
