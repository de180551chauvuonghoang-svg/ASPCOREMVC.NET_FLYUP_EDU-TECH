import os
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE

prs = Presentation()
prs.slide_width = Inches(13.333)
prs.slide_height = Inches(7.5)
blank_layout = prs.slide_layouts[6]

DARK_BG = RGBColor(15, 23, 42)      # Slate 900
PRIMARY = RGBColor(37, 99, 235)     # Blue 600
ACCENT = RGBColor(14, 165, 233)     # Sky 500
CARD_BG = RGBColor(30, 41, 59)      # Slate 800
TEXT_WHITE = RGBColor(248, 250, 252)
TEXT_MUTED = RGBColor(148, 163, 184)
GREEN_ACCENT = RGBColor(16, 185, 129)

def add_header(slide, title_text, category_text="BÁO CÁO ĐỒ ÁN MÔN HỌC"):
    cat_box = slide.shapes.add_textbox(Inches(0.8), Inches(0.5), Inches(11.5), Inches(0.4))
    tf_c = cat_box.text_frame
    tf_c.word_wrap = True
    p_c = tf_c.paragraphs[0]
    p_c.text = category_text.upper()
    p_c.font.size = Pt(11)
    p_c.font.bold = True
    p_c.font.color.rgb = ACCENT

    title_box = slide.shapes.add_textbox(Inches(0.8), Inches(0.85), Inches(11.5), Inches(0.8))
    tf_t = title_box.text_frame
    tf_t.word_wrap = True
    p_t = tf_t.paragraphs[0]
    p_t.text = title_text
    p_t.font.size = Pt(24)
    p_t.font.bold = True
    p_t.font.color.rgb = TEXT_WHITE

def create_card(slide, left, top, width, height, title, content_list, border_color=None):
    shape = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, left, top, width, height)
    shape.fill.solid()
    shape.fill.fore_color.rgb = CARD_BG
    if border_color:
        shape.line.color.rgb = border_color
        shape.line.width = Pt(1.5)
    else:
        shape.line.color.rgb = RGBColor(51, 65, 85)
        shape.line.width = Pt(1)

    tf = shape.text_frame
    tf.word_wrap = True
    tf.margin_left = Inches(0.3)
    tf.margin_right = Inches(0.3)
    tf.margin_top = Inches(0.3)
    tf.margin_bottom = Inches(0.3)
    
    p0 = tf.paragraphs[0]
    p0.text = title
    p0.font.size = Pt(16)
    p0.font.bold = True
    p0.font.color.rgb = TEXT_WHITE
    p0.space_after = Pt(10)

    for item in content_list:
        p = tf.add_paragraph()
        p.text = "• " + item
        p.font.size = Pt(13)
        p.font.color.rgb = TEXT_MUTED
        p.space_after = Pt(6)

# ----------------- SLIDE 1: BÌA -----------------
s1 = prs.slides.add_slide(blank_layout)
bg1 = s1.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg1.fill.solid()
bg1.fill.fore_color.rgb = DARK_BG
bg1.line.fill.background()

card1 = s1.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(1.5), Inches(1.2), Inches(10.333), Inches(5.1))
card1.fill.solid()
card1.fill.fore_color.rgb = CARD_BG
card1.line.color.rgb = PRIMARY
card1.line.width = Pt(2)

tf1 = card1.text_frame
tf1.word_wrap = True
tf1.margin_left = Inches(0.8)
tf1.margin_right = Inches(0.8)
tf1.margin_top = Inches(0.8)

p = tf1.paragraphs[0]
p.text = "BÁO CÁO TIẾN ĐỘ ĐỒ ÁN MÔN HỌC"
p.font.size = Pt(14)
p.font.bold = True
p.font.color.rgb = ACCENT
p.space_after = Pt(14)

p2 = tf1.add_paragraph()
p2.text = "EDUFLYUP - NỀN TẢNG ĐÀO TẠO & PHÂN PHỐI KHÓA HỌC TRỰC TUYẾN"
p2.font.size = Pt(26)
p2.font.bold = True
p2.font.color.rgb = TEXT_WHITE
p2.space_after = Pt(20)

p3 = tf1.add_paragraph()
p3.text = "Giảng viên hướng dẫn: ThS. Trần Thị Tố Tâm"
p3.font.size = Pt(16)
p3.font.color.rgb = GREEN_ACCENT
p3.font.bold = True
p3.space_after = Pt(10)

p4 = tf1.add_paragraph()
p4.text = "Công nghệ: ASP.NET Core 8 MVC • PostgreSQL • VietQR & SePay • Clean N-Tier"
p4.font.size = Pt(13)
p4.font.color.rgb = TEXT_MUTED

