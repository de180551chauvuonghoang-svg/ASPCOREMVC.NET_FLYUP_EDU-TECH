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

## Phần 3: áp dụng, thay và gỡ mã giảm giá trên đơn Pending

`ICheckoutCouponService.ApplyAsync` nhận phiên người mua và OrderPublicId/Code/Version. Code trim/chuyển hoa; để trống là gỡ mã. Version là rowversion SQL mã hóa base64, không dùng số tiền từ trình duyệt. Chỉ tài khoản Student đang hoạt động trong database được sửa đơn của mình. Sai owner trả NotFound; form cũ trả Stale. Đơn phải Pending còn hạn và chưa có PaymentTransaction; không sửa đơn đã trả tiền, hết hạn hoặc đã nhận giao dịch.

Mã phải duy nhất, đang bật, trong thời hạn, tỷ lệ 1–90 và còn lượt. Giảm trên snapshot giá trong đơn, chỉ các dòng của giảng viên phát hành mã (chấp nhận Identity ID hoặc username cũ) và đúng CourseId nếu mã giới hạn một khóa. Đơn được dùng một mã tại một thời điểm; mã không áp dụng được vào dòng nào thì từ chối, không giữ chỗ. Tiền giảm làm tròn xuống từng dòng về VND nguyên; ví dụ 101 VND giảm 10% thành giảm 10 VND. Các dòng ngoài phạm vi không giảm. Tổng đơn luôn cộng lại từ các dòng sau khi áp dụng.

### Lượt dùng và giữ chỗ

- Một lượt sử dụng tính trên một đơn thanh toán thành công, không tính trên số khóa trong đơn.
- Đơn Pending còn hạn có snapshot CouponId là một chỗ giữ. Khả dụng = MaxUsage - UsedCount - số đơn giữ chỗ. Không tăng UsedCount chỉ vì áp dụng mã.
- Tính giữ chỗ trực tiếp từ đơn và dòng đã có, không thêm bảng/cột/migration. Gỡ mã, thay sang mã khác, hoặc đơn hết hạn/Cancelled sẽ nhả chỗ tương ứng.
- Mọi kiểm tra và cập nhật tổng/dòng diễn ra trong Serializable, khóa CheckoutManagement rồi CouponManagement. Nếu ghi dòng lỗi, rollback cả tổng đơn; không giữ chỗ nửa chừng.
- Quản lý mã Instructor nay khóa Code/DiscountPercent/CourseId khi có lượt đã dùng **hoặc đơn đang giữ chỗ**; MaxUsage không thấp hơn UsedCount cộng giữ chỗ. Thời hạn/bật tắt vẫn điều chỉnh được cho đơn mới. Snapshot trên đơn còn hạn đã áp dụng không đổi theo việc tắt hoặc sửa thời hạn của mã; áp dụng lại cùng mã/version hiện tại không cộng chỗ mới. Form version cũ vẫn phải tải lại.
- Writer xác nhận thanh toán sau này phải lấy đúng thứ tự khóa, xác nhận đơn còn hạn, tăng UsedCount một lần cho mỗi CouponId trong đơn và đổi Pending sang Paid trong cùng transaction. Chưa có writer đó ở bước này; chưa có giao dịch thật được ghi nhận hay nguồn doanh thu được bật.

Backend này chưa có controller/view. Khi tạo QR ở bước sau phải chốt và khóa số phải trả trước khi phát hành QR để không đổi chiết khấu sau khi người mua đã nhận thông tin chuyển tiền; hiện chưa phát hành QR nào.

Kiểm thử mới gồm chuẩn hóa/validation/role, phạm vi snapshot và legacy owner, làm tròn, mã lỗi/trùng/hết hạn, stale/ownership, các trạng thái không sửa được, hai đơn tranh lượt cuối, thay/gỡ/hết hạn nhả chỗ, rollback khi ghi dòng lỗi và quản lý giới hạn đồng thời với checkout. Không áp dụng migration lên database nhóm trong bước này.

Kết quả xác nhận ngày 06/11/2026: **276/276 test ứng dụng đạt, không bỏ qua**, gồm 15 test mới (9 SQL Server và 6 unit/case làm tròn). Lần chạy đầy đủ đầu có hai timeout SQL (tạo tài khoản fixture và lấy khóa checkout); kiểm tra truy vấn SQL rồi chạy lại toàn bộ không đổi code hay nới timeout cho kết quả trên. Không kiểm tra giao diện trình duyệt ở bước backend này.

## Phần 4: giao diện checkout và xem đơn của người mua

