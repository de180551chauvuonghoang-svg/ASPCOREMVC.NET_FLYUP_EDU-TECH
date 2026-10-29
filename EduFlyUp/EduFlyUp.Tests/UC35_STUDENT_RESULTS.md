# UC-35 Học viên và kết quả học tập

## Danh sách ghi danh

Instructor vào **Học viên của tôi** ở sidebar, chọn khóa học để xem danh sách ghi danh. `/InstructorStudents/Courses` chỉ liệt kê khóa học sở hữu (chấp nhận InstructorId là Identity ID hoặc username/email cũ). `/InstructorStudents?courseId={id}` hiển thị họ tên, email, thời gian ghi danh, trạng thái và ngày hoàn thành nếu đã lưu, phân trang 10 dòng. Gồm Active, Completed và Cancelled để giữ lịch sử ghi danh.

Repository dùng AppDbContext scoped, DAO singleton và service riêng. Ownership được kiểm tra trong truy vấn database; course ngoài quyền hoặc không tồn tại trả 404. Trang chỉ cho role Instructor; không đưa PasswordHash, SecurityStamp hoặc dữ liệu xác thực vào DTO. Có Cache-Control no-store và Razor encode nội dung hồ sơ. Tài khoản học viên đã xóa vẫn giữ dòng ghi danh, hiển thị trạng thái thiếu hồ sơ, không làm mất bản ghi.

Count, sort và Skip/Take chạy tại SQL Server. Sắp xếp theo thời gian ghi danh rồi ID để phân trang ổn định. Thời gian hiển thị theo UTC+7. Không có migration hoặc endpoint chỉnh sửa tài khoản/ghi danh trong module báo cáo.

Danh sách hỗ trợ tìm theo họ tên/email (trim, không phân biệt hoa/thường) và lọc Active/Completed/Cancelled. Điều kiện lọc áp dụng trước Count/Skip/Take tại database. Giữ courseId/search/status khi chuyển trang; trang vượt phạm vi được giới hạn. Query filter sai kiểu hoặc enum ngoài phạm vi trả 400. Có trạng thái rỗng khi chưa ghi danh hoặc không khớp bộ lọc.

Hai mốc đầu đã kiểm tra với SQL Server thật: 168/168 test sau danh sách và 171/171 sau bộ lọc, không có test bị bỏ qua. Test HTTP kiểm tra role, ownership, HTML encode, no-store và không lộ PasswordHash; test SQL kiểm tra join hồ sơ, tài khoản bị thiếu, phân trang và phối hợp bộ lọc.

## Phạm vi dữ liệu

Repo đã có Enrollment và QuizAttempt (Score, Passed, StartedAt, CompletedAt). Chưa có bảng hoàn thành từng bài học và đáp án từng câu của lần thi. Không suy diễn tỷ lệ tiến độ từ trạng thái Enrollment, không tạo đáp án hoặc dữ liệu thi giả. Các phần thiếu sẽ được hiển thị rõ khi nối màn hình kết quả.

## Lịch sử điểm thi

Từ danh sách học viên bấm **Xem kết quả**. GET `/InstructorStudents/Details/{enrollmentId}?courseId={courseId}` kiểm tra đồng thời khóa học sở hữu, bản ghi ghi danh và courseId trước khi đọc lịch sử. Không dùng UserId do client cung cấp để truy vấn kết quả.

Lịch sử chỉ lấy QuizAttempt của đúng học viên và quiz thuộc khóa học đã chọn, phân trang 10 dòng theo StartedAt/ID. Hiển thị tên đề, CAT/tiêu chuẩn, ngày bắt đầu/nộp, điểm và Passed đã lưu. Không tính lại Passed từ cấu hình điểm đạt hiện tại của Quiz; không tự gắn % hoặc mẫu số cho Score khi chưa có hợp đồng thang điểm từ phần User.

Lần chưa nộp (CompletedAt=null) hiện **Chưa nộp**, không hiển thị Score/Passed tạm và không được tính vào số lần đã nộp/số lần đạt/lần nộp gần nhất. Summary thực hiện aggregate trong database; bộ số liệu không cam kết snapshot chung với truy vấn phân trang khi có dữ liệu mới đồng thời. Học viên chưa có attempt có trạng thái rỗng, không tạo điểm giả.

Tiến độ bài học và đáp án từng câu hiện rõ **Chưa có dữ liệu**. UC-35 mới hoàn thành phần đọc ghi danh và kết quả đã có; chưa thể nghiệm thu tỷ lệ tiến độ/chi tiết đáp án cho đến khi phần User cung cấp dữ liệu nguồn. Không thêm migration hoặc can thiệp chức năng học/nộp bài của phần User.

## Kiểm tra giao diện và SQL

Ngày 30/10/2026: 173/173 test ứng dụng đạt trên SQL Server thật, không bỏ qua. Test lịch sử kiểm tra không lẫn học viên/khóa học, không nhận enrollmentId của khóa khác, aggregate chỉ tính bài đã nộp, phân trang và ẩn điểm tạm. HTTP dùng Razor/cookie thật; test SQL gọi repository/DAO production.

Đã chạy demo Web/Kestrel và startup/migrations với database GUID riêng; đủ 8 nhóm tích hợp Admin đạt. Bổ sung 3 attempt chỉ vào database demo để kiểm tra UI: đạt 85,5, chưa đạt 45 và một lần chưa nộp có điểm tạm. Qua trình duyệt đã kiểm tra chọn khóa học sở hữu → tìm theo email chữ hoa và lọc Đang học → xem kết quả. UI hiển thị đúng 3 lần làm bài/2 đã nộp/1 đạt, không lộ điểm tạm; các nguồn chưa có hiện rõ thông báo. Không có console warning/error trong luồng đã xem. Ảnh bằng chứng giữ ở `.admin-demo/`, không commit dữ liệu cá nhân hoặc mật khẩu demo.

Kiểm tra cuối: build Release 0 lỗi/0 cảnh báo; 173/173 test ứng dụng trên SQL Server thật, 11/11 test AdminDemo đạt. Report demo có Passed=true, BrowserVerified=true và đủ 8 nhóm đạt. Nhánh có các commit riêng cho danh sách, bộ lọc và lịch sử điểm; không thay đổi database ứng dụng của nhóm.
