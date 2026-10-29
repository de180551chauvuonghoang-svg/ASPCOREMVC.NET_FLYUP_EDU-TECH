# Kiểm tra nền trước khi triển khai Phân hệ B

- Ngày kiểm tra: 29/10/2026 (Asia/Saigon).
- Người phụ trách: Dương Thái Duy.
- Nhánh: `codex/duong-thai-duy-instructor-b`.
- Mã nguồn gốc: `origin/main`, commit `07e17e4ede03c730830ea5a20e2a5c68198912e9`.
- Phạm vi tiếp theo: UC-24, UC-35, UC-36, UC-37, UC-40 và phần giảng viên của UC-38.

## Kết quả thực tế

Chạy từ thư mục gốc repository trên Windows, dùng .NET SDK 10.0.401 với target framework .NET 8.

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build EduFlyUp/EduFlyUp.slnx --configuration Release --nologo` | Thành công, 0 cảnh báo, 0 lỗi |
| `dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release --no-build --no-restore --nologo` | 85 đạt, 0 lỗi, 3 bỏ qua; tổng 88 |
| `dotnet test tools/AdminDemo.Tests/AdminDemo.Tests.csproj --configuration Release --no-restore --nologo` | 11 đạt, 0 lỗi, 0 bỏ qua |

Ba test SQL Server bị bỏ qua do chưa cấu hình biến môi trường `FLYUP_TEST_SQLSERVER`: hai test bảo vệ trạng thái tài khoản Admin và một test aggregate dashboard. Xem `README.md` trong thư mục kiểm thử để chạy với SQL Server phát triển riêng. Kết quả trên chưa xác nhận persistence, transaction hoặc concurrency trên SQL Server thật, cũng chưa xác nhận giao diện hay startup với database của nhóm.

Lần chạy AdminDemo.Tests ban đầu gặp CS2012 vì chạy đồng thời với build solution và ghi cùng file đầu ra. Chạy lại tuần tự đã đạt đủ 11 test. Các lần kiểm tra tiếp theo cần chạy tuần tự khi dùng chung project phụ thuộc.

Lệnh `dotnet --info` báo lỗi khởi tạo thành phần Windows workload installer; các lệnh build và test nêu trên vẫn hoàn tất thành công.

Không sửa mã chức năng, tạo migration hoặc chạy cập nhật database trong bước kiểm tra này. Bộ test ứng dụng có dùng host kiểm thử và dữ liệu InMemory.

## Quy trình làm việc

Triển khai từng phần nhỏ có ý nghĩa, kiểm tra phù hợp, commit với mô tả tiếng Việt rõ ràng và push lên nhánh riêng trước khi chuyển sang phần tiếp theo. Nếu kiểm tra hoặc push thất bại, xử lý và ghi nhận kết quả trước khi tiếp tục.
