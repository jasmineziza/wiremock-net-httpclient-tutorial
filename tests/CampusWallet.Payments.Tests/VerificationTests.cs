using System.Text.Json;
using CampusWallet.Payments;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CampusWallet.Payments.Tests;

[TestFixture]
public class VerificationTests
{
    private WireMockServer _server = null!;
    private HttpClient _httpClient = null!;
    private PaymentClient _client = null!;

    [SetUp]
    public void SetUp()
    {
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

    private void ArrangeSuccessStub()
    {
        _server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TRX-001", status = "SUCCESS" }));
    }

    [Test]
    public async Task TopUp_WhenAmountIsValid_SendsChargeExactlyOnce()
    {
        // Arrange
        ArrangeSuccessStub();

        // Act
        var result = await _client.TopUpAsync("ORDER-001", 50_000m);

        // Assert: aplikasi menganggap sukses DAN gateway menerima tepat 1 request charge
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Success));

        var calls = _server.FindLogEntries(
            Request.Create().WithPath("/payments").UsingPost());

        Assert.That(calls.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task TopUp_WhenAmountIsValid_SendsRequestWithCorrectContent()
    {
        // Arrange
        ArrangeSuccessStub();

        // Act
        await _client.TopUpAsync("ORDER-002", 75_000m);

        // Assert: ambil request yang diterima gateway, lalu periksa isinya
        var calls = _server.FindLogEntries(
            Request.Create().WithPath("/payments").UsingPost()).ToList();

        Assert.That(calls, Has.Count.EqualTo(1));

        var received = calls[0].RequestMessage;
        Assert.That(received.Method, Is.EqualTo("POST"));
        Assert.That(received.Path, Is.EqualTo("/payments"));

        using var json = JsonDocument.Parse(received.BodyData!.BodyAsString!);
        var root = json.RootElement;

        Assert.That(root.GetProperty("orderId").GetString(), Is.EqualTo("ORDER-002"));
        Assert.That(root.GetProperty("amount").GetDecimal(), Is.EqualTo(75_000m));
        Assert.That(root.GetProperty("currency").GetString(), Is.EqualTo("IDR"));
    }

    [TestCase(9_999)]
    [TestCase(0)]
    [TestCase(-5_000)]
    public async Task TopUp_WhenAmountIsInvalid_DoesNotSendAnyRequest(int invalidAmount)
    {
        // Arrange: gateway siap menjawab, tapi harusnya ngga pernah dihubungi
        ArrangeSuccessStub();

        // Act
        var result = await _client.TopUpAsync("ORDER-INVALID", invalidAmount);

        // Assert: aplikasi menolak nominal, dan gateway menerima 0 request
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.InvalidAmount));
        Assert.That(result.TransactionId, Is.Null);

        Assert.That(_server.LogEntries.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task TopUp_WhenAmountIsExactlyMinimum_SendsChargeExactlyOnce()
    {
        // Arrange: nominal tepat di batas minimum masih dianggap valid
        ArrangeSuccessStub();

        // Act
        var result = await _client.TopUpAsync("ORDER-MIN", PaymentClient.MinimumAmount);

        // Assert
        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Success));
        Assert.That(_server.LogEntries.Count(), Is.EqualTo(1));
    }
}