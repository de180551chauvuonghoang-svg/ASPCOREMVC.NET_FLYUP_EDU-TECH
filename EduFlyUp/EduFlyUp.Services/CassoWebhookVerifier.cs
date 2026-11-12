using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EduFlyUp.Services;

public sealed record CassoTransaction(long Id, long Amount, string Description, string AccountNumber,
    string BankAbbreviation, DateTimeOffset? OccurredAt);

public static class CassoWebhookVerifier
{
    public const int MaxBodyBytes = 32 * 1024;

    public static CassoTransaction? Verify(ReadOnlyMemory<byte> body, string? signature, string? signingKey)
    {
        if (body.Length is 0 or > MaxBodyBytes || string.IsNullOrWhiteSpace(signingKey) ||
            !TryReadSignature(signature, out var timestamp, out var suppliedHash))
            return null;

        try
        {
            using var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            using var message = new MemoryStream();
            message.Write(Encoding.ASCII.GetBytes(timestamp + "."));
            using (var writer = new Utf8JsonWriter(message, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
            {
                if (!WriteSorted(writer, root))
                    return null;
                writer.Flush();
            }

            // Casso V2 ký toàn bộ envelope JSON đã sắp xếp, cùng timestamp từ header.
            var hash = HMACSHA512.HashData(Encoding.UTF8.GetBytes(signingKey),
                message.GetBuffer().AsSpan(0, (int)message.Length));
            return CryptographicOperations.FixedTimeEquals(hash, suppliedHash) ? ReadTransaction(root) : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // JsonElement có thể gặp chuỗi UTF-8 hoặc cặp surrogate không hợp lệ.
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool TryReadSignature(string? header, out string timestamp, out byte[] hash)
    {
        timestamp = "";
        hash = [];
        if (header is null || header.Length > 160)
            return false;

        var parts = header.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[0].StartsWith("t=", StringComparison.Ordinal) ||
            !parts[1].StartsWith("v1=", StringComparison.Ordinal) || parts[1].Length != 131)
            return false;

        timestamp = parts[0][2..];
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) ||
            milliseconds <= 0 || milliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() ||
            timestamp != milliseconds.ToString(CultureInfo.InvariantCulture))
            return false;

        try
        {
            hash = Convert.FromHexString(parts[1].AsSpan(3));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool WriteSorted(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                string? previous = null;
                foreach (var field in value.EnumerateObject().OrderBy(field => field.Name, StringComparer.Ordinal))
                {
                    if (field.Name == previous)
                        return false;
                    writer.WritePropertyName(field.Name);
                    if (!WriteSorted(writer, field.Value))
                        return false;
                    previous = field.Name;
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    if (!WriteSorted(writer, item))
                        return false;
                }
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
        return true;
    }

    private static CassoTransaction? ReadTransaction(JsonElement root)
    {
        if (!TryReadInteger(root, "error", out var error) || error != 0 ||
            !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !TryReadInteger(data, "id", out var id) || id < 0 ||
            !TryReadInteger(data, "amount", out var amount) || amount == 0 ||
            amount < -BusinessObjects.CheckoutPolicy.MaxVndAmount || amount > BusinessObjects.CheckoutPolicy.MaxVndAmount ||
            !TryReadText(data, "description", 2048, out var description) ||
            !TryReadText(data, "accountNumber", 64, out var accountNumber) || string.IsNullOrWhiteSpace(accountNumber) ||
            !TryReadText(data, "bankAbbreviation", 32, out var bank) || string.IsNullOrWhiteSpace(bank) ||
            !TryReadText(data, "transactionDateTime", 19, out var time))
            return null;

        if (DateTime.TryParseExact(time, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return new(id, amount, description, accountNumber, bank, new DateTimeOffset(local, TimeSpan.FromHours(7)));

        // Một số ngân hàng chỉ cung cấp ngày, không suy ra giờ để đối chiếu thời hạn đơn.
        return DateOnly.TryParseExact(time, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? new(id, amount, description, accountNumber, bank, null)
            : null;
    }

    private static bool TryReadInteger(JsonElement value, string name, out long number)
    {
        number = 0;
        return value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number &&
            field.TryGetInt64(out number);
    }

    private static bool TryReadText(JsonElement value, string name, int limit, out string text)
    {
        text = "";
        if (!value.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            return false;
        text = field.GetString()!;
        return text.Length <= limit;
    }
}