- Nút Đăng ký học ngay trên trang chi tiết khóa học mở GET `/Checkout/Create?courseId=...`; hiện hỗ trợ mua một khóa từ trang khóa học, chưa có giao diện giỏ hàng nhiều khóa.
- GET chỉ xem lại tên/giá server và tạo SubmissionKey cho form; chưa tạo đơn hay ghi danh. POST Create chỉ bind CourseId/SubmissionKey, có anti-forgery; giá/owner/trạng thái do backend xác định. Lỗi giữ key của form để thử lại.
- POST thành công hoặc gửi lại chuyển sang `/Checkout/Details/{publicId}`. Nếu có đơn Pending chứa khóa đang mua, mở lại đơn đó thay vì tạo thêm đơn. Không dùng returnUrl bên ngoài.
- Chi tiết chỉ đọc đơn của Student đang hoạt động và còn role Student trong database. Scope buyer từ claims, không từ query/body. DTO không chứa BuyerId hoặc thông tin xác thực. No-store và Razor encode tên khóa/mã.
- Hiển thị snapshot từng dòng, giá gốc, giảm giá, tổng phải trả, trạng thái và hạn theo UTC+7. Đơn Pending quá hạn được trình bày là hết hạn mà không sửa database bằng GET.
- Form áp dụng/gỡ mã dùng route publicId, Code/Version và anti-forgery. Trả lỗi 400/404/409/503 phù hợp; lỗi stale tải lại phiên bản hiện tại để người mua kiểm tra tổng trước khi thử lại. Không có form khi đơn hết hạn/đã trả tiền/đã nhận giao dịch.
- Đơn miễn phí hiển thị đăng ký thành công và liên kết quay lại khóa học. Đơn có phí ghi rõ chưa mở thanh toán trực tuyến; chưa có QR hoặc nút tự xác nhận đã trả tiền.

Kiểm thử HTTP với cookie/Identity, Razor và SQL Server thật: quyền Student/guest/sai role, GET không ghi dữ liệu, anti-forgery, overposting giá/owner/status, gửi lại/mở lại Pending, scope route/owner, encode, no-store, áp dụng/gỡ/stale và đăng ký miễn phí. TestApplication có tùy chọn SQL dành cho fixture GUID để các kiểm thử này chạy trọn backend thay vì mock transaction.

Kết quả ngày 06/11/2026: **281/281 test ứng dụng đạt, 0 bỏ qua**, có 5 test HTTP/SQL mới. Các lượt đầu dùng Shared Memory bị timeout ở tạo database/Identity kể cả test cũ; đổi kết nối kiểm thử sang TCP trên đúng instance MSSQLSERVER01 và chạy tuần tự giữa các lớp đã đạt. Các test đồng thời bên trong từng ca vẫn giữ nguyên. Lượt xác nhận cuối dùng timeout mặc định của ứng dụng, không đổi cấu hình database nhóm:

```powershell
$env:FLYUP_TEST_SQLSERVER='Server=tcp:127.0.0.1,1433;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;'
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release --no-build --no-restore --nologo -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1
```

