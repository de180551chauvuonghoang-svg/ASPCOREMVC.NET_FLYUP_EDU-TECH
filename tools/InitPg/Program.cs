using System;
using System.Text;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace InitPg;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var sqlConnStr = "Server=DESKTOP-IJV2BTH\\SQLEXPRESS;Database=EduFlyUpDb;Trusted_Connection=True;TrustServerCertificate=True;";
        var pgConnStr = "Host=thomas.proxy.rlwy.net;Port=52251;Database=railway;Username=postgres;Password=JGBdFelMwXyPVDJRtpgdthIrGgApeTkz;SSL Mode=Prefer;Trust Server Certificate=true;";

        // 1. Thêm / cập nhật khóa học trên SQL Server
        try
        {
            using var sqlConn = new SqlConnection(sqlConnStr);
            sqlConn.Open();

            var sqlScript = @"
                -- Kiểm tra bài học cho các khóa 2000đ và 3000đ
                DECLARE @cId1 INT = (SELECT Id FROM Courses WHERE Title LIKE N'%Test Thanh Toán%2.000%');
                IF @cId1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Lessons WHERE CourseId = @cId1)
                BEGIN
                    INSERT INTO Lessons (CourseId, Title, Content, VideoUrl, DurationMinutes, [Order], IsPreview, SectionName, MaterialUrl, AttachmentName, CreatedAt)
                    VALUES 
                    (@cId1, N'Bài 1: Xác nhận thanh toán thành công', N'Chúc mừng bạn đã thanh toán thành công khóa học 2.000đ qua SePay!', '', 10, 1, 0, N'Chương 1: Bắt đầu', '', '', GETUTCDATE()),
                    (@cId1, N'Bài 2: Hướng dẫn học tập', N'Nội dung bài học tiếp theo...', '', 15, 2, 0, N'Chương 1: Bắt đầu', '', '', GETUTCDATE());
                END

                DECLARE @cId2 INT = (SELECT Id FROM Courses WHERE Title LIKE N'%Test Thanh Toán%3.000%');
                IF @cId2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Lessons WHERE CourseId = @cId2)
                BEGIN
                    INSERT INTO Lessons (CourseId, Title, Content, VideoUrl, DurationMinutes, [Order], IsPreview, SectionName, MaterialUrl, AttachmentName, CreatedAt)
                    VALUES 
                    (@cId2, N'Bài 1: Xác nhận thanh toán thành công', N'Chúc mừng bạn đã thanh toán thành công khóa học 3.000đ qua SePay!', '', 10, 1, 0, N'Chương 1: Bắt đầu', '', '', GETUTCDATE());
                END
            ";
            using var cmd = new SqlCommand(sqlScript, sqlConn);
            cmd.ExecuteNonQuery();
            Console.WriteLine("✅ Đã cập nhật bài học cho khóa 2.000đ và 3.000đ trên SQL Server local thành công!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Lỗi SQL Server: " + ex.Message);
        }

        // Kiểm tra danh sách khóa học trên cả 2 DB
        Console.WriteLine("\n=== DANH SÁCH KHÓA HỌC TRÊN SQL SERVER ===");
        using (var sqlConn = new SqlConnection(sqlConnStr))
        {
            sqlConn.Open();
            using var cmd = new SqlCommand("SELECT Id, Title, Price, IsPublished FROM Courses ORDER BY Price ASC", sqlConn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Console.WriteLine($"ID: {r["Id"]} | Giá: {Convert.ToDecimal(r["Price"]):N0} đ | Published: {r["IsPublished"]} | Tiêu đề: {r["Title"]}");
            }
        }

        Console.WriteLine("\n=== DANH SÁCH KHÓA HỌC TRÊN POSTGRESQL RAILWAY ===");
        using (var pgConn = new NpgsqlConnection(pgConnStr))
        {
            pgConn.Open();
            using var cmd = new NpgsqlCommand("SELECT \"Id\", \"Title\", \"Price\", \"IsPublished\" FROM \"Courses\" ORDER BY \"Price\" ASC", pgConn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Console.WriteLine($"ID: {r["Id"]} | Giá: {Convert.ToDecimal(r["Price"]):N0} đ | Published: {r["IsPublished"]} | Tiêu đề: {r["Title"]}");
            }
        }
    }
}
