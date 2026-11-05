# Thanh toán và lưu giao dịch — triển khai từng phần

## Phần 1: cấu trúc lưu trữ (05/11/2026)

Phạm vi tài liệu gốc: UC-15 giỏ hàng, UC-16 mua khóa, UC-17 áp dụng mã, UC-18 lịch sử giao dịch và UC-19 đăng ký khóa miễn phí. Tài liệu gợi ý Casso/VietQR nhưng nhóm chưa chốt cổng thanh toán; schema hiện không phụ thuộc SDK/nhà cung cấp cụ thể.

- `CheckoutOrders`: người mua, mã công khai Guid, key chống gửi lại theo người mua, tổng giá/giảm giá/phải trả bằng VND, trạng thái, hạn thanh toán và thời điểm trả tiền.
- `CheckoutOrderItems`: mỗi khóa xuất hiện một lần trong đơn; lưu snapshot tên khóa, giảng viên hưởng doanh thu, giá, giảm giá và mã giảm giá. Không liên kết cascade từ Course/Coupon/User để lịch sử không mất khi dữ liệu gốc thay đổi hoặc bị xóa.
- `PaymentTransactions`: tiền vào, nhà cung cấp và mã giao dịch duy nhất theo nhà cung cấp, thời điểm phát sinh và xác minh. `Received` chỉ là đã ghi nhận đầu vào; không đồng nghĩa tiền đã xác minh. Chưa dùng cho hoàn tiền hoặc lệnh chuyển tiền ra.
- Rowversion trên đơn/giao dịch để các service tiếp theo phát hiện cập nhật đồng thời. Không cascade xóa các dòng đơn/giao dịch khi xóa đơn.
- Check constraint giữ số tiền không âm, chiết khấu không vượt giá, số phải trả bằng giá trừ giảm, trạng thái hợp lệ, thông tin coupon đủ bộ và thời điểm xác minh tương ứng trạng thái. Unique index bảo vệ key gửi lại, mã đơn và mã giao dịch.

Migration `20261105163935_AddCheckoutOrdersAndPaymentTransactions` chỉ thêm ba bảng và index/constraint; không đổi dữ liệu bảng cũ. Chỉ chạy migration trên database GUID của fixture khi kiểm thử; chưa cập nhật database nhóm.

Đây là phần lưu trữ, chưa có endpoint checkout/webhook, nút mua hàng hoặc nguồn thu nhập mới. Các service sau phải kiểm tra người mua/khóa sở hữu, giá từ server, tổng đơn khớp các dòng, hạn thanh toán, xác thực thông báo cổng, số tiền/đơn vị tiền, trạng thái chuyển tiếp và xử lý lặp. Check constraint không thay thế xác thực giao dịch. Không nhận trường Paid/Verified từ trình duyệt để cấp khóa học hoặc mở rút tiền.

Nguồn doanh thu Instructor vẫn là `UnavailableInstructorSettlementSource` cho tới khi hoàn thiện xác minh và đối soát; không suy ra thu nhập từ bảng đơn Pending.

## Kiểm tra phần 1

`CheckoutPersistenceSqlServerTests` có sáu test với SQL Server thật: nâng cấp từ schema cũ giữ khóa học; lưu snapshot Pending; trùng giao dịch trên hai đơn; key gửi lại và khóa trùng trong đơn; số tiền/trạng thái/coupon không hợp lệ; chặn xóa mất lịch sử. Có kiểm tra transaction Verified phải có thời điểm xác minh và giao dịch Received không tự tạo enrollment. Kiểm tra EF xác nhận model khớp migration.

Kết quả ngày 05/11/2026: toàn bộ **247/247 test ứng dụng đạt, không bỏ qua test**, gồm sáu test lưu trữ thanh toán mới trên SQL Server thật.

## Các phần tiếp theo (mỗi phần kiểm tra → commit → push riêng)

1. Tạo đơn ở server, chốt giá/snapshot, kiểm tra quyền mua và chống gửi lại; xử lý khóa miễn phí.
2. Áp dụng mã giảm giá và phối hợp khóa `EduFlyUp.CouponManagement` khi giữ/chốt lượt dùng.
3. Giao diện checkout và đơn hàng thuộc người mua.
4. Kết nối cổng sau khi nhóm chốt: tạo yêu cầu/QR, xác thực callback hoặc webhook; không tin return URL là bằng chứng đã thanh toán.
5. Xác nhận thanh toán và kích hoạt ghi danh trong transaction, chống xử lý lặp; lịch sử giao dịch của người mua.
6. Nối nguồn giao dịch đã xác minh sang báo cáo/70%/rút tiền với khóa `EduFlyUp.PayoutManagement`; kiểm thử đầy đủ trước khi mở nguồn.

Hoàn tiền/đối soát và xử lý rút tiền của Admin cần phạm vi riêng trước khi nghiệm thu dòng tiền hoàn chỉnh.
