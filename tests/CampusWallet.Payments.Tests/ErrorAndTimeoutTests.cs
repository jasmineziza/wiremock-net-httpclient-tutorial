using System.Diagnostics;
using CampusWallet.Payments;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CampusWallet.Payments.Tests;

[TestFixture]
public class ErrorAndTimeoutTests
{
    private WireMockServer _server = null!;
    private HttpClient _httpClient = null!;
    private PaymentClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _server = WireMockServer.Start();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_server.Url!),
            Timeout = TimeSpan.FromSeconds(1) // pendek, supaya test timeout cepat
        };
        _client = new PaymentClient(_httpClient, "test-api-key");
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Test]
    public async Task TopUp_WhenGatewayReturns402_ReturnsDeclined()
    {
        // Arrange: gateway pura-pura menolak karena saldo kurang
        _server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(402));

        // Act
        var result = await _client.TopUpAsync("ORDER-402", 50_000m);

        // Assert
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Declined));
        Assert.That(result.TransactionId, Is.Null);
    }
}