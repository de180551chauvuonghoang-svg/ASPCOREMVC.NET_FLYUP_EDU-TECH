# Demo tích hợp Admin trên SQL Server

Công cụ bước 5 chạy Web thật qua Kestrel, Identity cookie, MVC/Razor, services, repositories và DAO hiện có. Không thay provider bằng InMemory hoặc giả lập repository. Dành cho máy phát triển có .NET 8 runtime/SDK hỗ trợ net8.0, SQL Server Express và quyền tạo database bằng Windows Authentication.

Chạy từ thư mục gốc repository:

```powershell
dotnet build tools/AdminDemo/AdminDemo.csproj --configuration Release
dotnet run --project tools/AdminDemo/AdminDemo.csproj --configuration Release --no-build -- --repo . --server '.\SQLEXPRESS' --keep-open
```

Mỗi lần chạy tạo database mới `FlyUpAdminDemo_<GUID>`. Công cụ không nhận tên database có sẵn, kiểm tra tên chưa tồn tại trước khi migrate/seed và không xóa database tự động. Dữ liệu được giữ để demo. Dùng migration và startup seed thật; Admin mặc định của startup seed được vô hiệu hóa **chỉ trong database demo**, để còn đúng hai Admin demo hoạt động cho thử nghiệm đồng thời.

App chỉ lắng nghe `127.0.0.1` trên cổng trống. Process Web chạy từ thư mục `EduFlyUp.Web` với `ConnectionStrings__DefaultConnection` trỏ về database mới: Identity từ DI và DAO tạo context riêng phải cùng đọc dữ liệu. Công cụ tắt đúng process Web do nó khởi tạo khi kết thúc; không dừng các app khác. Với `--keep-open`, nhấn Enter để kết thúc server. Chế độ `--wait-for-stop-file` giữ server cho kiểm tra browser đến khi file được ghi tại đường dẫn `StopFile` trong session; dùng cho quy trình bàn giao có điều khiển.

## Dữ liệu và kết quả

Thư mục `.admin-demo/` được Git bỏ qua:

- `session.json`: URL, PID, database, ID khóa học, tài khoản và mật khẩu demo ngẫu nhiên. Đây là file cục bộ có thông tin đăng nhập; không commit hoặc đưa vào báo cáo dùng chung.
- `report.json`: thời gian, database, URL và trạng thái từng nhóm kiểm tra; không chứa mật khẩu/cookie/hash/stamp.
- `web.log`: log của Web để tìm nguyên nhân nếu startup hoặc HTTP thất bại.

Demo gồm Admin A/B; Instructor A/B có khóa học riêng; Teacher; Student Active, Completed, Cancelled, chưa ghi danh và bị khóa; 12 Student bổ sung để thử phân trang. Có 13 khóa học demo, khóa công khai/ẩn, miễn phí/có phí, ba cấp độ và khóa chưa có lesson, cộng dữ liệu startup seed. Một course nháp được tạo thêm qua HTTP để kiểm tra POST giả mạo publication. Các tài khoản thuộc `demo.example.test`; mật khẩu chỉ sinh cho lần demo này.

## Tám nhóm kiểm tra

1. Startup/migration và dữ liệu SQL được đọc qua cả DAO public và dashboard dùng DI; 7 số liệu đối chiếu trực tiếp với DB.
2. Guest bị yêu cầu đăng nhập; Student/Instructor/Teacher bị từ chối Admin; user inactive không nhận cookie.
3. User/course filter, phân trang, chi tiết owner/ghi danh, trạng thái rỗng và filter sai.
4. Student và Instructor có phiên hiện tại: khóa, request tiếp theo bị từ chối, login mới bị chặn; mở khóa, bản sao cookie cũ không sống lại và login mới được phép. Giữ hồ sơ, mật khẩu, role và lockout.
5. Instructor không sửa/xóa course của người khác; Create không nhận publication hoặc owner giả mạo từ form.
6. Admin ẩn/bật course: list public và URL trực tiếp đúng quyền; Active/Completed vẫn xem được course ẩn; Cancelled/người ngoài bị từ chối; Admin/owner vẫn quản lý được. Instructor lưu nội dung không bật lại course. Giữ lesson/enrollment và đối chiếu dashboard sau thay đổi; gửi lặp không đổi timestamp.
7. GET mutation, POST thiếu token, ID sai, tự khóa Admin và khóa lặp; phục hồi trạng thái demo.
8. Hai Admin khóa chéo qua HTTP đồng thời: còn đúng một Admin hoạt động; Admin còn lại mở lại tài khoản kia để tiếp tục demo.

Chỉ coi chạy HTTP thành công khi `Passed=true`, đủ 8 nhóm đều pass và không có `Error` trong report; process trả exit code 0. Với chế độ `--wait-for-stop-file`, `BrowserVerified` còn false trong lúc giữ Web mở, chỉ thành true sau tín hiệu xác nhận. Wrapper phải đợi trạng thái này trước push. Build hoặc việc tạo được DB chưa chứng minh nghiệm thu. Khi lỗi, xem report/log, sửa rồi chạy một lần mới trên database demo mới.

Kiểm thử hồi quy riêng cho công cụ, không cần SQL Server:

```powershell
dotnet test tools/AdminDemo.Tests/AdminDemo.Tests.csproj --configuration Release
```

Có 8 ca kiểm tra redirect tương đối/tuyệt đối từ Kestrel, từ chối redirect khác origin/cổng/đường dẫn và cơ chế chờ tín hiệu browser. Redirect được đối chiếu cùng origin và đúng path; query ReturnUrl được giữ theo ứng dụng. Số ca này tách biệt với 88 test ứng dụng và 8 nhóm HTTP/SQL.

## Kiểm tra hồi quy và demo thủ công

Trước demo, chạy toàn bộ test suite với SQL Server để ba test SQL không bị skip:

```powershell
$env:FLYUP_TEST_SQLSERVER = 'Server=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;'
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release
```

Baseline bước 4: 88 test; cần 88 pass, 0 fail, 0 skip. Ba test SQL tạo/xóa database GUID riêng; database demo ở trên được giữ. Không hạ encryption để vượt lỗi môi trường.

Sau khi công cụ báo pass, mở URL `/Admin` trong session và đăng nhập Admin A. Kiểm tra dashboard, danh sách và hai trang phân trang; xem chi tiết Student Active và khóa học A. Dùng cửa sổ riêng đăng nhập Student Active/Instructor A, khóa từ Admin rồi tải lại trang có xác thực; mở khóa và đăng nhập lại. Ẩn khóa học A, đối chiếu Guest với học viên Active/Completed/Cancelled, rồi bật lại. Thao tác chỉ trên database demo. Dùng ứng dụng trong cùng lần chạy vì cookie demo không được giữ qua các lần tạo database.

Phạm vi xác minh là nhánh Admin hiện tại. Việc các thành viên Account/Instructor/Learner đã nhận và tích hợp thay đổi chung cần xác nhận từ nhóm; công cụ không tự merge nhánh khác. Endpoint học/player chưa có trong nhánh này, nên kiểm tra Enrollment ở đây xác nhận quyền metadata và quy tắc service, không khẳng định đã demo player. Thanh toán, workflow review và hỗ trợ không thuộc 9 UC.
