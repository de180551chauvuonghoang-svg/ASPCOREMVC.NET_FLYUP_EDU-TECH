# UC-35 Học viên và kết quả học tập

## Danh sách ghi danh

Instructor vào **Học viên của tôi** ở sidebar, chọn khóa học để xem danh sách ghi danh. `/InstructorStudents/Courses` chỉ liệt kê khóa học sở hữu (chấp nhận InstructorId là Identity ID hoặc username/email cũ). `/InstructorStudents?courseId={id}` hiển thị họ tên, email, thời gian ghi danh, trạng thái và ngày hoàn thành nếu đã lưu, phân trang 10 dòng. Gồm Active, Completed và Cancelled để giữ lịch sử ghi danh.

Repository dùng AppDbContext scoped, DAO singleton và service riêng. Ownership được kiểm tra trong truy vấn database; course ngoài quyền hoặc không tồn tại trả 404. Trang chỉ cho role Instructor; không đưa PasswordHash, SecurityStamp hoặc dữ liệu xác thực vào DTO. Có Cache-Control no-store và Razor encode nội dung hồ sơ. Tài khoản học viên đã xóa vẫn giữ dòng ghi danh, hiển thị trạng thái thiếu hồ sơ, không làm mất bản ghi.

Count, sort và Skip/Take chạy tại SQL Server. Sắp xếp theo thời gian ghi danh rồi ID để phân trang ổn định. Thời gian hiển thị theo UTC+7. Không có migration hoặc endpoint chỉnh sửa tài khoản/ghi danh trong module báo cáo.

## Phạm vi dữ liệu

Repo đã có Enrollment và QuizAttempt (Score, Passed, StartedAt, CompletedAt). Chưa có bảng hoàn thành từng bài học và đáp án từng câu của lần thi. Không suy diễn tỷ lệ tiến độ từ trạng thái Enrollment, không tạo đáp án hoặc dữ liệu thi giả. Các phần thiếu sẽ được hiển thị rõ khi nối màn hình kết quả.
