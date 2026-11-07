using CampusWallet.Payments;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CampusWallet.Payments.Tests;

[TestFixture]
public class StubbingTests
{
    private WireMockServer _server = null!;
    private HttpClient _httpClient = null!;
    private PaymentClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        // Nyalakan server palsu di port acak, lalu arahkan HttpClient ke sana.
        _server = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
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
    public async Task TopUp_WhenGatewayReturns200_ReturnsSuccessWithTransactionId()
    {
        // Arrange: "naskah" untuk server palsu
        _server
            .Given(Request.Create()
                .WithPath("/payments")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TRX-001", status = "SUCCESS" }));

        // Act: aplikasi memanggil "gateway" lewat HttpClient sungguhan
        var result = await _client.TopUpAsync("ORDER-001", 50_000m);

        // Assert: aplikasi membaca jawaban gateway dengan benar
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Success));
        Assert.That(result.TransactionId, Is.EqualTo("TRX-001"));
    }
}