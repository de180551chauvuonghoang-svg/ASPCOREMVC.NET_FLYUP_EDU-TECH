using System;
using System.IO;
using Npgsql;

namespace InitPg;

class Program
{
    static void Main(string[] args)
    {
        var connStr = "Host=thomas.proxy.rlwy.net;Port=52251;Database=railway;Username=postgres;Password=JGBdFelMwXyPVDJRtpgdthIrGgApeTkz;SSL Mode=Prefer;Trust Server Certificate=true;";
        using (var conn = new NpgsqlConnection(connStr))
        {
            conn.Open();
            Console.WriteLine("Ket noi thanh cong den PostgreSQL Railway!");
            var sqlPath = Path.Combine(AppContext.BaseDirectory, "postgresql_schema.sql");
            if (!File.Exists(sqlPath))
            {
                sqlPath = Path.GetFullPath(@"docs\database\postgresql_schema.sql");
            }
            var sql = File.ReadAllText(sqlPath);
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine("SUCCESS: Da tao thanh cong toan bo 17 bang tren PostgreSQL!");
        }
    }
}

