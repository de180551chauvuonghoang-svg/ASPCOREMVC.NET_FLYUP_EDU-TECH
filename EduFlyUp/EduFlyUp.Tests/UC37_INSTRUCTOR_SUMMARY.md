# UC-37 — Bước đầu: thống kê ghi danh trên Studio

Đây là phần triển khai từng bước của UC-37, chưa phải toàn bộ dashboard thống kê.

## Đã có

- Tổng lượt ghi danh trong tất cả khóa học sở hữu, gồm khóa lưu trữ và các trạng thái Active/Completed/Cancelled.
- Số học viên không trùng theo UserId qua các khóa học. Bao gồm cả ghi danh đã hủy và hồ sơ đã mất; đây là số mã học viên trong lịch sử ghi danh, không phải số tài khoản đang hoạt động.
- Số lượt đang học, hoàn thành và đã hủy. Không suy ra tiến độ bài học từ Completed.
- Các thẻ trên `/Instructor/Dashboard` và liên kết tới danh sách học viên. Bộ lọc khóa học bên dưới không tác động tổng thống kê; phạm vi ghi ngay cạnh số liệu. Không có ghi danh thì hiện số 0 và thông báo rỗng.

Truy vấn aggregate chạy tại SQL Server, chỉ lấy khóa học của giảng viên theo Identity ID hoặc username/email cũ. Không tải toàn bộ hồ sơ học viên để đếm. Service kiểm tra định danh. Admin vẫn dùng phạm vi quản lý có sẵn; không đưa thẻ thống kê cá nhân Instructor vào phiên Admin. Dashboard trả no-store.

## Chưa làm trong đợt này

Danh sách khóa đông học viên, thống kê đánh giá và doanh thu thực nhận 70% sẽ triển khai ở các bước sau. Chưa có nguồn giao dịch/thanh toán để tính doanh thu thực nhận; không lấy giá hiện tại nhân số ghi danh để thay thế. Không thêm migration hoặc sửa database ứng dụng của nhóm.

## Kiểm tra 31/10/2026

- 179/179 test ứng dụng với SQL Server thật đạt, không bỏ qua.
- SQL kiểm tra hai kiểu ownership, học viên ghi danh nhiều khóa, khóa lưu trữ, ba trạng thái, hồ sơ thiếu, khóa ngoài quyền, giảng viên chưa có dữ liệu và định danh rỗng.
- HTTP/Razor kiểm tra thẻ số liệu đúng phạm vi, không thay đổi theo bộ lọc khóa học, liên kết danh sách, no-store, trạng thái rỗng và phiên Admin.
- Demo Kestrel với database GUID riêng đạt 8 nhóm regression Admin. Trình duyệt Instructor hiển thị đúng 3 ghi danh/3 học viên/1 đang học/1 hoàn thành/1 hủy; tìm khóa học không khớp trả danh sách rỗng nhưng tổng vẫn giữ nguyên. Không có console warning/error trong luồng đã xem.
- Ảnh kiểm tra ở `.admin-demo/uc37-enrollment-summary-proof.jpg` (cục bộ, không commit).

Các bước được kiểm tra, commit tiếng Việt và push riêng: truy vấn tổng và distinct; phân tách trạng thái; hiển thị Studio.
