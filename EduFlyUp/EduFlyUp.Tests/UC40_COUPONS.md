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

## Giao diện và HTTP

Instructor vào **Mã giảm giá của tôi** ở sidebar. `/Coupons` tìm theo mã và phân trang 10 dòng; hiển thị khóa học, mức giảm, thời hạn theo giờ Việt Nam, lượt đã dùng/tối đa và trạng thái (đã tắt, hết hạn, hết lượt, chưa bắt đầu, đang áp dụng). Có trạng thái rỗng và nút tạo mã.

- GET/POST `/Coupons/Create`: tạo mã, áp dụng cho một khóa học sở hữu hoặc toàn bộ khóa học của mình. Server tự đặt owner, UsedCount=0 và IsActive=true.
- GET/POST `/Coupons/Edit/{id}`: đọc/sửa mã đúng chủ sở hữu; ID route phải khớp form và version phải còn mới.
- POST `/Coupons/Activate/{id}` và `/Coupons/Deactivate/{id}`: đặt trạng thái cụ thể, giữ lịch sử. GET không thay đổi dữ liệu. Không có endpoint xóa vĩnh viễn.
- Mọi POST yêu cầu anti-forgery; chỉ role Instructor truy cập. Admin không có role Instructor, Student, Teacher và Guest không vào được chức năng. Mã của người khác trả 404.
- Validation thất bại trả lại form và dữ liệu đã nhập/khóa học được phép chọn; form cũ trả 409 và có liên kết tải lại. Lỗi DB ghi log và báo thử lại. Các trường thời gian nhập theo UTC+7, không dùng timezone của server để suy diễn.

Đã kiểm tra trên Web/Kestrel và database demo SQL Server riêng: tạo mã chữ thường có khoảng trắng → lưu dạng chữ hoa, chọn đúng khóa học, sửa mức giảm lên 90% và MaxUsage, tắt → bật lại, tìm kiếm không phân biệt hoa/thường, báo lỗi code trùng và giữ dữ liệu form. Không có warning/error console ở luồng thành công (POST trùng mã chủ động trả HTTP 400). Ảnh bằng chứng nằm trong `.admin-demo/`, không commit.

HTTP TestServer dùng cookie/anti-forgery và Razor thật, fake repository để kiểm tra phân quyền, overposting, UTC+7, validation, mã trùng, form cũ và bật/tắt. Các test SQL riêng thực thi DAO/repository/service production; fake HTTP không thay thế kiểm thử transaction trên SQL Server.

Kết quả cuối ngày 30/10/2026: build Release 0 lỗi/0 cảnh báo; **162/162 test ứng dụng đạt trên SQL Server thật, 0 bỏ qua**; **11/11 test AdminDemo đạt**; bản demo riêng đạt đủ 8 nhóm tích hợp và được kiểm tra trình duyệt (report Passed=true, BrowserVerified=true). Không thêm package hoặc migration, không cập nhật database ứng dụng của nhóm.
