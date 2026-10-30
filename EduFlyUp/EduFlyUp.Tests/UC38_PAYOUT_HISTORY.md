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

Nguồn thu nhập chưa xác minh trả 503 và không ghi yêu cầu. Khi nguồn sẵn sàng, transaction Serializable + sp_getapplock `EduFlyUp.PayoutManagement` kiểm tra lại thu nhập/số tiền đã duyệt/đang chờ trước khi ghi. Chặn khi đã có Pending hoặc số tiền vượt khả dụng, trả 409; input sai trả 400. Yêu cầu mới luôn có owner Identity ID, Pending, UTC CreatedAt, AdminNote/ProcessedAt=null. Hai request đồng thời qua các context riêng chỉ tạo một Pending. Cơ chế nhận diện gửi lại sau khi yêu cầu đã xử lý được bổ sung ở bước tiếp theo bên dưới.

Adapter thu nhập, writer đối soát và chức năng Admin thay đổi payout phải dùng cùng SQL lock và transaction để giữ quy tắc số dư. Bước chuẩn bị form ban đầu không thêm schema/migration và không mở chức năng chuyển tiền. GET số dư là thông tin tại thời điểm đọc; POST luôn đọc lại dưới khóa. Chưa có writer Admin/adapter thật để nghiệm thu toàn bộ luồng tài chính.

Kiểm tra cuối bước này: 199/199 test ứng dụng trên SQL Server thật, 0 bỏ qua; 11/11 test AdminDemo; build Release 0 lỗi/0 cảnh báo. Unit kiểm tra amount/account/normalize và null balance; SQL kiểm tra không ghi khi nguồn chưa có, trừ Approved/giữ chỗ Pending/bỏ Rejected, legacy owner, số tiền vượt khả dụng, server-owned fields và gửi đồng thời. HTTP kiểm tra anti-forgery, input sai, giả owner/status/balance không được tin, form sẵn sàng khi service xác minh (test double), redirect lịch sử và form đóng khi nguồn production chưa có.

Trình duyệt demo database GUID riêng: từ lịch sử mở trang yêu cầu, thu nhập/khả dụng hiện Chưa xác minh, không có ô tài khoản/nút gửi; không console warning/error. Demo đạt đủ 8 nhóm regression Admin, đã dừng sau QA. Không thêm nguồn tiền giả vào ứng dụng hoặc database nhóm. Ảnh `.admin-demo/uc38-payout-request-proof.jpg` cục bộ.

Ba commit riêng: policy và hợp đồng nguồn số dư; lưu yêu cầu dưới transaction/lock; form và chặn gửi khi chưa xác minh.

## Bước tiếp theo — nhận diện cùng một lần gửi

