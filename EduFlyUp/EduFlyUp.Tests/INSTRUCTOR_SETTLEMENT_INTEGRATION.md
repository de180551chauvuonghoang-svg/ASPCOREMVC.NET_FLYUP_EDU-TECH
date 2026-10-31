# Điểm tích hợp thanh toán với doanh thu/rút tiền Instructor

## Phân chia phần việc kỹ thuật

Phần Instructor đã có phép chia 70%, tính khả dụng sau Approved/Pending, kiểm tra số tiền, luồng tạo yêu cầu, chống gửi trùng và lịch sử/chi tiết. Phần thanh toán cần cung cấp giao dịch đã xác minh qua `IInstructorSettlementSource`. Không cần tính lại khoản chia 70% bên checkout.

Chưa có model/bảng giao dịch thanh toán trong repo đã kiểm tra. Production đang đăng ký `UnavailableInstructorSettlementSource`, trả null; vì vậy chưa mở gửi yêu cầu thực tế. Không bật bằng dữ liệu demo, dữ liệu ghi danh hoặc giá khóa học hiện tại.

## Hợp đồng nguồn dữ liệu

Triển khai `ReadVerifiedReceiptsAsync(AppDbContext db, InstructorIdentity instructor)` và thay registration của `IInstructorSettlementSource` khi có nguồn thật. Giữ `SettlementInstructorEarningsSource` để áp dụng phép chia của Instructor.

Mỗi `InstructorSettledReceipt` gồm:

| Trường | Ý nghĩa |
| --- | --- |
| Reference | Mã giao dịch ổn định, chuẩn hóa và duy nhất; nếu nhiều nhà cung cấp cần phân biệt nhà cung cấp trong mã. |
| InstructorId | Chủ sở hữu doanh thu của giao dịch, theo Identity ID hoặc username/email cũ. |
| PaidAmount | Tiền thực tế đã thu, sau giảm giá, trước hoàn tiền. Không phải giá niêm yết hiện tại. |
| RefundedAmount | Tổng tiền đã xác nhận hoàn của chính giao dịch đó. |
| Info (tùy chọn) | Snapshot CourseId, CourseTitle, StudentName, OriginalPrice và SettledAtUtc để hiển thị báo cáo. Thiếu trường nào thì UI ghi Chưa có thông tin, không suy ra từ dữ liệu hôm nay. |

Nguồn phải lọc theo giảng viên và chỉ trả giao dịch đã thanh toán/đối soát xác minh. Giao dịch chưa trả tiền, thất bại hoặc chờ xác nhận không được ghi có. Mỗi giao dịch chỉ có một dòng với số hoàn tiền cộng dồn, không nhân bản dòng khi có nhiều lần hoàn. Quyền hưởng doanh thu nên dùng chủ sở hữu đã lưu tại lúc phát sinh giao dịch, không suy ra lại từ chủ sở hữu khóa học sau khi chuyển nhượng.

Nếu một thanh toán gồm nhiều khóa học, Reference phải nhận diện duy nhất từng khoản được phân bổ cho khóa/giảng viên, chẳng hạn kết hợp nhà cung cấp, mã thanh toán và mã dòng hàng; không lặp cùng một Reference cho các dòng. Chưa có bảng đơn hàng để xác nhận cấu trúc khóa thực tế. Báo cáo hiện ghi Mã khoản thu, không tự diễn giải thành mã đơn hàng khi nguồn chưa cung cấp ánh xạ.

Các số tiền trong hợp đồng hiện dùng VNĐ, cùng đơn vị với Course.Price và PayoutRequest.Amount. Nguồn phải xác nhận đúng đơn vị tiền trước khi cung cấp; chưa có chức năng đa tiền tệ hoặc tự đổi tỷ giá.

Trả **null** khi nguồn chưa có, chưa xác minh, chưa đủ phạm vi lịch sử hoặc không thể bảo đảm dữ liệu đầy đủ. Trả **danh sách rỗng** chỉ khi đã xác minh rằng chưa có khoản thu. Không trả một trang hoặc phần lịch sử làm tổng đầy đủ, vì sẽ làm sai số dư. Dùng mã tham chiếu chuẩn hóa nhất quán để kiểm tra trùng.

Đọc qua cùng AppDbContext và transaction được truyền vào. Không mở context/kết nối ngoài transaction khi kiểm tra số dư để gửi yêu cầu. Writer xác nhận thanh toán/hoàn tiền/đối soát và Admin thay đổi payout phải phối hợp lock `EduFlyUp.PayoutManagement` trong transaction để tránh ghi có/trừ tiền đồng thời sai lệch.

## Quy tắc tính của Instructor

Cho từng giao dịch: net = PaidAmount - RefundedAmount; instructor = Round(net × 0.70, 2, AwayFromZero); platform = net - instructor. Đây là quy ước làm tròn hai chữ số thập phân của phần triển khai, cần giữ nhất quán khi đối soát. Cộng khoản Instructor của từng dòng sau khi làm tròn, không tính 70% trên tổng đã cộng rồi làm tròn lại. Không thêm phí khác khi chưa có quy tắc nghiệp vụ.

Số tiền âm, quá decimal(18,2), quá hai chữ số thập phân, hoàn vượt tiền thu, mã giao dịch trùng/rỗng hoặc dòng ngoài quyền khiến nguồn thu nhập trả null để kiểm tra lại. Không bỏ qua dòng lỗi rồi công bố tổng thu nhập như đã đủ.

Khả dụng = Max(0, tổng khoản Instructor - tổng payout Approved - tổng payout Pending). Rejected không trừ. Nếu hoàn tiền làm thu nhập thấp hơn khoản đã rút thì khả dụng bằng 0; việc thu hồi công nợ/đối soát tiếp theo chưa được triển khai. Số dư POST luôn đọc lại dưới transaction/lock; GET chỉ là thông tin tại thời điểm xem.

## Ví dụ kiểm thử, không phải dữ liệu production

Thu 1.000.000, hoàn 200.000 → khoản Instructor 560.000. Thu thêm 500.000, không hoàn → 350.000. Giao dịch hoàn toàn bộ → 0. Tổng ghi có 910.000; đã duyệt rút 200.000 → khả dụng 710.000. Gửi yêu cầu 700.000 → giữ chỗ 700.000, còn khả dụng 10.000 và chờ Admin xử lý trước yêu cầu mới.

## Chưa hoàn thành

Adapter từ bảng/cổng thanh toán thật, kiểm tra xác thực giao dịch, cung cấp snapshot/ánh xạ đơn hàng thực và chức năng xử lý của Admin. Màn hình báo cáo theo khoản thu đã có qua nguồn xác minh, nhưng chưa có dữ liệu thật để nghiệm thu báo cáo đầy đủ. Các nguồn giả chỉ ở project Tests; không có endpoint cho người dùng tự khai doanh thu.
