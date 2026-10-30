# UC-36 — Xuất học viên và kết quả Excel

Tại **Học viên của tôi → chọn khóa học**, bấm **Xuất học viên & điểm (.xlsx)**. File lấy toàn bộ các trang của bộ lọc tên/email và trạng thái đã áp dụng; bộ lọc chưa bấm Tìm kiếm chưa được áp dụng. GET `/InstructorStudents/Export?courseId={id}&search={keyword}&status={status}` chỉ dành cho Instructor và khóa học sở hữu (Identity ID hoặc username/email cũ).

## Nội dung workbook

- `Thong tin`: khóa học, bộ lọc, phạm vi và các giới hạn dữ liệu.
- `Hoc vien`: mã ghi danh, mã học viên, họ tên, email, ngày ghi danh, trạng thái và ngày hoàn thành đã lưu.
- `Da hoan thanh`: các dòng Completed trong cùng bộ lọc. Đây là trạng thái Enrollment, không phải suy luận từ bài học.
- `Lich su diem`: từng lần làm bài đúng học viên, đúng đề thuộc khóa học, loại đề, thời gian, điểm và Đạt/Trượt đã lưu.
- `Diem cao nhat`: điểm cao nhất và số lần đã nộp, tính riêng từng đề thi và ghi danh. Không so sánh các đề có thang điểm khác nhau.

Thời gian là giá trị ngày giờ Excel theo UTC+7, điểm là số; header cố định, có bảng/bộ lọc khi có dữ liệu. Báo cáo rỗng vẫn có sheet và tiêu đề. Chuỗi do người dùng nhập được ghi bằng Value dạng text, không dùng FormulaA1. Không có PasswordHash hoặc trường xác thực trong DTO/workbook. Response no-store, file đính kèm có tên chỉ dùng courseId và MIME xlsx.

Lần chưa nộp có ô điểm trống và trạng thái Chưa nộp; không được tính điểm cao nhất. Tiến độ bài học ghi Chưa có dữ liệu vì chưa có nguồn hoàn thành từng bài. Không tự gán 100% cho Enrollment.Completed, không tạo đáp án hoặc điểm giả. UC-36 đáp ứng xuất dữ liệu hiện có; cột tiến độ thực tế còn phụ thuộc nguồn dữ liệu của phần User.

ClosedXML 0.105.0 được thêm ở Web, không cần cài Microsoft Excel trên server. Tham khảo API: https://docs.closedxml.io/en/v0.105.0/api/index.html . Không thêm migration hoặc sửa database của nhóm. Tối đa 10.000 ghi danh/50.000 dòng lịch sử; vượt trả HTTP 422 kèm hướng dẫn thu hẹp bộ lọc, không xuất file bị cắt thiếu dữ liệu. Các truy vấn đọc không cam kết cùng một snapshot nếu dữ liệu thay đổi đồng thời trong lúc xuất.

## Kiểm tra ngày 31/10/2026

- 177/177 test ứng dụng đạt với SQL Server thật, 0 bỏ qua; 11/11 test AdminDemo đạt.
- SQL kiểm tra toàn bộ 13 ghi danh vượt một trang, bộ lọc, hồ sơ thiếu, khóa học rỗng, ownership và lịch sử thi đúng phạm vi.
- HTTP thực hiện đăng nhập, gọi endpoint tải xuống rồi mở lại byte xlsx bằng ClosedXML; kiểm tra 5 sheet, 12 học viên đủ trang, Completed, bộ lọc kết hợp, ngày UTC+7, điểm dạng số, bài chưa nộp, highest theo đề, không lộ dữ liệu ngoài khóa học/tài khoản xác thực, chuỗi bắt đầu bằng dấu bằng giữ dạng text và không chứa formula.
- Kiểm tra role Instructor, guest/Student/Admin bị chặn; khóa học khác/mất trả 404, bộ lọc sai trả 400, vượt giới hạn trả 422.
- Chưa chạy thêm kiểm tra bằng trình duyệt hoặc mở bằng ứng dụng Excel cho lần thay đổi này.
