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

## Phần 2: tạo đơn ở server và khóa miễn phí (05/11/2026)

Đã chốt hướng **Casso + VietQR** theo tài liệu nhóm. VietQR tạo mã chuyển khoản; Casso cung cấp thông báo giao dịch để server xác minh. Cần tài khoản Casso, ngân hàng nhận tiền và khóa xác thực do nhóm cấu hình khi triển khai kết nối; chưa gửi thông tin hay đăng ký dịch vụ bên ngoài trong bước này.

Nguồn chính thức đã đối chiếu:
- [Casso — tích hợp xác nhận thanh toán](https://developer.casso.vn/tai-nguyen-khac/tich-hop-xac-nhan-thanh-toan).
- [VietQR — thay đổi API tạo QR](https://www.vietqr.io/danh-sach-api/link-tao-ma-nhanh/api-tao-ma-qr): API generate cũ ngừng hỗ trợ và dừng từ 01/01/2027; không tích hợp mới vào endpoint này. Cách tạo QR sẽ dùng cơ chế đang được hỗ trợ khi làm giao diện thanh toán.

`ICheckoutService.CreateAsync` nhận claims của phiên đăng nhập và `CheckoutInput` chỉ có SubmissionKey/danh sách CourseId. Người mua, giá, giảng viên và trạng thái được xác định ở server. Chỉ tài khoản đang hoạt động với role Student trong database được tạo đơn; role trong cookie không thay thế kiểm tra database. Input tối đa 20 khóa, mã dương không trùng, key Guid khác rỗng.

Tạo đơn qua Repository/DAO scoped, Serializable và khóa `EduFlyUp.CheckoutManagement`. Khóa phải được dùng lại trong writer xác nhận thanh toán/ghi danh sau này. Nếu một writer cần cả ba khóa, thống nhất thứ tự CheckoutManagement → CouponManagement → PayoutManagement, không đảo thứ tự giữa các writer.

- Các khóa phải vừa Approved vừa IsPublished; không mua khóa của chính tài khoản. VND hiện nhận số nguyên, không tự làm tròn giá có phần lẻ; tổng tối đa 9.999.999.999.999 VND.
- Chụp giá/tên/giảng viên theo database, cộng tổng tại server; chưa áp dụng coupon ở bước này.
- Cùng key + cùng tập khóa trả lại đơn đã lưu, kể cả giá/trạng thái khóa đã đổi. Cùng key + tập khóa khác báo xung đột. Key có phạm vi người mua.
- Khóa đã Active/Completed không mua lại. Đơn Pending còn hạn của cùng người mua có khóa trùng sẽ chặn tạo đơn khác; đơn hết hạn không chặn tạo mới. Xử lý nhận tiền muộn của đơn hết hạn còn ở bước webhook/đối soát.
- Đơn có phí ở Pending, hạn 30 phút, chưa tạo giao dịch hoặc enrollment. Giỏ hỗn hợp miễn phí/có phí chờ xác nhận toàn bộ đơn.
- Đơn hoàn toàn miễn phí được Paid với số tiền 0, cấp enrollment trong cùng transaction; tái kích hoạt enrollment Cancelled thay vì chèn trùng. Không tạo giao dịch ngân hàng giả cho đơn miễn phí.
- Nếu lưu đơn lỗi thì rollback cả việc tái kích hoạt enrollment; service trả Unavailable, không trả kết quả thành công.

Đây là backend tạo đơn, chưa có controller/view/QR/webhook; chưa kích hoạt nguồn doanh thu Instructor. Không có migration hoặc package mới trong phần 2. Kiểm thử unit và SQL thật bao gồm snapshot, replay, account/role hiện tại, giới hạn tiền, toàn bộ giỏ, khóa miễn phí, rollback và tạo đồng thời.

Kết quả phần 2: **261/261 test ứng dụng đạt, không bỏ qua**, gồm 14 test mới (4 unit, 10 SQL Server). Chưa cập nhật database ứng dụng của nhóm; fixture chỉ dùng database GUID riêng.
