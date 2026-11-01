using ClosedXML.Excel;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Instructor;

public static class InstructorStudentWorkbook
{
    public static byte[] Create(InstructorStudentExport report)
    {
        using var workbook = new XLWorkbook();
        var info = workbook.Worksheets.Add("Thong tin");
        string[][] notes =
        [
            ["Báo cáo học viên và kết quả", report.Roster.CourseTitle],
            ["Mã khóa học", report.Roster.CourseId.ToString()],
            ["Tìm kiếm", report.Roster.Search ?? ""],
            ["Bộ lọc trạng thái", report.Roster.StatusFilter.HasValue ? InstructorReportDisplay.Status(report.Roster.StatusFilter.Value) : "Tất cả"],
            ["Phạm vi", "Toàn bộ học viên khớp bộ lọc, không chỉ trang đang xem."],
            ["Thời gian", "Các ngày giờ hiển thị theo giờ Việt Nam (UTC+7)."],
            ["Tiến độ bài học (%)", "Chưa có dữ liệu theo từng bài học; không suy ra từ trạng thái ghi danh."],
            ["Đã hoàn thành", "Theo trạng thái Completed trong dữ liệu ghi danh, trong phạm vi bộ lọc."],
            ["Điểm thi", "Giá trị đã lưu; chỉ công bố điểm và Đạt/Trượt khi có thời gian nộp bài."],
            ["Điểm cao nhất", "Tính riêng từng đề thi từ các lần đã nộp; không so sánh giữa các đề hoặc quy đổi sang %."],
            ["Giới hạn", "Tối đa 10.000 ghi danh và 50.000 dòng lịch sử trong một lần xuất."]
        ];
        for (var i = 0; i < notes.Length; i++)
        {
            Text(info.Cell(i + 1, 1), notes[i][0]); Text(info.Cell(i + 1, 2), notes[i][1]);
        }
        info.Column(1).Width = 28; info.Column(2).Width = 95;
        info.Column(2).Style.Alignment.WrapText = true;
        info.Rows().Height = 32;
        info.Column(1).Style.Font.Bold = true;

        WriteStudents(workbook, "Hoc vien", report.Roster.Students);
        WriteStudents(workbook, "Da hoan thanh", report.Roster.Students.Where(s => s.Status == EnrollmentStatus.Completed));
        var history = Sheet(workbook, "Lich su diem", "Mã ghi danh", "Mã học viên", "Họ tên", "Email", "Mã đề thi", "Đề thi", "Loại đề", "Mã lần thi", "Bắt đầu (UTC+7)", "Nộp bài (UTC+7)", "Điểm", "Kết quả");
        var row = 2;
        foreach (var item in report.Attempts)
        {
            Identity(history, row, item);
            Text(history.Cell(row, 7), item.Attempt.IsCAT ? "CAT" : "Tiêu chuẩn");
            history.Cell(row, 8).Value = item.Attempt.Id;
            Date(history.Cell(row, 9), item.Attempt.StartedAt);
            Date(history.Cell(row, 10), item.Attempt.CompletedAt);
            if (item.Attempt.CompletedAt.HasValue && item.Attempt.Score.HasValue) history.Cell(row, 11).Value = item.Attempt.Score.Value;
            Text(history.Cell(row, 12), item.Attempt.CompletedAt is null ? "Chưa nộp" : item.Attempt.Passed == true ? "Đạt" : "Trượt");
            row++;
        }
        Finish(history, row - 1, 12);

        var highest = Sheet(workbook, "Diem cao nhat", "Mã ghi danh", "Mã học viên", "Họ tên", "Email", "Mã đề thi", "Đề thi", "Điểm cao nhất đã nộp", "Số lần đã nộp");
        row = 2;
        foreach (var group in report.Attempts.Where(a => a.Attempt.CompletedAt.HasValue && a.Attempt.Score.HasValue)
                     .GroupBy(a => new { a.EnrollmentId, a.QuizId }))
        {
            Identity(highest, row, group.First());
            highest.Cell(row, 7).Value = group.Max(a => a.Attempt.Score!.Value);
            highest.Cell(row, 8).Value = group.Count(); row++;
        }
        Finish(highest, row - 1, 8);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Identity(IXLWorksheet sheet, int row, InstructorStudentAttemptExport item)
    {
        sheet.Cell(row, 1).Value = item.EnrollmentId;
        Text(sheet.Cell(row, 2), item.UserId); Text(sheet.Cell(row, 3), item.FullName); Text(sheet.Cell(row, 4), item.Email);
        sheet.Cell(row, 5).Value = item.QuizId; Text(sheet.Cell(row, 6), item.Attempt.QuizTitle);
    }

    private static void WriteStudents(XLWorkbook workbook, string name, IEnumerable<InstructorStudentRow> students)
    {
        var sheet = Sheet(workbook, name, "Mã ghi danh", "Mã học viên", "Họ tên", "Email", "Ngày ghi danh (UTC+7)", "Trạng thái", "Hoàn thành (UTC+7)", "Tiến độ bài học (%)");
        var row = 2;
        foreach (var student in students)
        {
            sheet.Cell(row, 1).Value = student.EnrollmentId; Text(sheet.Cell(row, 2), student.UserId);
            Text(sheet.Cell(row, 3), student.FullName); Text(sheet.Cell(row, 4), student.Email);
            Date(sheet.Cell(row, 5), student.EnrolledAt); Text(sheet.Cell(row, 6), InstructorReportDisplay.Status(student.Status));
            Date(sheet.Cell(row, 7), student.CompletedAt); Text(sheet.Cell(row, 8), "Chưa có dữ liệu"); row++;
        }
        Finish(sheet, row - 1, 8);
    }

    // Assign data as typed text, never FormulaA1, including names starting with =, +, - or @.
    private static void Text(IXLCell cell, string? value) => cell.Value = value ?? "";
    private static void Date(IXLCell cell, DateTime? value)
    {
        if (!value.HasValue) return;
        cell.Value = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).AddHours(7);
        cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
    }
    private static IXLWorksheet Sheet(XLWorkbook workbook, string name, params string[] headers)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var i = 0; i < headers.Length; i++) Text(sheet.Cell(1, i + 1), headers[i]);
        var heading = sheet.Range(1, 1, 1, headers.Length);
        heading.Style.Font.Bold = true; heading.Style.Font.FontColor = XLColor.White;
        heading.Style.Fill.BackgroundColor = XLColor.FromHtml("#234D73");
        sheet.SheetView.FreezeRows(1);
        return sheet;
    }
    private static void Finish(IXLWorksheet sheet, int lastRow, int columns)
    {
        sheet.Columns(1, columns).Width = 24;
        sheet.Column(3).Width = 32; sheet.Column(4).Width = 36;
        if (lastRow >= 2) sheet.Range(1, 1, lastRow, columns).CreateTable();
        sheet.Row(1).Height = 32;
        sheet.Row(1).Style.Alignment.WrapText = true;
    }
}
