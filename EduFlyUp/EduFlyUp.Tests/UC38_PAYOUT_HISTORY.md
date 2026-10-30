# UC-38 — Bước đầu: lịch sử yêu cầu rút tiền

Instructor vào **Lịch sử rút tiền** ở sidebar. GET `/InstructorPayouts` chỉ lấy PayoutRequest có InstructorId bằng Identity ID hoặc username/email cũ của phiên đăng nhập, không nhận owner từ query. Role Admin/Student/Teacher và guest không được vào endpoint Instructor này.

Hiển thị mã yêu cầu, số tiền VNĐ với hai chữ số thập phân, trạng thái Pending/Approved/Rejected (Chờ xử lý/Đã duyệt/Bị từ chối), ngày gửi/ngày xử lý theo UTC+7 và AdminNote đã lưu. Approved là trạng thái trong dữ liệu, trang không đối soát hoặc xác minh giao dịch ngân hàng. DTO không chứa BankName/AccountNumber/AccountHolderName. Razor encode ghi chú; no-store cho response.

Lịch sử mới nhất trước, CreatedAt rồi Id giảm dần; Count/Skip/Take tại SQL Server, 10 dòng/trang. Trang ngoài phạm vi được giới hạn. Hỗ trợ bộ lọc trạng thái trước Count/Skip/Take, giữ trạng thái khi chuyển trang; bấm Lọc đưa về trang đầu, Đặt lại bỏ bộ lọc. Có thông báo riêng chưa có lịch sử và không khớp bộ lọc. Query sai kiểu/enum ngoài miền trả 400.

Repository/context scoped, DAO singleton, service kiểm tra định danh. Không thêm migration/package, không thay đổi các bản ghi rút tiền qua giao diện này. Count và danh sách không cam kết snapshot chung khi dữ liệu được cập nhật đồng thời.

## Phần còn thiếu

Đây mới là phần xem lịch sử của UC-38. Chưa có gửi yêu cầu rút tiền, tính số dư khả dụng/doanh thu 70%, đối soát giao dịch hoặc xử lý yêu cầu của Admin. Không tính doanh thu bằng giá khóa hiện tại nhân số ghi danh; cần nguồn giao dịch/thanh toán và quy tắc số dư trước khi mở chức năng tạo yêu cầu.

UC-37 thống kê đánh giá cũng chưa có nguồn: đã fetch và kiểm tra mô hình hiện tại, main, mergecodetaihoang và domain-interfaces; chưa có Review/Rating trong nguồn đã kiểm tra. Không tạo số sao hay đánh giá giả.

## Kiểm tra 31/10/2026

- 185/185 test ứng dụng với SQL Server thật đạt, 0 bỏ qua. 11/11 test AdminDemo đạt; build Release 0 lỗi/0 cảnh báo.
- SQL: dual identity, phạm vi cá nhân, xếp thứ tự ổn định khi trùng ngày, đủ hai trang, giảng viên rỗng/định danh thiếu và bộ lọc cả ba trạng thái.
- HTTP: chặn role ngoài Instructor/guest; query instructorId giả không đổi owner; định dạng tiền/ngày UTC+7, encode AdminNote, không lộ tài khoản ngân hàng hoặc yêu cầu người khác, no-store, page clamp, empty và 400 cho query sai.
- Demo Kestrel với database GUID riêng đạt đủ 8 nhóm regression Admin. Chỉ thêm 12 yêu cầu mẫu vào database demo đã kiểm tra tên GUID; không chèn hoặc sửa database ứng dụng của nhóm. Trình duyệt xác nhận 12 yêu cầu/2 trang, chuyển trang 2 có hai dòng, lọc Rejected còn 4 yêu cầu/trang 1 và phản hồi Admin hiển thị đúng. Không console warning/error trong luồng đã xem.
- Ảnh `.admin-demo/uc38-payout-history-proof.jpg` giữ cục bộ. Server demo đã dừng sau kiểm tra.

Ba commit riêng: dữ liệu lịch sử theo quyền; màn hình lịch sử; bộ lọc trạng thái.
