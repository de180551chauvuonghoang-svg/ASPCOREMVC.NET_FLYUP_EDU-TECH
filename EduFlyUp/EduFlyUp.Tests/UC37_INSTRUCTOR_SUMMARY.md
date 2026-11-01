# UC-37 — Bước đầu: thống kê ghi danh trên Studio

Đây là phần triển khai từng bước của UC-37, chưa phải toàn bộ dashboard thống kê.

## Đã có

- Tổng lượt ghi danh trong tất cả khóa học sở hữu, gồm khóa lưu trữ và các trạng thái Active/Completed/Cancelled.
- Số học viên không trùng theo UserId qua các khóa học. Bao gồm cả ghi danh đã hủy và hồ sơ đã mất; đây là số mã học viên trong lịch sử ghi danh, không phải số tài khoản đang hoạt động.
- Số lượt đang học, hoàn thành và đã hủy. Không suy ra tiến độ bài học từ Completed.
- Các thẻ trên `/Instructor/Dashboard` và liên kết tới danh sách học viên. Bộ lọc khóa học bên dưới không tác động tổng thống kê; phạm vi ghi ngay cạnh số liệu. Không có ghi danh thì hiện số 0 và thông báo rỗng.

Truy vấn aggregate chạy tại SQL Server, chỉ lấy khóa học của giảng viên theo Identity ID hoặc username/email cũ. Không tải toàn bộ hồ sơ học viên để đếm. Service kiểm tra định danh. Admin vẫn dùng phạm vi quản lý có sẵn; không đưa thẻ thống kê cá nhân Instructor vào phiên Admin. Dashboard trả no-store.

## Chưa làm trong đợt này

Thống kê đánh giá và doanh thu thực nhận 70% sẽ triển khai ở các bước sau. Chưa có nguồn giao dịch/thanh toán để tính doanh thu thực nhận; không lấy giá hiện tại nhân số ghi danh để thay thế. Không thêm migration hoặc sửa database ứng dụng của nhóm.

## Kiểm tra 31/10/2026

- 179/179 test ứng dụng với SQL Server thật đạt, không bỏ qua.
- SQL kiểm tra hai kiểu ownership, học viên ghi danh nhiều khóa, khóa lưu trữ, ba trạng thái, hồ sơ thiếu, khóa ngoài quyền, giảng viên chưa có dữ liệu và định danh rỗng.
- HTTP/Razor kiểm tra thẻ số liệu đúng phạm vi, không thay đổi theo bộ lọc khóa học, liên kết danh sách, no-store, trạng thái rỗng và phiên Admin.
- Demo Kestrel với database GUID riêng đạt 8 nhóm regression Admin. Trình duyệt Instructor hiển thị đúng 3 ghi danh/3 học viên/1 đang học/1 hoàn thành/1 hủy; tìm khóa học không khớp trả danh sách rỗng nhưng tổng vẫn giữ nguyên. Không có console warning/error trong luồng đã xem.
- Ảnh kiểm tra ở `.admin-demo/uc37-enrollment-summary-proof.jpg` (cục bộ, không commit).

Các bước được kiểm tra, commit tiếng Việt và push riêng: truy vấn tổng và distinct; phân tách trạng thái; hiển thị Studio.

## Bước tiếp theo: khóa học đông học viên

Studio bổ sung bảng tối đa 5 khóa học sở hữu có học viên Active hoặc Completed, gồm cả khóa lưu trữ. Đếm distinct UserId theo từng khóa, không tính Cancelled; bỏ các khóa có số đếm bằng 0. Sắp xếp StudentCount giảm dần rồi CourseId tăng dần để ổn định khi bằng nhau. Query Count/Distinct/OrderBy/Take chạy tại SQL Server, không lấy toàn bộ hồ sơ học viên để xếp hạng.

Phạm vi của bảng top khác tổng lịch sử ghi danh phía trên: tổng lịch sử có Cancelled, bảng top không có Cancelled. Có mô tả ngay trên UI. Bộ lọc danh sách khóa học không thay đổi bảng top. Khi chưa có khóa phù hợp, hiện thông báo rỗng. Phiên Admin không hiển thị bảng này.

Nút **Xem ghi danh** mở báo cáo của đúng courseId. Báo cáo đích giữ toàn bộ lịch sử, gồm cả Cancelled; ví dụ top đếm 2 học viên đang học/hoàn thành nhưng danh sách ghi danh có 3 người nếu 1 người đã hủy. Endpoint đích tự kiểm tra ownership, sửa courseId sang khóa ngoài quyền trả 404. Tiêu đề khóa và aria-label được Razor encode.

Kiểm tra sau bước này: 180/180 test ứng dụng trên SQL Server thật, 0 bỏ qua; 11/11 test AdminDemo; build Release 0 lỗi/0 cảnh báo. SQL kiểm tra top 5 trong 7 khóa, thứ tự bằng điểm, cả ID/email cũ, khóa lưu trữ, khóa trống/chỉ hủy, khóa ngoài quyền có nhiều học viên hơn và định danh rỗng. HTTP kiểm tra dữ liệu top, bộ lọc không đổi bảng, empty/Admin, encode tiêu đề và liên kết đích đúng quyền.

Demo database GUID riêng đạt đủ 8 nhóm regression Admin. Trình duyệt xác nhận top DEMO course a có 2 học viên khi bộ lọc không khớp khóa nào; nút Xem ghi danh mở đúng khóa với 3 dòng lịch sử (có một đã hủy). Không có console warning/error trong luồng đã xem. Ảnh `.admin-demo/uc37-popular-courses-proof.jpg` lưu cục bộ. Ba commit riêng: truy vấn xếp hạng; giao diện bảng; liên kết xem ghi danh và kiểm tra luồng.
