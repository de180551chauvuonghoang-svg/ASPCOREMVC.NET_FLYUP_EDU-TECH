# UC-40 Quản lý mã giảm giá

## Nghiệp vụ

Giảng viên quản lý mã của mình. Mã có thể áp dụng cho một khóa học do mình sở hữu hoặc toàn bộ khóa học của mình (CourseId=null). Quyền sở hữu chấp nhận Identity ID hoặc username/email cũ; mã mới luôn lưu InstructorId là Identity ID từ tài khoản đăng nhập.

- Mã được trim và chuyển hoa: 3–40 ký tự A–Z, số, dấu gạch ngang/gạch dưới. Mã duy nhất toàn hệ thống, không phân biệt hoa/thường; mã đã tắt vẫn giữ tên để bảo toàn lịch sử.
- Giảm từ 1% đến 90%, giới hạn lượt dùng lớn hơn 0. Khi sửa không được giảm giới hạn xuống dưới UsedCount và không nhận UsedCount từ form.
- Thời gian kết thúc sau thời gian bắt đầu và sau hiện tại. Lưu UTC; giao diện nhập/hiển thị giờ Việt Nam UTC+7.
- Mã có lượt dùng không đổi Code, DiscountPercent hoặc CourseId; có thể điều chỉnh thời hạn, MaxUsage và bật/tắt. Không xóa vĩnh viễn mã; dùng tắt mã để bảo toàn lịch sử.
- Không bật lại mã hết hạn hoặc hết lượt trước khi điều chỉnh điều kiện. Mã chưa bắt đầu có thể bật nhưng chỉ áp dụng từ StartDate.
- Form sửa/bật/tắt mang version từ timestamp hiện tại. Form cũ phải tải lại thay vì ghi đè thay đổi mới.

## Tích hợp

BusinessObjects → CouponDAO singleton → ICouponRepository/CouponRepository scoped → ICouponService/CouponService. Repository dùng AppDbContext scoped để cùng cấu hình với Identity; service chịu trách nhiệm chuẩn hóa/kiểm tra đầu vào. Không thêm migration hoặc thay schema đã có.

Các mutation quản lý mã dùng transaction Serializable và SQL Server sp_getapplock độc quyền với resource `EduFlyUp.CouponManagement`, owner Transaction, timeout 10 giây. Đọc lại coupon và khóa học sau khi lấy khóa; chỉ map trường cho phép, giữ owner, UsedCount và IsActive khi sửa. Kiểm tra trùng mã trong cùng transaction nên hai create qua luồng này không thể cùng lưu một code.

Do schema gốc chưa có unique index trên Code, mọi đường tạo/sửa mã mới phải đi qua contract này. Tích hợp checkout UC-17/UsedCount cần phối hợp cùng khóa transaction khi thay UsedCount/MaxUsage và chỉ nhận coupon đúng owner/khóa học, đang bật, trong thời hạn và còn lượt. UC-40 chưa triển khai checkout, tính tiền đơn hàng hoặc tăng UsedCount. Không giả lập lượt dùng trên giao diện quản lý.

## Kiểm tra phần nghiệp vụ

Ngày 30/10/2026: 149/149 test ứng dụng đạt, 0 lỗi/0 bỏ qua, gồm các test SQL Server thật trên instance phát triển `.\MSSQLSERVER01` và database GUID riêng. Test mới kiểm tra trùng mã đồng thời từ hai context, chuẩn hóa code, khóa học ngoài quyền, ownership cũ/mới, giữ lượt sử dụng, điều kiện sửa mã đã dùng, version cũ, bật mã hết hạn, search và paging. Chưa có giao diện ở commit nghiệp vụ này.
