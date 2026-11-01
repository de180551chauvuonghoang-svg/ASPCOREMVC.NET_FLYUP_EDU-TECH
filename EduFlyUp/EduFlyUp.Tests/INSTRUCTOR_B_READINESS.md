# Tình trạng và kịch bản demo phân hệ Instructor B

## Phạm vi đã triển khai và phụ thuộc còn lại

| Use case | Đã có | Còn cần tích hợp/nghiệm thu |
| --- | --- | --- |
| UC-24 — Học liệu | Upload/thay/gỡ/tải tài liệu; MP4 có kiểm tra quyền/Range; dọn file theo snapshot sau xóa bài/khóa. | Lưu bền vững/sao lưu App_Data khi triển khai; chưa có preview Guest, chuyển mã video hoặc tác vụ dọn file dư. |
| UC-35 — Kết quả học viên | Danh sách ghi danh, tìm kiếm/trạng thái, lịch sử điểm đã nộp và summary từng học viên. | Nguồn hoàn thành từng bài và đáp án từng câu từ phần học/làm bài của User. |
| UC-36 — Xuất Excel | Xuất đủ trang theo bộ lọc, Completed, lịch sử điểm và điểm cao nhất theo từng đề; typed text không formula. | Cột tiến độ thực tế cần nguồn hoàn thành bài; không tự gán % từ trạng thái ghi danh. |
| UC-37 — Studio thống kê | Tổng lượt/HV không trùng/trạng thái; top 5 khóa đông HV; backend tính 70% và trang báo cáo doanh thu riêng. | Nguồn giao dịch thật để gắn doanh thu xác minh vào Studio; dữ liệu đánh giá Review/Rating. |
| UC-38 — Doanh thu/rút tiền | Báo cáo khoản thu; chia 70%; kiểm tra số dư; form tạo yêu cầu; khóa giao dịch; chống gửi lại; lịch sử/chi tiết che tài khoản. | Adapter giao dịch đã xác minh, snapshot/ánh xạ đơn hàng, đối soát thật và phần xử lý của Admin. Nguồn chưa có thì vẫn chặn tạo yêu cầu mới. |
| UC-40 — Mã giảm giá | Tạo/sửa/bật/tắt, scope khóa sở hữu, tối đa 90%, thời hạn/lượt dùng, stale form và khóa SQL. | Checkout áp dụng mã và cập nhật UsedCount theo cùng contract khóa; chưa triển khai ở module quản lý coupon. |

Trạng thái thiếu nguồn không phải doanh thu/tiến độ bằng 0. Chỉ hiển thị 0 khi nguồn đã xác minh và kết quả thực sự rỗng. Dữ liệu tiền giả chỉ nằm trong Tests; không có endpoint tự khai doanh thu để mở rút tiền.

## Các lỗi tái hiện và sửa trong đợt rà soát

1. **Trạng thái payout ngoài danh mục bị bỏ qua khi tính khả dụng.** Trước sửa, một payout có Status=99 vẫn để khả dụng 700.000 trong fixture. Sau sửa, payout của mình có trạng thái không rõ khiến số dư chưa xác minh, không lưu yêu cầu mới. Dữ liệu lỗi của người khác không tác động số dư của mình.
2. **Gửi lại bằng email cũ khác hoa/thường bị báo xung đột.** SQL lịch sử vẫn nhận bản ghi, nhưng đối chiếu key tại C# dùng so sánh khác. Đã thống nhất username/email không phân biệt hoa thường; chủ sở hữu khác vẫn bị chặn.
3. **Mã khoản thu có khoảng trắng đầu/cuối có thể làm cộng hai dòng cùng mã logic.** Fixture trước sửa cộng thành 1.400.000. Sau sửa, nguồn có Reference chưa chuẩn hóa trả chưa xác minh để nguồn đối soát sửa lại; không tự viết lại mã giao dịch rồi tiếp tục công bố tổng tiền.

Mỗi lỗi có test tái hiện thất bại trước sửa, kiểm tra đạt sau sửa và commit/push riêng. Không thêm migration/package trong đợt sửa này.

Kết quả ngày 01/11/2026: **241/241 test ứng dụng đạt, 0 bỏ qua**, có chạy các fixture SQL Server thật; **11/11 test AdminDemo đạt**; build Release 0 lỗi/0 cảnh báo. Demo khởi động/migrate trên database GUID mới và đủ **8 nhóm HTTP/SQL regression Admin đạt**, server đã dừng. Không cập nhật database ứng dụng của nhóm hoặc chạy lại QA trình duyệt trong đợt sửa backend này; kịch bản bên dưới dùng cho lần trình bày tiếp theo.

## Chạy kiểm tra cục bộ

Chạy từ thư mục gốc repo; thay tên SQL Server nếu máy dùng instance khác. Fixture SQL chỉ tạo/xóa database GUID riêng, không dùng database ứng dụng để kiểm thử.

