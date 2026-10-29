# UC-35 Học viên và kết quả học tập

## Danh sách ghi danh

Instructor vào **Học viên của tôi** ở sidebar, chọn khóa học để xem danh sách ghi danh. `/InstructorStudents/Courses` chỉ liệt kê khóa học sở hữu (chấp nhận InstructorId là Identity ID hoặc username/email cũ). `/InstructorStudents?courseId={id}` hiển thị họ tên, email, thời gian ghi danh, trạng thái và ngày hoàn thành nếu đã lưu, phân trang 10 dòng. Gồm Active, Completed và Cancelled để giữ lịch sử ghi danh.

Repository dùng AppDbContext scoped, DAO singleton và service riêng. Ownership được kiểm tra trong truy vấn database; course ngoài quyền hoặc không tồn tại trả 404. Trang chỉ cho role Instructor; không đưa PasswordHash, SecurityStamp hoặc dữ liệu xác thực vào DTO. Có Cache-Control no-store và Razor encode nội dung hồ sơ. Tài khoản học viên đã xóa vẫn giữ dòng ghi danh, hiển thị trạng thái thiếu hồ sơ, không làm mất bản ghi.

Count, sort và Skip/Take chạy tại SQL Server. Sắp xếp theo thời gian ghi danh rồi ID để phân trang ổn định. Thời gian hiển thị theo UTC+7. Không có migration hoặc endpoint chỉnh sửa tài khoản/ghi danh trong module báo cáo.

Danh sách hỗ trợ tìm theo họ tên/email (trim, không phân biệt hoa/thường) và lọc Active/Completed/Cancelled. Điều kiện lọc áp dụng trước Count/Skip/Take tại database. Giữ courseId/search/status khi chuyển trang; trang vượt phạm vi được giới hạn. Query filter sai kiểu hoặc enum ngoài phạm vi trả 400. Có trạng thái rỗng khi chưa ghi danh hoặc không khớp bộ lọc.

Hai mốc đầu đã kiểm tra với SQL Server thật: 168/168 test sau danh sách và 171/171 sau bộ lọc, không có test bị bỏ qua. Test HTTP kiểm tra role, ownership, HTML encode, no-store và không lộ PasswordHash; test SQL kiểm tra join hồ sơ, tài khoản bị thiếu, phân trang và phối hợp bộ lọc.

## Phạm vi dữ liệu

Repo đã có Enrollment và QuizAttempt (Score, Passed, StartedAt, CompletedAt). Chưa có bảng hoàn thành từng bài học và đáp án từng câu của lần thi. Không suy diễn tỷ lệ tiến độ từ trạng thái Enrollment, không tạo đáp án hoặc dữ liệu thi giả. Các phần thiếu sẽ được hiển thị rõ khi nối màn hình kết quả.
