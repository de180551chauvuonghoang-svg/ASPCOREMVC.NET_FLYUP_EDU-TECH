# UC-38 — Bước đầu: lịch sử yêu cầu rút tiền

Instructor vào **Lịch sử rút tiền** ở sidebar. GET `/InstructorPayouts` chỉ lấy PayoutRequest có InstructorId bằng Identity ID hoặc username/email cũ của phiên đăng nhập, không nhận owner từ query. Role Admin/Student/Teacher và guest không được vào endpoint Instructor này.

Hiển thị mã yêu cầu, số tiền VNĐ với hai chữ số thập phân, trạng thái Pending/Approved/Rejected (Chờ xử lý/Đã duyệt/Bị từ chối), ngày gửi/ngày xử lý theo UTC+7 và AdminNote đã lưu. Approved là trạng thái trong dữ liệu, trang không đối soát hoặc xác minh giao dịch ngân hàng. DTO không chứa BankName/AccountNumber/AccountHolderName. Razor encode ghi chú; no-store cho response.

Lịch sử mới nhất trước, CreatedAt rồi Id giảm dần; Count/Skip/Take tại SQL Server, 10 dòng/trang. Trang ngoài phạm vi được giới hạn. Hỗ trợ bộ lọc trạng thái trước Count/Skip/Take, giữ trạng thái khi chuyển trang; bấm Lọc đưa về trang đầu, Đặt lại bỏ bộ lọc. Có thông báo riêng chưa có lịch sử và không khớp bộ lọc. Query sai kiểu/enum ngoài miền trả 400.

Repository/context scoped, DAO singleton, service kiểm tra định danh. Không thêm migration/package, không thay đổi các bản ghi rút tiền qua giao diện này. Count và danh sách không cam kết snapshot chung khi dữ liệu được cập nhật đồng thời.

## Phần còn thiếu

UC-38 chưa hoàn chỉnh. Luồng tạo yêu cầu và kiểm tra số dư đã được chuẩn bị ở bước tiếp theo bên dưới, nhưng chưa mở gửi yêu cầu trong cấu hình hiện tại vì chưa nối nguồn thu nhập đã đối soát. Còn thiếu nguồn giao dịch để tính doanh thu 70%, lịch sử đối soát và chức năng xử lý của Admin. Không tính doanh thu bằng giá khóa hiện tại nhân số ghi danh.

UC-37 thống kê đánh giá cũng chưa có nguồn: đã fetch và kiểm tra mô hình hiện tại, main, mergecodetaihoang và domain-interfaces; chưa có Review/Rating trong nguồn đã kiểm tra. Không tạo số sao hay đánh giá giả.

## Kiểm tra 31/10/2026

- 185/185 test ứng dụng với SQL Server thật đạt, 0 bỏ qua. 11/11 test AdminDemo đạt; build Release 0 lỗi/0 cảnh báo.
- SQL: dual identity, phạm vi cá nhân, xếp thứ tự ổn định khi trùng ngày, đủ hai trang, giảng viên rỗng/định danh thiếu và bộ lọc cả ba trạng thái.
- HTTP: chặn role ngoài Instructor/guest; query instructorId giả không đổi owner; định dạng tiền/ngày UTC+7, encode AdminNote, không lộ tài khoản ngân hàng hoặc yêu cầu người khác, no-store, page clamp, empty và 400 cho query sai.
- Demo Kestrel với database GUID riêng đạt đủ 8 nhóm regression Admin. Chỉ thêm 12 yêu cầu mẫu vào database demo đã kiểm tra tên GUID; không chèn hoặc sửa database ứng dụng của nhóm. Trình duyệt xác nhận 12 yêu cầu/2 trang, chuyển trang 2 có hai dòng, lọc Rejected còn 4 yêu cầu/trang 1 và phản hồi Admin hiển thị đúng. Không console warning/error trong luồng đã xem.
- Ảnh `.admin-demo/uc38-payout-history-proof.jpg` giữ cục bộ. Server demo đã dừng sau kiểm tra.

Ba commit riêng: dữ liệu lịch sử theo quyền; màn hình lịch sử; bộ lọc trạng thái.

## Bước tiếp theo — chuẩn bị gửi yêu cầu và kiểm tra số dư

