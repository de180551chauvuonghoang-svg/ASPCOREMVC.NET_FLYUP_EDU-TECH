using System.Security.Cryptography;
using System.Text;
using EduFlyUp.Services;

namespace EduFlyUp.Tests;

public sealed class CassoWebhookVerifierTests
{
    private const string Key = "fixture-only-signing-key";
    private const string Timestamp = "1727948258788";
    private const string Sorted = """{"data":{"accountNumber":"00001234","amount":160000,"bankAbbreviation":"VPB","description":"Đóng học phí","id":1234,"transactionDateTime":"2024-10-03 15:06:37"},"error":0}""";

    [Fact]
    public void Accepts_published_casso_v2_signature()
    {
        // Dữ liệu công khai trong dotnet.cs của CassoHQ/casso-webhook-v2-verify-signature.
        const string example = """
            {"error":0,"data":{"id":0,"reference":"MA_GIAO_DICH_THU_NGHIEM","description":"giao dich thu nghiem",
            "amount":599000,"runningBalance":25000000,"transactionDateTime":"2024-10-03 15:06:37",
            "accountNumber":"88888888","bankName":"VPBank","bankAbbreviation":"VPB","virtualAccountNumber":"",
            "virtualAccountName":"","counterAccountName":"NGUYEN VAN A","counterAccountNumber":"8888888888",
            "counterAccountBankId":"970415","counterAccountBankName":"VietinBank"}}
            """;
        const string publicExampleKey = "mgExMrjj4i7a1p8Cs6LmFbpCd8lbw4sSX4LZXPag7TKazeIogtR90tvWYTkwQbvs";
        const string header = "t=1727948258788,v1=ed0a4bd2e826d5cb69988cdb141e6c1a080e21f3b57eb72cd78192220042b9e7dde0868fc667faea8e224900fa7904e7c88dfa098032fb2d6b6996856e8b7ff3";
        var transaction = Assert.IsType<CassoTransaction>(Verify(example, header, publicExampleKey));
        Assert.Equal(599000, transaction.Amount);
    }

    [Fact]
    public void Sorts_the_envelope_preserves_unicode_account_and_normalizes_time_offset()
    {
        const string incoming = """
            { "error":0, "data":{"transactionDateTime":"2024-10-03 15:06:37","description":"\u0110óng học phí",
              "id":1234,"accountNumber":"00001234","bankAbbreviation":"VPB","amount":160000} }
            """;
        var transaction = Assert.IsType<CassoTransaction>(Verify(incoming, Sign(Sorted)));
        Assert.Equal(1234, transaction.Id);
        Assert.Equal(160000, transaction.Amount);
        Assert.Equal("00001234", transaction.AccountNumber);
        Assert.Equal("VPB", transaction.BankAbbreviation);
        Assert.Equal("Đóng học phí", transaction.Description);
        Assert.Equal(new DateTime(2024, 10, 3, 8, 6, 37, DateTimeKind.Utc), transaction.OccurredAt?.UtcDateTime);
    }

    [Fact]
    public void Rejects_bad_signatures_tampering_and_ambiguous_json()
    {
        var header = Sign(Sorted);
        Assert.Null(Verify(Sorted.Replace("160000", "1"), header));
        Assert.Null(Verify(Sorted, header.Replace(Timestamp, "1727948258789")));
        Assert.Null(Verify(Sorted, header, "different-signing-key"));
        Assert.Null(Verify(Sorted, header, ""));
        Assert.Null(Verify(Sorted, null));
        Assert.Null(Verify(Sorted, header + ",v1=00"));
        Assert.Null(Verify(Sorted, header[..^1] + "z"));
        Assert.Null(Verify("{", header));
        Assert.Null(Verify(new string(' ', CassoWebhookVerifier.MaxBodyBytes + 1), header));
        var duplicate = Sorted.Replace("\"amount\":160000", "\"amount\":160000,\"amount\":160000");
        Assert.Null(Verify(duplicate, Sign(duplicate)));
    }

    [Theory]
    [InlineData("\"error\":0", "\"error\":1")]
    [InlineData("\"amount\":160000", "\"amount\":160000.5")]
    [InlineData("\"amount\":160000", "\"amount\":10000000000000")]
    [InlineData("\"amount\":160000", "\"amount\":0")]
    [InlineData("\"id\":1234", "\"id\":\"1234\"")]
    [InlineData("\"accountNumber\":\"00001234\"", "\"accountNumber\":\"\"")]
    [InlineData("2024-10-03 15:06:37", "invalid time")]
    public void Rejects_invalid_fields_even_if_signed(string oldValue, string newValue)
    {
        var invalid = Sorted.Replace(oldValue, newValue);
        Assert.Null(Verify(invalid, Sign(invalid)));
    }

    [Fact]
    public void Date_only_and_outgoing_events_do_not_gain_a_payment_timestamp_or_positive_amount()
    {
        var outgoing = Sorted.Replace("160000", "-160000").Replace("2024-10-03 15:06:37", "2024-10-03");
        var transaction = Assert.IsType<CassoTransaction>(Verify(outgoing, Sign(outgoing)));
        Assert.Equal(-160000, transaction.Amount);
        Assert.Null(transaction.OccurredAt);
    }

    private static CassoTransaction? Verify(string json, string? signature, string key = Key)
        => CassoWebhookVerifier.Verify(Encoding.UTF8.GetBytes(json), signature, key);

    private static string Sign(string sorted)
        => "t=" + Timestamp + ",v1=" + Convert.ToHexString(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(Timestamp + "." + sorted)));
}