# ----------------- SLIDE 2: GIỚI THIỆU DỰ ÁN -----------------
s2 = prs.slides.add_slide(blank_layout)
bg2 = s2.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg2.fill.solid()
bg2.fill.fore_color.rgb = DARK_BG
bg2.line.fill.background()
add_header(s2, "1. Giới Thiệu Dự Án EduFlyUp")

create_card(s2, Inches(0.8), Inches(1.8), Inches(3.6), Inches(5.0), "Bối Cảnh & Đặt Vấn Đề", [
    "Nhu cầu tự học CNTT & kỹ năng số ngày càng gia tăng.",
    "Học viên thường học thụ động, thiếu cơ chế kiểm chứng năng lực thực tế.",
    "Thanh toán qua cổng quốc tế rườm rà, phí cao, chưa tối ưu cho người học Việt Nam."
])

create_card(s2, Inches(4.8), Inches(1.8), Inches(3.6), Inches(5.0), "Giải Pháp EduFlyUp", [
    "Hệ thống phân phối khóa học chuyên nghiệp chuẩn LMS hiện đại.",
    "Tích hợp video bài giảng, tài liệu đính kèm, ghi chú học tập trực tiếp.",
    "Cơ chế Final Test chuẩn hóa kết hợp kiểm duyệt tiến độ để cấp chứng chỉ.",
    "Thanh toán tự động 100% bằng VietQR qua webhook SePay tức thì."
], border_color=PRIMARY)

create_card(s2, Inches(8.8), Inches(1.8), Inches(3.6), Inches(5.0), "Mục Tiêu & Định Hướng", [
    "Cung cấp trải nghiệm học tập mượt mà, minh bạch và tin cậy.",
    "Đảm bảo học viên đạt đủ 95% tiến độ & 95 điểm kiểm tra mới nhận chứng chỉ.",
    "Kiến trúc mở, bảo mật, sẵn sàng chịu tải và dễ dàng mở rộng tính năng."
])

# ----------------- SLIDE 3: PHÂN CÔNG NHIỆM VỤ -----------------
s3 = prs.slides.add_slide(blank_layout)
bg3 = s3.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg3.fill.solid()
bg3.fill.fore_color.rgb = DARK_BG
bg3.line.fill.background()
add_header(s3, "2. Phân Công Nhiệm Vụ Các Thành Viên")

create_card(s3, Inches(0.8), Inches(1.8), Inches(3.6), Inches(5.0), "Trưởng Nhóm & Backend Core", [
    "Phụ trách kiến trúc phần mềm N-Tier (Services, Repositories, DAO).",
    "Thiết kế cơ sở dữ liệu Entity Framework Core & ASP.NET Identity.",
    "Tích hợp SePay Webhook xử lý giao dịch VietQR tự động.",
    "Cài đặt thuật toán đánh giá điều kiện cấp Chứng chỉ và Final Test."
])

create_card(s3, Inches(4.8), Inches(1.8), Inches(3.6), Inches(5.0), "Frontend & UI/UX Developer", [
    "Thiết kế giao diện hiện đại với Tailwind CSS và Razor Views.",
    "Phát triển trang học tập tích hợp (Video Player, Quiz, Sidebar tiến độ).",
    "Xây dựng trang cá nhân (Profile, Lịch sử giao dịch, Quản lý chứng chỉ).",
    "Đảm bảo trải nghiệm tương thích trên thiết bị di động (Responsive)."
])

create_card(s3, Inches(8.8), Inches(1.8), Inches(3.6), Inches(5.0), "QA, Tester & Database/DevOps", [
    "Viết và duy trì bộ Unit & Integration Tests (246 test cases).",
    "Kiểm thử bảo mật CSRF, XSS, cơ chế chống khóa tài khoản Admin cuối.",
    "Thiết lập CI/CD pipeline tự động build và deploy lên Railway.",
    "Chuẩn bị dữ liệu mẫu và kịch bản thuyết trình/demo đồ án."
])

# ----------------- SLIDE 4: TOOL QUẢN LÝ DỰ ÁN -----------------
s4 = prs.slides.add_slide(blank_layout)
bg4 = s4.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg4.fill.solid()
bg4.fill.fore_color.rgb = DARK_BG
bg4.line.fill.background()
add_header(s4, "3. Công Cụ Quản Lý & Quy Trình Lên Kế Hoạch")