GET `/InstructorPayouts/Create` hiển thị thu nhập đã đối soát, tổng tiền yêu cầu Approved, tổng tiền Pending và khả dụng. Khả dụng = Max(0, thu nhập đã ghi có - Approved - Pending), không trừ Rejected. Khi chưa xác minh thu nhập, Available=null và UI ghi Chưa xác minh, không hiển thị ô nhập ngân hàng/nút gửi. Có yêu cầu Pending thì chờ xử lý trước khi gửi mới; không có số dư thì hiện thông báo.

`IInstructorEarningsSource` là điểm tích hợp dữ liệu thu nhập đã được đối soát của chính giảng viên: số cộng dồn sau giảm giá/hoàn tiền và đã áp dụng phần chia 70%, trước khi trừ payout. Adapter phải đọc bằng AppDbContext/transaction được truyền vào. Chưa có adapter thanh toán thật, DI production dùng `UnavailableInstructorEarningsSource` trả null; tuyệt đối không thay bằng dữ liệu demo hoặc tổng Enrollment x Course.Price. Test dùng nguồn xác minh giả riêng trong project Tests.

POST Create yêu cầu role Instructor và anti-forgery. Chỉ bind Amount/BankName/AccountNumber/AccountHolderName; owner lấy từ phiên, không nhận status/processed/adminnote/số dư từ form. Số tiền phải >0, tối đa decimal(18,2), tối đa hai chữ số thập phân. Chuỗi được trim; tên ngân hàng/chủ tài khoản tối đa 100 ký tự, không ký tự điều khiển; số tài khoản 4–34 ký tự ASCII chữ/số, giữ nguyên số 0 ở đầu.

Nguồn thu nhập chưa xác minh trả 503 và không ghi yêu cầu. Khi nguồn sẵn sàng, transaction Serializable + sp_getapplock `EduFlyUp.PayoutManagement` kiểm tra lại thu nhập/số tiền đã duyệt/đang chờ trước khi ghi. Chặn khi đã có Pending hoặc số tiền vượt khả dụng, trả 409; input sai trả 400. Yêu cầu mới luôn có owner Identity ID, Pending, UTC CreatedAt, AdminNote/ProcessedAt=null. Hai request đồng thời qua các context riêng chỉ tạo một Pending. Đây là chặn gửi đồng thời/yêu cầu mới khi đang chờ, chưa phải token idempotency bền vững cho replay sau khi yêu cầu cũ đã xử lý.

Adapter thu nhập, writer đối soát và chức năng Admin thay đổi payout phải dùng cùng SQL lock và transaction để giữ quy tắc số dư. Không thêm schema/migration, không mở chức năng chuyển tiền. GET số dư là thông tin tại thời điểm đọc; POST luôn đọc lại dưới khóa. Chưa có writer Admin/adapter thật để nghiệm thu toàn bộ luồng tài chính.

Kiểm tra cuối bước này: 199/199 test ứng dụng trên SQL Server thật, 0 bỏ qua; 11/11 test AdminDemo; build Release 0 lỗi/0 cảnh báo. Unit kiểm tra amount/account/normalize và null balance; SQL kiểm tra không ghi khi nguồn chưa có, trừ Approved/giữ chỗ Pending/bỏ Rejected, legacy owner, số tiền vượt khả dụng, server-owned fields và gửi đồng thời. HTTP kiểm tra anti-forgery, input sai, giả owner/status/balance không được tin, form sẵn sàng khi service xác minh (test double), redirect lịch sử và form đóng khi nguồn production chưa có.

Trình duyệt demo database GUID riêng: từ lịch sử mở trang yêu cầu, thu nhập/khả dụng hiện Chưa xác minh, không có ô tài khoản/nút gửi; không console warning/error. Demo đạt đủ 8 nhóm regression Admin, đã dừng sau QA. Không thêm nguồn tiền giả vào ứng dụng hoặc database nhóm. Ảnh `.admin-demo/uc38-payout-request-proof.jpg` cục bộ.

Ba commit riêng: policy và hợp đồng nguồn số dư; lưu yêu cầu dưới transaction/lock; form và chặn gửi khi chưa xác minh.
