using System.Text.Json;
using ArtMarketplace.Domain.Modules.Payments;
using ArtMarketplace.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace ArtMarketplace.IntegrationTests;

public sealed class FakePaymentProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private const string Secret = "unit-test-secret-with-at-least-32-characters";

    [Fact]
    public async Task ValidSignatureAndFreshTimestampProduceVerifiedCallback()
    {
        var provider = Provider(); var body = Body(); var timestamp = Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var result = await provider.VerifyCallbackAsync(body, FakePaymentProvider.Sign(timestamp, body, Secret), timestamp, Now, CancellationToken.None);
        Assert.True(result.IsValid); Assert.Equal(PaymentOutcome.Succeeded, result.Callback!.Outcome); Assert.Equal("event-1", result.Callback.EventId);
    }

    [Fact]
    public async Task InvalidSignatureFailsClosed()
    {
        var result = await Provider().VerifyCallbackAsync(Body(), new string('0', 64), Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), Now, CancellationToken.None);
        Assert.False(result.IsValid); Assert.Null(result.Callback);
    }

    [Fact]
    public async Task StaleCallbackFailsClosed()
    {
        var body = Body(); var timestamp = Now.AddMinutes(-10).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var result = await Provider().VerifyCallbackAsync(body, FakePaymentProvider.Sign(timestamp, body, Secret), timestamp, Now, CancellationToken.None);
        Assert.False(result.IsValid); Assert.Equal("Stale callback.", result.Error);
    }

    private static FakePaymentProvider Provider() => new(Options.Create(new PaymentProviderOptions { CallbackSecret = Secret, CallbackFreshnessSeconds = 300 }));
    private static string Body() => JsonSerializer.Serialize(new FakePaymentProvider.FakeCallbackPayload("event-1", "txn-1", "merchant-ref", "merchant", 10, "IRR", "Succeeded", Now));
}