create_card(s4, Inches(0.8), Inches(1.8), Inches(5.6), Inches(5.0), "Mô Hình Quản Lý Agile / Scrum", [
    "Phân chia dự án thành 4 Sprints rõ ràng (Mỗi sprint 1-2 tuần).",
    "Sprint 1: Phân tích nghiệp vụ, thiết kế DB và khung N-Tier.",
    "Sprint 2: Quản lý khóa học, bài học video, phân quyền Identity.",
    "Sprint 3: Tích hợp thanh toán SePay, bài kiểm tra Quiz & Final Test.",
    "Sprint 4: Cấp chứng chỉ, kiểm thử toàn diện 246 tests, tối ưu UI & deploy."
])

create_card(s4, Inches(6.8), Inches(1.8), Inches(5.6), Inches(5.0), "Công Cụ Hỗ Trợ Đắc Lực", [
    "GitHub Projects / Trello: Theo dõi tiến độ công việc theo bảng Kanban (To Do, In Progress, Review, Done).",
    "Git Flow: Chiến lược phân nhánh rõ ràng (main, feature branches, pull request có code review).",
    "Postman: Kiểm thử tích hợp Webhook thanh toán SePay và API.",
    "Railway & Neon PostgreSQL: Nền tảng Cloud tự động hóa deploy liên tục (CI/CD) mỗi khi commit vào nhánh main."
], border_color=ACCENT)

# ----------------- SLIDE 5: KIẾN TRÚC PHẦN MỀM -----------------
s5 = prs.slides.add_slide(blank_layout)
bg5 = s5.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg5.fill.solid()
bg5.fill.fore_color.rgb = DARK_BG
bg5.line.fill.background()
add_header(s5, "4. Kiến Trúc Phần Mềm & Công Nghệ Sử Dụng")

create_card(s5, Inches(0.8), Inches(1.8), Inches(5.6), Inches(5.0), "Mô Hình Kiến Trúc N-Tier Tách Biệt", [
    "EduFlyUp.Web: Tầng hiển thị MVC, Controllers, ViewModels, Razor Views.",
    "EduFlyUp.Services: Tầng nghiệp vụ xử lý logic chứng chỉ, bài thi, thanh toán.",
    "EduFlyUp.Repositories: Lớp trừu tượng hóa truy cập dữ liệu (Repository Pattern).",
    "EduFlyUp.DataAccess: Tầng tương tác DB trực tiếp (DAO, AppDbContext).",
    "EduFlyUp.BusinessObjects: Các thực thể mô hình dữ liệu (Models/Entities).",
    "EduFlyUp.Tests: Bộ kiểm thử độc lập cho từng module nghiệp vụ."
], border_color=PRIMARY)

create_card(s5, Inches(6.8), Inches(1.8), Inches(5.6), Inches(5.0), "Ngăn Xếp Công Nghệ (Tech Stack)", [
    "Backend: .NET 8, ASP.NET Core MVC, Entity Framework Core 8.",
    "Cơ sở dữ liệu: PostgreSQL (Production) / Microsoft SQL Server.",
    "Bảo mật: ASP.NET Core Identity, BCrypt Password Hashing, Antiforgery Tokens.",
    "Thanh toán: SePay API + VietQR động, xử lý Webhook tự động.",
    "Media & Tài liệu: Cloudinary CDN lưu trữ ảnh & video streaming.",
    "DevOps & Hosting: Docker, Railway Platform, GitHub Actions."
])

# ----------------- SLIDE 6: CÁC TÍNH NĂNG CHÍNH -----------------
s6 = prs.slides.add_slide(blank_layout)
bg6 = s6.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg6.fill.solid()
bg6.fill.fore_color.rgb = DARK_BG
bg6.line.fill.background()
add_header(s6, "5. Các Tính Năng Cốt Lõi Của Dự Án")

create_card(s6, Inches(0.8), Inches(1.8), Inches(3.6), Inches(5.0), "Học Tập & Tiến Độ", [
    "Trình phát video bài giảng chuyên nghiệp.",
    "Tài liệu học tập tải xuống an toàn.",
    "Ghi chú thời gian thực đồng bộ bài học.",
    "Đánh dấu hoàn thành và theo dõi % tiến độ chính xác theo từng bài giảng."
])

create_card(s6, Inches(4.8), Inches(1.8), Inches(3.6), Inches(5.0), "Kiểm Tra & Chứng Chỉ", [
    "Đề thi trắc nghiệm luyện tập theo từng chương.",
    "Khóa bài thi Final Test cho đến khi hoàn thành >= 95% bài học.",
    "Đạt điểm >= 95% bài Final Test sẽ tự động cấp Chứng chỉ tốt nghiệp.",
    "Bảo lưu chứng chỉ lần đầu, hỗ trợ học lại trọn đời không mất dữ liệu."
], border_color=GREEN_ACCENT)

