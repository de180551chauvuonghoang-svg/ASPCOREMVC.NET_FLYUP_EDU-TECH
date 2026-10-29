# UC-24 Tải lên tài liệu bài giảng

## Sử dụng

Đăng nhập bằng Instructor sở hữu khóa học hoặc Admin, vào Studio Giảng viên → Giáo trình. Với bài giảng chưa có tài liệu, chọn file trong danh sách rồi bấm **Tải lên**. Với bài giảng mới, lưu bài giảng trước để có ID. Sau khi thành công, tên tài liệu trở thành liên kết tải xuống.

Với bài giảng đã có tài liệu, chọn file mới rồi bấm **Thay tài liệu**, hoặc bấm **Xóa tài liệu** để gỡ đính kèm; cả hai thao tác đều yêu cầu xác nhận. Bài giảng, nội dung và video được giữ nguyên. Có thể thay link tài liệu ngoài bằng file upload; xóa link ngoài chỉ gỡ liên kết, không truy cập hoặc xóa tài nguyên bên ngoài.

Hỗ trợ PDF, DOC, DOCX, PPT, PPTX; dung lượng tối đa 10 MiB (giao diện ghi 10 MB). Server kiểm tra phần mở rộng, MIME và chữ ký định dạng cơ bản; DOCX/PPTX phải có entry tài liệu tương ứng trong ZIP và không chứa vbaProject.bin. Kiểm tra này không phải công cụ xác minh toàn bộ nội dung tài liệu.

Tài liệu được lưu trong `EduFlyUp.Web/App_Data/materials` bằng tên GUID. Thư mục này nằm ngoài wwwroot, được bỏ qua bởi Git và cần được giữ cùng database khi sao lưu hoặc triển khai lại. `MaterialUrl` lưu đường dẫn tải có kiểm tra quyền; `AttachmentName` lưu tên hiển thị. Không cần migration mới.

## Quyền truy cập

- POST `/LessonMaterials/Upload`: yêu cầu cookie, role Admin/Instructor, quyền quản lý đúng khóa học và anti-forgery token. ID khóa học được lấy từ bài giảng trong database.
- GET `/LessonMaterials/Download/{id}/{fileName}`: yêu cầu cookie và `CanLearnCourseAsync`: Admin, Instructor sở hữu hoặc học viên có Enrollment Active/Completed. Khóa học công khai không tự cấp quyền tải. Bài học preview chưa mở tải file cho Guest trong bước này.
- Tên file trên route phải khớp metadata của đúng bài giảng; storage chỉ nhận tên GUID và phần mở rộng cho phép. Phản hồi buộc tải xuống, không cache và có nosniff.
- Khi lưu metadata thất bại, controller dọn file mới. Sửa nội dung bài giảng giữ nguyên tài liệu upload; các trường link ngoài không ghi đè metadata file upload.
- POST `/LessonMaterials/Replace` và `/LessonMaterials/Remove`: yêu cầu cùng quyền/anti-forgery như upload, nhận `expectedUrl` từ form. DAO chỉ cập nhật hai trường tài liệu và timestamp khi URL hiện tại khớp giá trị mong đợi. Form cũ hoặc thao tác thua cập nhật có điều kiện không được ghi đè tài liệu mới.
- Thay thế: lưu file mới → cập nhật metadata có điều kiện → dọn file cũ. Nếu cập nhật thất bại hoặc thua thao tác khác, dọn file mới và giữ tài liệu hiện tại. Xóa: gỡ metadata trước khi dọn file. Nếu file cũ không xóa được (ví dụ đang được tải trên Windows), ghi log để dọn sau; URL cũ đã bị thu hồi. Không xóa file từ URL bên ngoài hoặc đường dẫn không hợp lệ.
- DAO sửa bài giảng cập nhật các trường nội dung bằng một lệnh riêng với điều kiện giữ metadata upload hiện tại. Bản sửa cũ không khôi phục đường dẫn file đã thay thế/xóa.

## Phạm vi hiện tại

Đã hỗ trợ thêm, thay thế, gỡ đính kèm và tải tài liệu. Dọn file khi xóa toàn bộ bài giảng/khóa học và upload video là các bước sau. Việc thay tài liệu dùng SQL cập nhật có điều kiện, không giữ transaction chung giữa SQL Server và filesystem. Cần giữ thư mục học liệu trên ổ lưu trữ bền vững; file còn dư do lỗi dọn được ghi log và không có tác vụ dọn nền tự động ở bước này.

## Kiểm tra

```powershell
dotnet build EduFlyUp/EduFlyUp.slnx --configuration Release --no-restore --nologo
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release --no-restore --nologo
dotnet test tools/AdminDemo.Tests/AdminDemo.Tests.csproj --configuration Release --no-restore --nologo
```

Kiểm thử mới dùng HTTP TestServer với cookie/anti-forgery thật và storage giả để kiểm tra quyền, lưu/tải, giữ tài liệu khi sửa và dọn file khi lỗi. Kiểm thử storage riêng ghi file thật vào thư mục tạm để kiểm tra tên file, giới hạn, định dạng và chặn đường dẫn vượt thư mục. Không chạy startup/migrations hoặc thay đổi database ứng dụng. Chưa kiểm tra giao diện bằng trình duyệt hoặc persistence trên SQL Server thật.

Bước thay/xóa có kiểm thử HTTP cho Instructor sở hữu và Admin, chặn Instructor khác/học viên/thiếu anti-forgery, giữ tài liệu cũ khi lỗi lưu hoặc form cũ, dọn file mới khi cập nhật có điều kiện thất bại, thu hồi download ngay cả khi dọn file cũ bị lỗi và gỡ link ngoài. Kiểm thử SQL translation chặn mở kết nối và thực thi lệnh, chỉ xác nhận EF dịch đúng câu lệnh production, không xác nhận thực thi trên server.

`LessonMaterialSqlServerTests.Conditional_material_updates_preserve_content_and_reject_stale_requests` cần `FLYUP_TEST_SQLSERVER` như các test SQL hiện có: fixture tạo database GUID riêng rồi xóa đúng database đó sau test. Test chạy hai cập nhật cạnh tranh từ hai context độc lập, xác nhận chỉ một thao tác đạt và bản sửa bài giảng cũ không phục hồi metadata. Không có biến môi trường thì test báo skip; chưa được coi là đã xác minh đồng thời trên SQL Server thật.
