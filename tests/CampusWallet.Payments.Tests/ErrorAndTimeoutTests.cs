using System.Diagnostics;
using CampusWallet.Payments;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CampusWallet.Payments.Tests;

[TestFixture]
public class ErrorAndTimeoutTests
{
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(1);

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
            Timeout = ClientTimeout
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

    // ---------- ERRORS ----------

    [Test]
    public async Task TopUp_WhenGatewayReturns402_ReturnsDeclined()
    {
        // Arrange: gateway pura-pura menolak karena saldo kartu kurang
        _server
            .Given(Request.Create()
                .WithPath("/payments")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(402)
                .WithBodyAsJson(new { error = "INSUFFICIENT_FUNDS" }));

        // Act
        var result = await _client.TopUpAsync("ORDER-402", 50_000m);

        // Assert: ditolak, dan tidak ada transaksi yang tercatat
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Declined));
        Assert.That(result.TransactionId, Is.Null);
    }


    [TestCase(500)]
    [TestCase(502)]
    [TestCase(503)]
    public async Task TopUp_WhenGatewayReturns5xx_ReturnsGatewayError(int statusCode)
    {
        // Arrange: gateway pura-pura sedang bermasalah
        _server
            .Given(Request.Create()
                .WithPath("/payments")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode));

        // Act
        var result = await _client.TopUpAsync("ORDER-5XX", 50_000m);

        // Assert
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.GatewayError));
        Assert.That(result.TransactionId, Is.Null);
    }
}