```powershell
dotnet build EduFlyUp/EduFlyUp.slnx --configuration Release
$env:FLYUP_TEST_SQLSERVER='Server=.\MSSQLSERVER01;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;'
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release --no-build --no-restore
dotnet test tools/AdminDemo.Tests/AdminDemo.Tests.csproj --configuration Release
```

Nếu không có biến SQL thì các test SqlServerFact bị skip; không coi kết quả đó là đã kiểm tra tích hợp SQL.

## Demo trên database riêng

```powershell
dotnet run --project tools/AdminDemo/AdminDemo.csproj --configuration Release -- --repo . --server '.\MSSQLSERVER01' --keep-open
```

Công cụ tạo database `FlyUpAdminDemo_<guid>`, chạy Web theo bản sao runtime và giữ server mở sau kiểm tra. Mở URL in trong kết quả. Đăng nhập bằng Instructor A (`instructor-a@demo.example.test`), mật khẩu sinh riêng nằm trong `.admin-demo/session.json` cục bộ. Không đưa file này/mật khẩu vào Git hoặc tài liệu gửi nhóm. Nhấn Enter trong terminal khi kiểm tra xong để dừng server. Chi tiết công cụ ở [AdminDemo](../../tools/AdminDemo/README.md).

### Kịch bản trình bày

1. **Studio:** giải thích số lượt ghi danh khác số HV không trùng; bảng top loại ghi danh đã hủy, trong khi tổng lịch sử vẫn giữ. Lọc danh sách khóa học để thấy các thống kê tổng giữ đúng phạm vi ghi trên UI.
2. **Học viên của tôi:** chọn khóa sở hữu, tìm/lọc trạng thái, mở học viên. Lịch sử thi hiển thị các lần đã có; nếu chưa có lần thi thì giữ trạng thái rỗng. Nêu rõ tiến độ từng bài/đáp án đang chờ nguồn User.
3. **Excel:** chọn bộ lọc đã áp dụng, tải file và xem 5 sheet. File xuất toàn bộ các trang của bộ lọc; điểm cao nhất riêng từng đề. Bài chưa nộp không có điểm; tiến độ chưa có nguồn không thành 0%/100%.
4. **Học liệu:** trong Giáo trình của khóa sở hữu, thử tài liệu kiểm thử nhỏ hợp lệ và các thao tác thay/gỡ; MP4 tối đa 100 MiB, tài liệu 10 MiB. Dùng tệp demo, không tải tài liệu cá nhân lên bản trình bày.
5. **Mã giảm giá:** tạo code chưa dùng trong database demo, chọn khóa sở hữu, mức giảm hợp lệ; sửa, tắt/bật và tìm lại. Thử mức giảm 91% để thấy validation. Đây là quản lý mã; chưa trình bày như checkout đã áp dụng mã.
6. **Doanh thu của tôi:** trong cấu hình hiện tại, cho thấy thông báo chưa có nguồn đã xác minh. Bảng khoản thu và tổng theo tìm kiếm đã được kiểm tra qua HTTP/Razor với nguồn trong Tests, chưa có giao dịch thật để demo số tiền.
7. **Lịch sử/yêu cầu rút tiền:** lịch sử có thể rỗng ở database demo mới. Mở trang yêu cầu để thấy chưa xác minh số dư và không có nút gửi. Các tình huống nguồn sẵn sàng, vượt số dư, gửi đồng thời và gửi lại đã có test SQL/HTTP; không đưa nguồn giả vào ứng dụng để giả lập số dư thật.

## Bàn giao tích hợp

Nguồn checkout cần cung cấp khoản thu/thông tin hoàn tiền đã xác minh; Instructor tính 70%, báo cáo và kiểm tra rút tiền. Nhóm cần chốt người phụ trách adapter/đối soát dùng chung. Writer thay đổi payout phía Admin và writer thanh toán/hoàn tiền phải phối hợp SQL lock `EduFlyUp.PayoutManagement`; giữ SubmissionKey khi cập nhật trạng thái. Writer checkout/coupon phối hợp lock `EduFlyUp.CouponManagement` khi cập nhật lượt dùng.

Migration `20261030234022_AddPayoutSubmissionKey` đã có trên nhánh, cần áp dụng khi tích hợp bản code này; chưa áp dụng vào database nhóm trong quá trình kiểm thử. Chi tiết nguồn tiền ở [hướng dẫn tích hợp](INSTRUCTOR_SETTLEMENT_INTEGRATION.md).

Tài liệu từng phần: [UC-24](UC24_MATERIALS.md), [UC-35](UC35_STUDENT_RESULTS.md), [UC-36](UC36_STUDENT_EXPORT.md), [UC-37](UC37_INSTRUCTOR_SUMMARY.md), [UC-38](UC38_PAYOUT_HISTORY.md), [UC-40](UC40_COUPONS.md).
