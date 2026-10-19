# Kiểm thử phần Admin

Chạy tại thư mục gốc repository với .NET SDK hỗ trợ `net8.0` và NuGet khả dụng:

```powershell
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release
```

Project dùng ASP.NET Core TestServer và Identity store thật trên EF Core InMemory. Các test bước 1 dùng repository course/enrollment giả lập; test Admin bước 2–4 chạy repository Admin thật trên InMemory. Có test biên dịch truy vấn bằng provider SQL Server mà không mở kết nối DB. Bộ kiểm thử có 88 trường hợp: 83 từ bước 1–3, 4 test thông thường và 1 test SQL Server thêm ở bước 4. Chạy mặc định: 85 pass, 3 SQL test skip nếu chưa đặt `FLYUP_TEST_SQLSERVER`.

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
- User list lọc tên/email, role và IsActive, phân trang ổn định, giữ filter và đọc nhiều role theo batch cho trang hiện tại.
- User details lấy course theo username/email, enrollment theo Identity ID; encode hồ sơ và không đưa password hash/security stamp/token vào DTO hoặc HTML.
- Khóa → mở yêu cầu đăng nhập lại; bản sao cookie cũ vẫn bị từ chối sau mở. Giữ role, mật khẩu, lockout do nhập sai, khóa học và enrollment; POST giả mạo field bị bỏ qua.
- Không tự khóa, từ chối actor đã mất role Admin, từ chối role/Guest, xử lý ID/token/filter sai và lỗi ghi Identity. Hai yêu cầu khóa chéo được kiểm tra lại actor sau khi chờ khóa.

- Dashboard đếm dữ liệu rỗng, user nhiều role, IsActive độc lập với login lockout, tổng bài học gồm course ẩn; các thẻ mở đúng URL lọc.
- Qua HTTP có cookie/token thật: khóa/mở user, ẩn/bật course rồi tải dashboard, đối chiếu cả 7 số liệu sau từng thao tác.
- Truy vấn dashboard biên dịch thành SQL aggregate, không join role hoặc tải nội dung entity để đếm.

## Contract cho các bước tiếp theo

### Khóa/mở tài khoản

`AddActiveAccountProtection()` đăng ký SignInManager và cookie events. Mỗi request có cookie đọc lại user và security stamp từ Identity store. Nếu user không còn tồn tại hoặc `IsActive=false`, principal bị từ chối và cookie trình duyệt bị xóa. Request cần xác thực chuyển về Login với thông báo; public content vẫn mở được như Guest. Request đã được xác thực trước khi khóa không bị hủy ngược.

Mutation khóa bước 3 lưu `IsActive=false` cùng security stamp mới qua `UserManager.UpdateSecurityStampAsync`; activation dùng `UserManager.UpdateAsync`, không reset lockout bảo mật. Không thay bằng cập nhật bool trực tiếp: bản sao cookie cũ có thể sống lại sau mở khóa nếu stamp giữ nguyên. Gửi lặp trạng thái đã đúng không ghi lại hoặc đổi stamp. Kết quả Identity thất bại trả thông báo conflict, lỗi DB trả thông báo thử lại; transaction chưa commit được rollback.

Cookie validation kiểm tra stamp mỗi request để phát hiện thay đổi ngay; Identity vẫn thực hiện chu kỳ renewal của principal. Đây là thêm truy vấn theo phiên có xác thực, không phải cơ chế online-presence.

`AdminUserService` giữ quy tắc nghiệp vụ, `AdminUserRepository` dùng UserManager/AppDbContext scoped từ cùng DI scope. `SqlServerAdminUserStatusLock` bắt đầu transaction Serializable, lấy `sp_getapplock` Exclusive với owner Transaction, timeout 10 giây và resource `EduFlyUp.AdminUserStatus`. Sau khi lấy khóa, xóa snapshot tracked từ cookie validation rồi đọc lại actor: phải còn active và có role Admin. Sau đó kiểm tra target, tự khóa và số Admin hoạt động trước khi lưu/commit. Hai request khóa chéo không thể cùng dùng trạng thái actor cũ. Khóa được quản lý tại DB nên dùng được qua nhiều app instance; không dựa vào semaphore trong production.

Mọi đường đổi trạng thái hoặc thay đổi role Admin trong tương lai phải phối hợp cùng guard/quy tắc này. Đường quản lý hiện tại không đổi role hoặc xóa tài khoản. Production guard từ chối provider không phải SQL Server; test/preview InMemory dùng lock riêng chỉ để kiểm tra thứ tự service/HTTP, không chứng minh transaction SQL.

| Method | Route | Hành vi |
| --- | --- | --- |
| GET | `/Admin/Users` | Search, Role, IsActive, Page; 10 tài khoản/trang; page được giới hạn, filter sai kiểu trả 400 |
| GET | `/Admin/UserDetails/{id}` | Hồ sơ, roles, course đã tạo, enrollment và status; user không tồn tại trả 404 |
| POST | `/Admin/DeactivateUser/{id}` | Khóa, đổi stamp, ngăn tự khóa và Admin cuối cùng |
| POST | `/Admin/ActivateUser/{id}` | Mở, giữ role/mật khẩu/lockout, không cấp phiên mới |

