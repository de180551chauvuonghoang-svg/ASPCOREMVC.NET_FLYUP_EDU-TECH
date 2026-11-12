using System;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace InitPg;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        var sqlConnStr = "Server=DESKTOP-IJV2BTH\\SQLEXPRESS;Database=EduFlyUpDb;Trusted_Connection=True;TrustServerCertificate=True;";
        var pgConnStr = "Host=thomas.proxy.rlwy.net;Port=52251;Database=railway;Username=postgres;Password=JGBdFelMwXyPVDJRtpgdthIrGgApeTkz;SSL Mode=Prefer;Trust Server Certificate=true;";

        using var sqlConn = new SqlConnection(sqlConnStr);
        sqlConn.Open();
        Console.WriteLine("✅ Kết nối thành công SQL Server local!");

        using var pgConn = new NpgsqlConnection(pgConnStr);
        pgConn.Open();
        Console.WriteLine("✅ Kết nối thành công PostgreSQL Railway!");

        // 1. Đồng bộ bảng Courses và Lessons
        Console.WriteLine("\n[1/2] Đang đồng bộ toàn bộ Khóa học (Courses) và Bài học (Lessons)...");

        using (var cmd = new NpgsqlCommand(@"
            DELETE FROM ""Lessons"";
            DELETE FROM ""Courses"";
        ", pgConn))
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("   -> Đã làm sạch dữ liệu cũ trong Lessons và Courses trên Postgres.");
        }

        int courseCount = 0;
        using (var cmd = new SqlCommand(@"
            SELECT Id, Title, Description, ThumbnailUrl, Price, Level, InstructorId, 
                   IsPublished, CreatedAt, UpdatedAt, Status, Category, RejectionReason 
            FROM Courses ORDER BY Id", sqlConn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                using var pgCmd = new NpgsqlCommand(@"
                    INSERT INTO ""Courses"" (
                        ""Id"", ""Title"", ""Description"", ""ThumbnailUrl"", ""Price"", ""Level"",
                        ""InstructorId"", ""IsPublished"", ""CreatedAt"", ""UpdatedAt"", ""Status"",
                        ""Category"", ""RejectionReason""
                    ) OVERRIDING SYSTEM VALUE VALUES (
                        @Id, @Title, @Description, @ThumbnailUrl, @Price, @Level,
                        @InstructorId, @IsPublished, @CreatedAt, @UpdatedAt, @Status,
                        @Category, @RejectionReason
                    );
                ", pgConn);

                pgCmd.Parameters.AddWithValue("@Id", r["Id"]);
                pgCmd.Parameters.AddWithValue("@Title", r["Title"] ?? "");
                pgCmd.Parameters.AddWithValue("@Description", r["Description"] ?? "");
                pgCmd.Parameters.AddWithValue("@ThumbnailUrl", r["ThumbnailUrl"] ?? "");
                pgCmd.Parameters.AddWithValue("@Price", r["Price"]);
                pgCmd.Parameters.AddWithValue("@Level", r["Level"]);
                pgCmd.Parameters.AddWithValue("@InstructorId", r["InstructorId"] ?? "");
                pgCmd.Parameters.AddWithValue("@IsPublished", r["IsPublished"]);
                pgCmd.Parameters.AddWithValue("@CreatedAt", r["CreatedAt"]);
                pgCmd.Parameters.AddWithValue("@UpdatedAt", r["UpdatedAt"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@Status", r["Status"]);
                pgCmd.Parameters.AddWithValue("@Category", r["Category"] ?? "Công nghệ thông tin");
                pgCmd.Parameters.AddWithValue("@RejectionReason", r["RejectionReason"] ?? DBNull.Value);

                pgCmd.ExecuteNonQuery();
                courseCount++;
                Console.WriteLine($"   + Đã chèn Course Id={r["Id"]}: {r["Title"]}");
            }
        }

        int lessonCount = 0;
        using (var cmd = new SqlCommand(@"
            SELECT Id, Title, Content, VideoUrl, DurationMinutes, [Order], IsPreview,
                   CourseId, CreatedAt, UpdatedAt, SectionName, MaterialUrl, AttachmentName
            FROM Lessons ORDER BY Id", sqlConn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                using var pgCmd = new NpgsqlCommand(@"
                    INSERT INTO ""Lessons"" (
                        ""Id"", ""Title"", ""Content"", ""VideoUrl"", ""DurationMinutes"", ""Order"",
                        ""IsPreview"", ""CourseId"", ""CreatedAt"", ""UpdatedAt"", ""SectionName"",
                        ""MaterialUrl"", ""AttachmentName""
                    ) OVERRIDING SYSTEM VALUE VALUES (
                        @Id, @Title, @Content, @VideoUrl, @DurationMinutes, @Order,
                        @IsPreview, @CourseId, @CreatedAt, @UpdatedAt, @SectionName,
                        @MaterialUrl, @AttachmentName
                    );
                ", pgConn);

                pgCmd.Parameters.AddWithValue("@Id", r["Id"]);
                pgCmd.Parameters.AddWithValue("@Title", r["Title"] ?? "");
                pgCmd.Parameters.AddWithValue("@Content", r["Content"] ?? "");
                pgCmd.Parameters.AddWithValue("@VideoUrl", r["VideoUrl"] ?? "");
                pgCmd.Parameters.AddWithValue("@DurationMinutes", r["DurationMinutes"]);
                pgCmd.Parameters.AddWithValue("@Order", r["Order"]);
                pgCmd.Parameters.AddWithValue("@IsPreview", r["IsPreview"]);
                pgCmd.Parameters.AddWithValue("@CourseId", r["CourseId"]);
                pgCmd.Parameters.AddWithValue("@CreatedAt", r["CreatedAt"]);
                pgCmd.Parameters.AddWithValue("@UpdatedAt", r["UpdatedAt"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@SectionName", r["SectionName"] ?? "Chương 1: Tổng quan");
                pgCmd.Parameters.AddWithValue("@MaterialUrl", r["MaterialUrl"] ?? "");
                pgCmd.Parameters.AddWithValue("@AttachmentName", r["AttachmentName"] ?? "");

                pgCmd.ExecuteNonQuery();
                lessonCount++;
                Console.WriteLine($"   + Đã chèn Lesson Id={r["Id"]}: {r["Title"]} (Course {r["CourseId"]})");
            }
        }

        // Đặt lại Sequence cho Courses và Lessons
        using (var cmd = new NpgsqlCommand(@"
            SELECT setval(pg_get_serial_sequence('""Courses""', 'Id'), COALESCE(MAX(""Id""), 1)) FROM ""Courses"";
            SELECT setval(pg_get_serial_sequence('""Lessons""', 'Id'), COALESCE(MAX(""Id""), 1)) FROM ""Lessons"";
        ", pgConn))
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("   -> Đã cập nhật lại Auto-Increment Sequence cho Courses & Lessons!");
        }

        // 2. Đồng bộ các Users từ SQL Server sang Postgres (nếu user chưa tồn tại)
        Console.WriteLine("\n[2/2] Đang đồng bộ AspNetUsers...");
        int userCount = 0;
        using (var cmd = new SqlCommand(@"
            SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, 
                   EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, 
                   PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, 
                   LockoutEnabled, AccessFailedCount, FullName, Bio, AvatarUrl, 
                   IsActive, CreatedAt 
            FROM AspNetUsers", sqlConn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                using var pgCmd = new NpgsqlCommand(@"
                    INSERT INTO ""AspNetUsers"" (
                        ""Id"", ""UserName"", ""NormalizedUserName"", ""Email"", ""NormalizedEmail"",
                        ""EmailConfirmed"", ""PasswordHash"", ""SecurityStamp"", ""ConcurrencyStamp"",
                        ""PhoneNumber"", ""PhoneNumberConfirmed"", ""TwoFactorEnabled"", ""LockoutEnd"",
                        ""LockoutEnabled"", ""AccessFailedCount"", ""FullName"", ""Bio"", ""AvatarUrl"",
                        ""IsActive"", ""CreatedAt""
                    ) VALUES (
                        @Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail,
                        @EmailConfirmed, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
                        @PhoneNumber, @PhoneNumberConfirmed, @TwoFactorEnabled, @LockoutEnd,
                        @LockoutEnabled, @AccessFailedCount, @FullName, @Bio, @AvatarUrl,
                        @IsActive, @CreatedAt
                    )
                    ON CONFLICT (""Id"") DO UPDATE 
                    SET ""FullName"" = COALESCE(EXCLUDED.""FullName"", ""AspNetUsers"".""FullName""),
                        ""Bio"" = COALESCE(EXCLUDED.""Bio"", ""AspNetUsers"".""Bio""),
                        ""AvatarUrl"" = COALESCE(EXCLUDED.""AvatarUrl"", ""AspNetUsers"".""AvatarUrl"");
                ", pgConn);

                pgCmd.Parameters.AddWithValue("@Id", r["Id"]);
                pgCmd.Parameters.AddWithValue("@UserName", r["UserName"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@NormalizedUserName", r["NormalizedUserName"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@Email", r["Email"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@NormalizedEmail", r["NormalizedEmail"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@EmailConfirmed", r["EmailConfirmed"]);
                pgCmd.Parameters.AddWithValue("@PasswordHash", r["PasswordHash"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@SecurityStamp", r["SecurityStamp"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@ConcurrencyStamp", r["ConcurrencyStamp"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@PhoneNumber", r["PhoneNumber"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@PhoneNumberConfirmed", r["PhoneNumberConfirmed"]);
                pgCmd.Parameters.AddWithValue("@TwoFactorEnabled", r["TwoFactorEnabled"]);
                pgCmd.Parameters.AddWithValue("@LockoutEnd", r["LockoutEnd"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@LockoutEnabled", r["LockoutEnabled"]);
                pgCmd.Parameters.AddWithValue("@AccessFailedCount", r["AccessFailedCount"]);
                pgCmd.Parameters.AddWithValue("@FullName", r["FullName"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@Bio", r["Bio"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@AvatarUrl", r["AvatarUrl"] ?? DBNull.Value);
                pgCmd.Parameters.AddWithValue("@IsActive", r["IsActive"]);
                pgCmd.Parameters.AddWithValue("@CreatedAt", r["CreatedAt"]);

                try
                {
                    pgCmd.ExecuteNonQuery();
                    userCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ! Bỏ qua user {r["UserName"]}: {ex.Message}");
                }
            }
            Console.WriteLine($"   -> Đồng bộ {userCount} Users hoàn tất!");
        }

        Console.WriteLine($"\n🎉 TẤT CẢ DỮ LIỆU ĐÃ ĐƯỢC ĐỒNG BỘ SANG POSTGRESQL THÀNH CÔNG!");
        Console.WriteLine($"📊 Tổng kết: {courseCount} Khóa học, {lessonCount} Bài học, {userCount} Người dùng!");
    }
}
