# HƯỚNG DẪN TÍCH HỢP THANH TOÁN TỰ ĐỘNG SEPAY & VIETQR CHO EDUFLYUP

Tài liệu này cung cấp toàn bộ quy trình thiết lập, tài liệu API và hướng dẫn kiểm thử cho tính năng thanh toán chuyển khoản quét mã VietQR tự động qua **SePay (sepay.vn)** trong dự án **EduFlyUp**.

---

## 1. THÔNG TIN CẤU HÌNH SEPAY CỦA DỰ ÁN

- **Ngân hàng**: `TPBank (Ngân hàng Tiên Phong)`
- **Mã BIN Napas 247**: `970423`
- **Số tài khoản**: `25816798888`
- **Chủ tài khoản**: `CHAU VUONG HOANG`
- **API Key SePay**: cấu hình khóa webhook mới qua biến môi trường `SePay__ApiKey` hoặc .NET User Secrets. Thay khóa từng được lưu trong Git tại SePay trước khi triển khai.
- **Tên Key**: `EDUFLYUPDOTNET`
- **Webhook URL Production (Railway)**:
  ```text
  https://aspcoremvcnetflyupedu-tech-production.up.railway.app/api/payments/sepay-webhook
  ```
- **Webhook URL Local**:
  ```text
  http://localhost:5298/api/payments/sepay-webhook
  hoặc
  https://localhost:7006/api/payments/sepay-webhook
  ```

---

## 2. KIẾN TRÚC & CÁC THÀNH PHẦN ĐÃ TRIỂN KHAI

Tính năng thanh toán được thiết kế theo đúng chuẩn kiến trúc **Clean Architecture (N-Tier)** của EduFlyUp:

1. **`EduFlyUp.BusinessObjects`**:
   - `Order.cs`: Entity đại diện cho Đơn hàng thanh toán (`OrderCode`, `UserId`, `CourseId`, `Amount`, `Status`, `TransactionReference`, `PaidAt`).
   - `OrderStatus.cs`: Enum trạng thái đơn (`Pending`, `Paid`, `Cancelled`, `Expired`).

2. **`EduFlyUp.DataAccess`**:
   - `OrderConfiguration.cs`: Cấu hình Fluent API bảng `Orders` (Unique Index `OrderCode`, Foreign Key `CourseId`).
   - `OrderDAO.cs`: Xử lý lưu đơn hàng, tìm đơn theo mã, và hàm `MarkAsPaidAsync` (tự động chuyển trạng thái đơn sang `Paid` và kích hoạt học viên vào bảng `Enrollments` ở trạng thái `Active`).
   - `AppDbContext.cs`: Đã khai báo `DbSet<Order> Orders`.

3. **`EduFlyUp.Repositories`**:
   - `IOrderRepository.cs` & `OrderRepository.cs`: Quản trị dữ liệu đơn hàng theo Repository Pattern.

4. **`EduFlyUp.Services`**:
   - `IPaymentService.cs` & `PaymentService.cs`:
     - Tự động sinh mã thanh toán chuẩn `EDUPAYxxxxxx` (6 chữ số ngẫu nhiên duy nhất).
     - Tạo link ảnh mã **VietQR** động chuẩn Napas 247:
       `https://img.vietqr.io/image/{bankBin}-{bankAcc}-compact2.png?amount={amount}&addInfo={orderCode}&accountName={accountName}`
     - Xử lý xác thực API Key từ SePay và kích hoạt khóa học khi nhận Webhook.

5. **`EduFlyUp.Web`**:
   - `PaymentController.cs`:
     - `GET /Payment/Checkout/{id}`: Hiển thị giao diện thanh toán có mã QR, thông tin chuyển khoản và nút copy.
     - `GET /api/payments/status/{orderCode}`: API cho trình duyệt kiểm tra tự động trạng thái đơn hàng mỗi 3 giây.
     - `POST /api/payments/sepay-webhook`: Nhận Webhook từ SePay khi có biến động số dư.
   - `Views/Payment/Checkout.cshtml`: Giao diện thanh toán hiện đại với mã VietQR, nút copy thông tin, spinner chờ và script tự động phát hiện thanh toán thành công để chuyển vào khóa học.
   - `Views/Courses/Details.cshtml`: Nút "Đăng ký học ngay" liên kết trực tiếp sang trang Checkout. Nếu học viên đã mua khóa học, tự động chuyển thành nút **"Vào học ngay"**.

---

## 3. CẤU HÌNH TÀI KHOẢN NGÂN HÀNG (appsettings.json)

Trong file `EduFlyUp/EduFlyUp.Web/appsettings.json`:

```json
{
  "SePay": {
    "ApiKey": "SEPAY_WEBHOOK_KEY",
    "BankBin": "970423",
    "BankName": "TPBank",
    "BankAccountNumber": "25816798888",
    "AccountHolderName": "CHAU VUONG HOANG"
  }
}
```

*Bạn có thể chỉnh sửa `BankBin`, `BankName`, `BankAccountNumber`, `AccountHolderName` theo đúng tài khoản ngân hàng của bạn.*

### Danh sách mã BIN ngân hàng thông dụng:
- **MBBank**: `970422`
- **Vietcombank**: `970436`
- **Techcombank**: `970407`
- **ACB**: `970416`
- **VPBank**: `970432`
- **TPBank**: `970423`
- **Timo / BanVietBank**: `970454`

---

## 4. HƯỚNG DẪN KIỂM THỬ TẠI LOCAL (TEST WEBHOOK)

### Kịch bản 1: Kiểm thử trên giao diện Web
1. Chạy dự án:
   ```powershell
   dotnet run --project EduFlyUp/EduFlyUp.Web
   ```
2. Mở trình duyệt vào [http://localhost:5298/Courses](http://localhost:5298/Courses), chọn một khóa học bất kỳ.
3. Bấm **"Đăng ký học ngay"** (hệ thống sẽ yêu cầu đăng nhập nếu chưa đăng nhập).
4. Bạn sẽ thấy màn hình Thanh toán hiển thị mã VietQR kèm mã đơn, ví dụ: **`EDUPAY648291`**.

### Kịch bản 2: Bắn Webhook giả lập để kích hoạt khóa học
Mở một cửa sổ PowerShell mới và chạy lệnh sau (thay mã `EDUPAY648291` bằng mã đơn đang hiện trên màn hình của bạn):

```powershell
$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Apikey SEPAY_WEBHOOK_KEY"
}

$body = @{
    id = 999999
    gateway = "MBBank"
    transactionDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    accountNumber = "0987654321"
    content = "EDUPAY648291 CHUYEN TIEN"
    transferType = "in"
    transferAmount = 599000
    referenceCode = "TEST_REF_123"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5298/api/payments/sepay-webhook" -Method POST -Headers $headers -Body $body
```

👉 **Kết quả**: Ngay khi lệnh trên chạy, màn hình Checkout trên trình duyệt sẽ tự động thông báo **"Thanh toán thành công!"** và chuyển hướng bạn thẳng vào khóa học!