POST lấy target ID từ route, actor ID từ cookie; không nhận entity/role/field nhạy cảm từ form. ReturnUrl chỉ cho URL local. Log actor ID/target ID/trạng thái/kết quả để debug; chưa phải dữ liệu UC-51 Login History.

### Chạy hai kiểm thử SQL Server

Chạy từ phiên Windows có quyền kết nối SQL Server phát triển và quyền tạo/xóa database:

```powershell
$env:FLYUP_TEST_SQLSERVER = 'Server=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;'
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release --filter FullyQualifiedName~AdminUserSqlServerTests
```

Mỗi test tạo database mới tên `FlyUpAdminStatusTest_<GUID>`, dùng EnsureCreated và xóa đúng database do fixture tạo khi kết thúc. InitialCatalog đầu vào được thay bằng tên mới; AttachDBFilename bị loại bỏ để không đính kèm file DB sẵn có. Không chạy migration/seed hoặc xóa database của ứng dụng. Dùng SQL Server phát triển riêng; không trỏ tới server production.

Hai test chạy production guard với các DI scope/context độc lập: Admin A/B cùng khóa chéo; giả lập commit thất bại sau khi Identity đã lưu để kiểm tra rollback cả IsActive và security stamp. Test không có biến môi trường sẽ báo skip rõ ràng. Chỉ khi hai test pass trên SQL Server mới xác nhận nghiệm thu transaction/concurrency bước 3.

### Dashboard UC-44 và kiểm thử SQL Server

`GET /Admin` và `/Admin/Index` chỉ dành cho Admin đang hoạt động. `/Admin/Dashboard` chuyển về `/Admin`. Controller trả `AdminDashboardModel` chứa `AdminDashboardStats`; repository dùng AppDbContext từ DI chung với Identity. Hai truy vấn group/count lấy tổng và trạng thái của user/course; truy vấn thứ ba đếm lesson. Empty group trả 0. User nhiều role được tính một lần; IsActive không đo online và không phụ thuộc LockoutEnd. Không dùng cache: tải lại trang sau mutation để đọc số liệu mới. Ba truy vấn không tạo snapshot chung xuyên bảng.

Kiểm thử SQL bước 4 chạy aggregate trên database mới rỗng rồi thêm user/course/lesson; khóa/mở user qua production guard, ẩn/bật course qua repository thật và đọc lại số liệu từ scope khác:

```powershell
$env:FLYUP_TEST_SQLSERVER = 'Server=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;'
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release --filter FullyQualifiedName~AdminDashboardSqlServerTests
```

Phải có **1 passed, 0 failed, 0 skipped**. Test dùng chung fixture database GUID của bước 3, không đọc/ghi database ứng dụng. Có thể đặt biến môi trường rồi chạy toàn bộ suite để chạy cả 3 SQL test. TestServer/InMemory và ToQueryString không thay thế việc chạy test này trên SQL Server.

Giao diện dashboard đã kiểm tra bằng browser với dữ liệu mẫu InMemory: 14 user (10 hoạt động, 4 khóa), 13 course (9 công khai, 4 ẩn), 13 lesson. Thẻ khóa tài khoản/khóa học ẩn mở đúng danh sách gồm 4 dòng; cả 6 URL liên kết được kiểm tra qua HTTP. Bố cục mặc định và viewport 390×844 hiển thị đủ các thẻ, không tràn ngang; không ghi nhận console warning/error trong luồng đã xem.

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

Release build và 85 test thông thường đã pass; lần chạy mặc định có 3 test SQL Server skip. Hai test transaction/concurrency bước 3 đã pass khi chạy từ phiên Windows của người dùng. Phiên tự động trước đó không mở được kết nối do SQL Client báo không hỗ trợ encryption mà server yêu cầu. Kiểm thử aggregate SQL bước 4 cần được chạy riêng theo hướng dẫn trên trước nghiệm thu. Không giảm yêu cầu encryption để vượt qua giới hạn môi trường. Browser các bước trước đã kiểm tra với dữ liệu mẫu InMemory: quản lý course/user, chi tiết, confirm khóa → mở, thông báo, bộ lọc và bố cục điện thoại. Biên dịch query bằng SQL Server provider không thay thế kiểm thử persistence/transaction trên server thật. Không có thay đổi schema/migration ở bước 1–4.

Trước nghiệm thu trên DB của nhóm, dùng database phát triển riêng, cấu hình `ConnectionStrings__DefaultConnection`, chạy từ `EduFlyUp/EduFlyUp.Web` để DAO và Identity cùng cấu hình. Startup tự migrate/seed. Kiểm tra Admin bật/ẩn course, tải lại `/Courses`, thử URL course ẩn với Guest/owner/học viên Active/Completed/Cancelled, đối chiếu lesson/enrollment trước và sau thao tác. Test InMemory và biên dịch SQL không thay thế bước này, cũng không thay thế thử concurrency trên database.
