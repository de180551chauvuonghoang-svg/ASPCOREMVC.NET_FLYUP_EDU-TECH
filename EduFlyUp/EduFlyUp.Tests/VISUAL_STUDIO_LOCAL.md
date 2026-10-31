# Chạy bằng Visual Studio với SQL Server trên máy cá nhân

1. Mở `EduFlyUp/EduFlyUp.slnx`, chọn startup project `EduFlyUp.Web`.
2. Tạo `EduFlyUp/EduFlyUp.Web/appsettings.Local.json`, thay tên instance bằng SQL Server đang chạy trên máy:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\MSSQLSERVER01;Database=EduFlyUp_Duy_Local;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
```

3. Chọn profile `http` rồi bấm F5. Địa chỉ mặc định: `http://localhost:5298`.
4. Ứng dụng tự chạy migration và seed trên database đã chọn. Dùng database local riêng để thử; không trỏ vào database nhóm khi chưa thống nhất.
5. Tài khoản giảng viên mẫu trong DataSeeder: `instructor@eduflyup.vn` / `Instructor@123`. Đây là tài khoản demo local.

File Local chỉ được đọc ở môi trường Development, đã bỏ qua trong Git và không đưa vào bản publish. Cả DbContext được inject và DAO tạo `new AppDbContext()` đều đọc file này khi chạy từ thư mục Web. Biến môi trường `ConnectionStrings__DefaultConnection` có độ ưu tiên cao hơn file Local; tránh đặt hai nơi khác nhau. Khi chạy CLI có tham số cấu hình, nên dùng biến môi trường cho connection string để DAO và Web cùng đọc được.

Trang chủ hiển thị được chưa chứng minh SQL hoạt động: cần mở Khóa học và đăng nhập Instructor để kiểm tra dữ liệu. Database mới chỉ có dữ liệu seed; danh sách học viên có thể trống. Doanh thu/rút tiền vẫn báo chưa có nguồn giao dịch đã xác minh cho đến khi tích hợp nguồn thanh toán thật.

## Kết quả kiểm tra ngày 01/11/2026

- Mở solution bằng Visual Studio Community 2026, startup `EduFlyUp.Web`, Debug, profile `http`, bấm F5.
- Trước khi cấu hình local: trang chủ mở được nhưng `/Courses` báo SqlException error 26 vì không có instance `SQLEXPRESS` trên máy.
- Sau khi cấu hình: SQL Server `MSSQLSERVER01` tạo database thử riêng có tiền tố `FlyUpVsDuy_`; migration và seed thành công. Trang Khóa học đọc được hai khóa mẫu, đăng nhập Instructor thành công.
- Trình duyệt đã mở Studio (2 khóa/3 bài), Mã giảm giá, khóa học và danh sách học viên, Doanh thu, Lịch sử rút tiền và trang yêu cầu rút tiền. Không còn lỗi kết nối SQL. Dữ liệu học viên/mã giảm giá/yêu cầu rút tiền đang trống theo database seed mới.
- Trang Doanh thu hiển thị chưa có dữ liệu đã xác minh; trang yêu cầu rút tiền hiển thị chưa xác minh số dư và chưa cho gửi yêu cầu. Chưa kiểm chứng doanh thu thực tế vì chưa có nguồn thanh toán tích hợp.
- Chạy lại `EduFlyUp.Tests` ở Release với SQL Server thật: 241/241 đạt, không bỏ qua test. MSBuild xác nhận file Local có `CopyToPublishDirectory=Never`; Git xác nhận file này được ignore.
