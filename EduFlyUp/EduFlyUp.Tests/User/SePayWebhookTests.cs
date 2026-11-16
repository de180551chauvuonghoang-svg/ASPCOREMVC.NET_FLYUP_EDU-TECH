using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduFlyUp.Tests;

public sealed class SePayWebhookTests
{
    [Theory]
    [InlineData("demo-key", null, "in", 1)]
    [InlineData("demo-key", "Apikey wrong-key", "in", 1)]
    [InlineData("demo-key", "Bearer demo-key", "in", 1)]
    [InlineData(null, "Apikey demo-key", "in", 1)]
    [InlineData("demo-key", "Apikey demo-key", "out", 1)]
    [InlineData("demo-key", "Apikey demo-key", "in", 0)]
    public async Task Rejects_invalid_webhooks_before_reading_orders(string? key, string? header, string type, long id)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["SePay:ApiKey"] = key }).Build();
        var service = new PaymentService(null!, null!, null!, configuration, NullLogger<PaymentService>.Instance);
        var payload = new SePayWebhookPayload { id = id, transferAmount = 200000, transferType = type, content = "EDUPAY123456" };

        Assert.False(await service.ProcessSePayWebhookAsync(header, payload));
    }
}