Thêm migration `20261030234022_AddPayoutSubmissionKey`: cột Guid? SubmissionKey trên PayoutRequests và unique filtered index cho các giá trị không null. Bản ghi cũ giữ null, không backfill ngẫu nhiên, không xóa dữ liệu. Chỉ áp dụng/kiểm tra migration trên database GUID riêng trong đợt này, chưa cập nhật database của nhóm. Khi tích hợp cần áp dụng migration trước khi dùng bản code mới. [Tài liệu EF Core về index](https://learn.microsoft.com/en-us/ef/core/modeling/indexes).

GET Create cấp Guid mới, POST bắt buộc mã hợp lệ và khác Guid.Empty. Mã được giữ qua lỗi validation, thiếu số dư hoặc lỗi lưu để lần thử lại giữ cùng định danh; một lần GET mới là một yêu cầu mới. Key không thay thế anti-forgery hoặc quyền truy cập.

Server tìm key trước khi phụ thuộc nguồn doanh thu, rồi kiểm tra lại dưới SQL lock. Cùng key + đúng chủ sở hữu + cùng amount/bank/account/holder đã normalize trả AlreadySubmitted, chuyển về lịch sử, không tạo thêm bản ghi kể cả trạng thái đã Approved/Rejected hoặc nguồn thu nhập hiện không sẵn sàng. Cùng key nhưng khác thông tin/chủ sở hữu trả 409, không trả chi tiết ngân hàng hoặc yêu cầu của người khác. Hai request cùng key đồng thời trả một Created/một AlreadySubmitted; hai key khác nhau vẫn bị chặn khi đã có Pending.

Lưu key trên bản ghi mới trong cùng transaction tạo yêu cầu. Unique index là lớp bảo vệ cuối cùng trong database; writer Admin sau này phải giữ nguyên key. Không xóa key khi yêu cầu hoàn tất. Chưa có cơ chế xóa lịch sử payout; nếu tích hợp cơ chế xóa thì cần bảo toàn lịch sử key để giữ bảo đảm chống gửi lại.

Kiểm tra cuối: 208/208 test ứng dụng với SQL Server thật, 0 bỏ qua; 11/11 test AdminDemo; build Release 0 lỗi/0 cảnh báo. SQL kiểm tra index không nhận key trùng nhưng chấp nhận nhiều bản ghi null cũ; nâng cấp từ migration cũ có dữ liệu vẫn giữ nguyên số tiền/chủ sở hữu/trạng thái/tài khoản và null key. Kiểm tra gửi lại sau Approved khi nguồn thu nhập unavailable, key với payload/owner khác và hai request cùng key đồng thời. HTTP kiểm tra redirect cho AlreadySubmitted, giữ key khi lỗi, GET mới cấp key khác, key thiếu/sai/rỗng bị chặn.

Demo khởi động/migrate và 8 nhóm HTTP/SQL regression Admin đạt trên database GUID mới; server đã dừng. Không chạy thêm QA bằng trình duyệt cho thay đổi hidden field này. UC-38 vẫn cần adapter doanh thu/đối soát thật để mở gửi thực tế, phần xử lý Admin và lịch sử doanh thu chưa hoàn chỉnh.

## Bước tiếp theo — chi tiết yêu cầu rút tiền

Từ mỗi dòng lịch sử bấm **Xem chi tiết**. GET `/InstructorPayouts/Details/{id}` kiểm tra role Instructor và ownership trong SQL (Identity ID hoặc username/email cũ). Id lấy từ route, không nhận id/owner thay thế từ query. Id sai kiểu/không dương hoặc status bộ lọc sai trả 400; yêu cầu không tồn tại/ngoài quyền trả 404. Response no-store.

Trang chi tiết hiển thị số tiền, trạng thái, ngày gửi/ngày xử lý, ngân hàng/chủ tài khoản và phản hồi Admin. Số tài khoản được che tại DAO trước khi tạo DTO: chỉ để tối đa bốn ký tự cuối, tài khoản cũ dài <=4 bị che toàn bộ, rỗng ghi Chưa có thông tin. Không truyền tài khoản đầy đủ hoặc SubmissionKey sang view. Razor encode ngân hàng, chủ tài khoản và AdminNote; dữ liệu thiếu có thông báo rõ. Không có chức năng sửa ngân hàng, sửa số tiền, duyệt hoặc chuyển tiền ở trang này.

Giữ page/status từ lịch sử khi mở chi tiết và khi bấm quay lại, không dùng returnUrl tùy ý. Endpoint đích tự kiểm tra quyền dù người dùng sửa id trong URL. Bước này không thêm migration/package hoặc thay đổi payout của nhóm.

Kiểm tra cuối: 213/213 test ứng dụng trên SQL Server thật, 0 bỏ qua; 11/11 test AdminDemo; build Release 0 lỗi/0 cảnh báo. SQL kiểm tra dual identity, ngoài quyền, dữ liệu thiếu, tài khoản ngắn và chỉ trả số đã che. HTTP kiểm tra role/guest, route id không bị query ghi đè, no-store, encode HTML, không lộ số tài khoản hoặc SubmissionKey, trạng thái/ngày/tiền, 400/404 và liên kết giữ page/status.

Demo Kestrel trên database GUID riêng đạt đủ 8 nhóm regression Admin. Chỉ thêm yêu cầu kiểm thử vào database demo đã kiểm tra phạm vi. Trình duyệt xác nhận lọc Rejected → mở chi tiết → tài khoản chỉ hiện đuôi 5678 → quay về vẫn giữ bộ lọc/trang; không có console warning/error. Server đã dừng, ảnh `.admin-demo/uc38-payout-detail-proof.jpg` giữ cục bộ. Ba commit riêng: truy vấn/che số; màn hình; liên kết giữ bộ lọc.