create_card(s6, Inches(8.8), Inches(1.8), Inches(3.6), Inches(5.0), "Thanh Toán & Quản Trị", [
    "Thanh toán VietQR động, kích hoạt tự động sau 3 giây.",
    "Lịch sử giao dịch & hóa đơn trong Hồ sơ cá nhân.",
    "Quản trị người dùng với cơ chế chống tự khóa Admin cuối cùng.",
    "Bảng thống kê doanh thu và báo cáo tiến độ học viên của Giảng viên."
])

# ----------------- SLIDE 7: PHẦN KIỂM THỬ -----------------
s7 = prs.slides.add_slide(blank_layout)
bg7 = s7.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg7.fill.solid()
bg7.fill.fore_color.rgb = DARK_BG
bg7.line.fill.background()
add_header(s7, "6. Phần Kiểm Thử (Testing & Quality Assurance)")

create_card(s7, Inches(0.8), Inches(1.8), Inches(5.6), Inches(5.0), "Kết Quả Kiểm Thử (246/246 Passed)", [
    "Framework: xUnit, Moq, EF Core InMemory & TestContainers.",
    "Tổng số Unit & Integration Tests: 246 tests (Đạt 100% Pass).",
    "Module hóa thư mục test theo nghiệp vụ:",
    "  • Tests/User: AccountSecurity, AdminUser, StatusLock.",
    "  • Tests/Course & Lesson: CourseAccess, Video, Material.",
    "  • Tests/Instructor: Payout, RevenueShare, Settlements.",
    "  • Tests/Coupon: Quản lý mã giảm giá và hạn mức sử dụng."
], border_color=GREEN_ACCENT)

create_card(s7, Inches(6.8), Inches(1.8), Inches(5.6), Inches(5.0), "Các Trường Hợp Kiểm Thử Trọng Yếu", [
    "Bảo mật xác thực: Thu hồi Auth Cookie ngay lập tức khi tài khoản bị vô hiệu hóa hoặc thay đổi SecurityStamp.",
    "Ngăn ngừa tấn công: Chặn hoàn toàn lỗ hổng XSS trên bio/avatar và Open Redirect qua tham số returnUrl.",
    "Giao dịch đồng thời (Concurrency): Bảo vệ nguyên tắc không bao giờ để mất Admin hoạt động cuối cùng.",
    "Toàn vẹn dữ liệu: Tự động rollback trạng thái khi có lỗi ghi database ở các giao dịch nhạy cảm."
])

# ----------------- SLIDE 8: KỊCH BẢN DEMO -----------------
s8 = prs.slides.add_slide(blank_layout)
bg8 = s8.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Inches(13.333), Inches(7.5))
bg8.fill.solid()
bg8.fill.fore_color.rgb = DARK_BG
bg8.line.fill.background()
add_header(s8, "7. Kịch Bản Demo Trực Tiếp & Kết Luận")

create_card(s8, Inches(0.8), Inches(1.8), Inches(5.6), Inches(5.0), "Quy Trình Demo Trực Tiếp", [
    "Bước 1: Học viên đăng ký/đăng nhập tài khoản, xem danh sách khóa học.",
    "Bước 2: Chọn khóa học và thanh toán quét mã VietQR tự động qua SePay.",
    "Bước 3: Truy cập Khóa học của tôi, trải nghiệm video và tài liệu.",
    "Bước 4: Hoàn thành bài học đạt >= 95% -> Mở khóa làm bài thi Final Test.",
    "Bước 5: Hoàn thành bài thi đạt >= 95% -> Nhận Chứng chỉ tốt nghiệp và kiểm tra lịch sử giao dịch trong Profile."
], border_color=PRIMARY)

create_card(s8, Inches(6.8), Inches(1.8), Inches(5.6), Inches(5.0), "Kết Quả Đạt Được & Lời Cảm Ơn", [
    "Hoàn thành toàn diện các yêu cầu nghiệp vụ của đề tài môn học.",
    "Hệ thống đã được đóng gói và triển khai thực tế trên môi trường Cloud.",
    "Mã nguồn được tổ chức chặt chẽ, kiểm thử tự động đạt độ tin cậy cao.",
    "Xin chân thành cảm ơn cô Trần Thị Tố Tâm đã tận tình hướng dẫn nhóm trong suốt quá trình thực hiện đồ án!"
])

output_path = r"c:\Users\RinHeo\Desktop\baitapprn\BaoCao_DoAn_EduFlyUp.pptx"
prs.save(output_path)
print(f"Presentation saved successfully to: {output_path}")