Địa chỉ/cổng trên là kết nối local đã kiểm tra, không phải cấu hình bắt buộc cho các máy khác. Cách giới hạn chạy song song theo [RunSettings của xUnit](https://xunit.net/docs/config-runsettings).

Demo Kestrel/migration trên database GUID riêng vượt đủ 8 nhóm regression Admin; trình duyệt đã đăng nhập Student, mở nút đăng ký từ trang khóa học, tạo đơn 200.000 VND, áp mã CHECKOUT20 còn 160.000 VND, gỡ mã về 200.000 VND và đăng ký khóa miễn phí. Đối chiếu SQL: chỉ khóa miễn phí có enrollment Active, đơn có phí vẫn Pending và chưa có enrollment, UsedCount vẫn 0. Ảnh minh chứng lưu trong `.admin-demo/checkout-ui-proof.png` cục bộ, không đưa vào Git. Server demo đã dừng sau xác nhận trình duyệt; không có thanh toán/ngân hàng thật hoặc database nhóm được cập nhật.

## Phần 5a: chốt thông tin chuyển khoản trước khi phát hành QR (07/11/2026)

Thêm `CheckoutPaymentIntents` và migration `20261106180828_AddCheckoutPaymentIntents`. Một đơn có tối đa một bộ thông tin chuyển khoản; nội dung chuyển khoản Reference duy nhất toàn hệ thống, dạng FU + 20 ký tự hex viết hoa (22 ký tự, vừa giới hạn nội dung QR). Snapshot gồm số tiền VND nguyên, BIN/người nhận/số tài khoản, hạn thanh toán theo đơn. Không xóa cascade lịch sử. Đây không phải PaymentTransaction hoặc bằng chứng đã thu tiền.

`ICheckoutPaymentService.PrepareAsync` chỉ nhận OrderPublicId/Version từ lớp gọi và phiên Student. Thông tin ngân hàng đọc từ cấu hình server `Payments:BankTransfer`, mặc định Disabled. Chưa có controller/view gọi bước chuẩn bị thanh toán nên chưa mở thông tin chuyển khoản hay QR cho người dùng ở phần này.

- Kiểm tra lại Student đang hoạt động/role trong database và quyền sở hữu đơn. Đơn phải Pending, còn hạn, số tiền dương nguyên VND và tổng khớp từng dòng; không chuẩn bị cho đơn miễn phí, đã thanh toán hoặc đã có giao dịch.
- Form cũ bị từ chối trước khi tạo snapshot mới. Cùng đơn đã chốt trả lại đúng snapshot cũ khi thử lại, không thay người nhận hoặc sinh mã mới dù cấu hình ngân hàng thay đổi; dữ liệu snapshot không khớp đơn thì chặn.
- Serializable với CheckoutManagement → CouponManagement. Tạo snapshot và cập nhật version đơn trong cùng transaction. Lỗi cập nhật đơn rollback cả intent vừa thêm. Hai lần chuẩn bị đồng thời chỉ tạo một dòng.
- Sau khi có intent, backend coupon từ chối áp dụng/thay/gỡ mã và query chi tiết không trả quyền sửa mã. Test tranh chấp giữa áp mã và chốt đơn chỉ cho một phía thắng; không âm thầm đổi số tiền đã chốt.
- Chưa tăng UsedCount, tạo giao dịch đã xác minh, cấp enrollment cho khóa có phí hoặc nối nguồn doanh thu Instructor.

Thông số cấu hình khi tích hợp các bước sau: `Enabled`, `BankBin` (6 chữ số), `AccountNumber` (4–19 chữ/số, giữ số 0 đầu), `AccountName` (5–50 chữ/số không dấu viết hoa và khoảng trắng). Cấu hình này chỉ kiểm tra định dạng, không xác thực tài khoản có thật tại ngân hàng. Không bật nhận tiền thật trước khi chốt tài khoản thụ hưởng và hoàn thiện xác thực/đối soát. Không thêm số tài khoản mẫu hoặc khóa bí mật vào cấu hình production; các giá trị demo chỉ nằm trong Tests.

Nguồn đối chiếu cho bước QR/webhook tiếp theo: [Quick Link VietQR](https://www.vietqr.io/qr-dong) và [Webhook Casso](https://developer.casso.vn/webhook/thiet-lap-webhook-thu-cong). Webhook V2 có chữ ký; QR chỉ là thông tin chuyển khoản, không phải kết quả thanh toán. Chưa đăng ký tài khoản/webhook hoặc gửi thông tin ngân hàng tới dịch vụ ngoài trong phần 5a.

Kiểm thử mới: 7 ca SQL thật và 4 unit cho snapshot, cấu hình tắt/thiếu/sai, quyền/account/role, stale, gửi đồng thời, tranh chấp với coupon, rollback, unique reference/đơn và nâng cấp schema giữ đơn cũ.

Kết quả ngày 07/11/2026: **292/292 test ứng dụng đạt, không bỏ qua**, chạy SQL qua TCP và tuần tự giữa các lớp như phần 4. EF xác nhận model khớp migration. Chỉ kiểm tra database fixture GUID riêng; chưa cập nhật database nhóm hoặc chạy demo QR/webhook trên trình duyệt ở bước backend này.

## Tinh gọn giao diện checkout (10/11/2026)

Rút ngắn hướng dẫn và nhãn nút ở trang tạo/chi tiết đơn. Tổng tiền nổi bật; chỉ hiển thị giá gốc và phần giảm khi có giảm giá. Mã đơn và hạn đơn vẫn đầy đủ trong mục mở rộng “Thông tin đơn”. Giữ nhãn cho trình đọc màn hình khi gạch giá gốc, trạng thái đơn và thông báo chưa mở thanh toán. Không thay đổi xử lý đơn/coupon hoặc bật nhận tiền.

Kiểm tra: build Release và 5/5 kiểm thử CheckoutHttpTests với Razor/Identity/SQL Server thật đạt, không bỏ qua. Chưa kiểm tra trực quan trên trình duyệt ở lượt tinh gọn này. Bước chức năng kế tiếp vẫn là hiển thị QR từ thông tin đã chốt, sau đó xác thực webhook.

## Phần 5b: hiển thị QR từ thông tin đã chốt (10/11/2026)

Student bấm **Tạo QR thanh toán** trên chi tiết đơn. POST `/Checkout/PreparePayment/{id}` dùng anti-forgery, route id và chỉ bind Version; người mua, giá và ngân hàng không lấy từ form. Backend chốt nguyên tử như phần 5a, sau đó redirect về chi tiết. GET chỉ đọc; tải lại và gửi lại không tạo intent khác. Lỗi stale tải lại version/tổng hiện tại để kiểm tra trước khi thử lại.

Query chỉ trả thông tin chuyển khoản cho chủ đơn Student đang hoạt động, khi đơn Pending còn hạn, VND, snapshot khớp tổng/hạn và chưa có giao dịch nhận. QR và bảng người nhận/số tài khoản/BIN/số tiền/nội dung dùng snapshot đã lưu, giữ số 0 đầu; đổi cấu hình ngân hàng không làm đổi đơn đã chốt. Không hiện QR hoặc form tạo QR khi hết hạn/đã thanh toán/đã có giao dịch, hoặc tắt cấu hình. Thông tin phụ nằm trong mục mở rộng, không thêm nút “Tôi đã thanh toán”.

Ảnh dùng HTTPS Quick Link của VietQR theo [cú pháp chính thức](https://www.vietqr.io/qr-dong). URL được dựng từ trường đã kiểm tra, escape tham số, cố định host; template chỉ nhận chữ/số/gạch dưới, tối đa 64 ký tự. Ảnh đặt `referrerpolicy=no-referrer` để không gửi URL chứa mã đơn. Trình duyệt gửi BIN, số tài khoản, tên người nhận, số tiền và nội dung chuyển khoản tới img.vietqr.io để lấy ảnh; không gửi tài khoản học viên hoặc cookie ứng dụng. Nếu ảnh không tải được, người mua vẫn có thể mở thông tin chuyển khoản dạng chữ.

Cấu hình `Payments:BankTransfer` cần Enabled, BankBin, AccountNumber, AccountName và **QrTemplate**. QrTemplate mặc định rỗng nên web chưa mở QR. VietQR yêu cầu dự án chính thức có template riêng; `qr_only` và tài khoản giả chỉ dùng trong fixture/demo. Không thêm ngân hàng thật hoặc bật cấu hình nhận tiền trong repository. QR không tự hết hiệu lực trong ứng dụng ngân hàng: khi đơn hết hạn website sẽ ẩn QR, nhưng xử lý chuyển tiền muộn cần webhook/đối soát ở bước sau.

Chưa có webhook Casso hoặc xác nhận đã thu tiền; chưa tăng UsedCount, ghi danh khóa có phí hay bật nguồn doanh thu Instructor. Không cập nhật database nhóm hoặc thay đổi schema ở phần này.

Xác nhận ngày 10/11/2026: **296/296 test đạt, 0 bỏ qua**, gồm 3 HTTP/SQL mới và 1 unit cho QR. Build Release không lỗi/cảnh báo. Sau chỉnh bố cục tổng tiền, build lại Web và kiểm tra trực quan trên demo Kestrel/SQL riêng: đăng nhập Student, tạo đơn 200.000 VND, áp QRDEMO20 còn 160.000 VND, tạo QR và mở thông tin chuyển khoản. Ảnh VietQR đã tải, form coupon đã ẩn. SQL đối chiếu đúng intent 160.000, đơn vẫn Pending, giao dịch/enrollment của người mua/UsedCount đều 0. Demo vượt 8 nhóm regression và đã dừng server; ảnh cục bộ `.admin-demo/checkout-qr-proof.png` không commit. Không chuyển tiền hoặc dùng tài khoản ngân hàng thật.

## Phần 6a: xác thực dữ liệu webhook Casso V2 (12/11/2026)

`CassoWebhookVerifier` kiểm tra header `X-Casso-Signature` bằng HMAC-SHA512 trên timestamp và toàn bộ JSON đã sắp xếp theo mẫu chính thức. Hàm so sánh chữ ký theo thời gian cố định, từ chối JSON trùng khóa và giới hạn đầu vào 32 KiB. [Mẫu chữ ký Casso V2](https://github.com/CassoHQ/casso-webhook-v2-verify-signature).

Dữ liệu đã xác thực giữ nguyên dấu số tiền và số 0 đầu tài khoản. Thời gian đầy đủ của ngân hàng được đọc theo UTC+7. Giao dịch chỉ có ngày trả về thời điểm rỗng để bước đối chiếu không tự suy ra giờ. Mẫu thử của Casso có ID 0, nên bước xử lý đơn sau này cần loại ID thử trước khi ghi nhận thanh toán.

Đã chạy 11 kiểm thử xác thực, gồm chữ ký mẫu công khai và các ca sửa số tiền, sai khóa, JSON trùng trường và dữ liệu giao dịch không hợp lệ. Endpoint sẽ gọi bộ xác thực khi có luồng đối chiếu và lưu giao dịch. Bước đó dùng ID của Casso để chống ghi nhận trùng khi nhận lại thông báo. [Hướng dẫn nhận và chống trùng webhook](https://developer.casso.vn/webhook/thiet-lap-webhook-thu-cong).
