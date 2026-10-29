# UC-24 Tải lên tài liệu bài giảng

## Sử dụng

Đăng nhập bằng Instructor sở hữu khóa học hoặc Admin, vào Studio Giảng viên → Giáo trình. Với bài giảng chưa có tài liệu, chọn file trong danh sách rồi bấm **Tải lên**. Với bài giảng mới, lưu bài giảng trước để có ID. Sau khi thành công, tên tài liệu trở thành liên kết tải xuống.

Hỗ trợ PDF, DOC, DOCX, PPT, PPTX; dung lượng tối đa 10 MiB (giao diện ghi 10 MB). Server kiểm tra phần mở rộng, MIME và chữ ký định dạng cơ bản; DOCX/PPTX phải có entry tài liệu tương ứng trong ZIP và không chứa vbaProject.bin. Kiểm tra này không phải công cụ xác minh toàn bộ nội dung tài liệu.

Tài liệu được lưu trong `EduFlyUp.Web/App_Data/materials` bằng tên GUID. Thư mục này nằm ngoài wwwroot, được bỏ qua bởi Git và cần được giữ cùng database khi sao lưu hoặc triển khai lại. `MaterialUrl` lưu đường dẫn tải có kiểm tra quyền; `AttachmentName` lưu tên hiển thị. Không cần migration mới.

## Quyền truy cập

- POST `/LessonMaterials/Upload`: yêu cầu cookie, role Admin/Instructor, quyền quản lý đúng khóa học và anti-forgery token. ID khóa học được lấy từ bài giảng trong database.
- GET `/LessonMaterials/Download/{id}/{fileName}`: yêu cầu cookie và `CanLearnCourseAsync`: Admin, Instructor sở hữu hoặc học viên có Enrollment Active/Completed. Khóa học công khai không tự cấp quyền tải. Bài học preview chưa mở tải file cho Guest trong bước này.
- Tên file trên route phải khớp metadata của đúng bài giảng; storage chỉ nhận tên GUID và phần mở rộng cho phép. Phản hồi buộc tải xuống, không cache và có nosniff.
- Khi lưu metadata thất bại, controller dọn file mới. Sửa nội dung bài giảng giữ nguyên tài liệu upload; các trường link ngoài không ghi đè metadata file upload.

## Phạm vi bước đầu

Chỉ thêm tài liệu khi bài giảng chưa có MaterialUrl. Tài liệu qua link ngoài vẫn sử dụng luồng hiện có. Thay thế/xóa file, dọn file khi xóa bài giảng/khóa học và upload video là các bước sau. Chưa có bảo đảm đồng thời cho hai upload/sửa cùng một bài giảng; DAO bài giảng hiện cập nhật toàn entity.

## Kiểm tra

```powershell
dotnet build EduFlyUp/EduFlyUp.slnx --configuration Release --no-restore --nologo
dotnet test EduFlyUp/EduFlyUp.Tests/EduFlyUp.Tests.csproj --configuration Release --no-restore --nologo
dotnet test tools/AdminDemo.Tests/AdminDemo.Tests.csproj --configuration Release --no-restore --nologo
```

Kiểm thử mới dùng HTTP TestServer với cookie/anti-forgery thật và storage giả để kiểm tra quyền, lưu/tải, giữ tài liệu khi sửa và dọn file khi lỗi. Kiểm thử storage riêng ghi file thật vào thư mục tạm để kiểm tra tên file, giới hạn, định dạng và chặn đường dẫn vượt thư mục. Không chạy startup/migrations hoặc thay đổi database ứng dụng. Chưa kiểm tra giao diện bằng trình duyệt hoặc persistence trên SQL Server thật.
