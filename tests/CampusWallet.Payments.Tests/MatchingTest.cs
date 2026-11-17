using CampusWallet.Payments;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace CampusWallet.Payments.Tests;

[TestFixture]
public class MatchingTest
{
    private const string ApiKey = "test-api-key";

    private WireMockServer _server = null!;
    private HttpClient _httpClient = null!;
    private PaymentClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _server = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _client = new PaymentClient(_httpClient, ApiKey);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Test]
    public async Task TopUp_SendsBearerApiKeyHeader_StubWithHeaderMatches()
    {
        _server
            .Given(Request.Create()
                .WithPath("/payments")
                .UsingPost()
                .WithHeader("Authorization", $"Bearer {ApiKey}"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TRX-HDR", status = "SUCCESS" }));

        var result = await _client.TopUpAsync("ORDER-HDR", 50_000m);

        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Success));
        Assert.That(result.TransactionId, Is.EqualTo("TRX-HDR"));
    }

    [Test]
    public async Task TopUp_WithWrongApiKey_ReturnsUnauthorized()
    {
        _server
            .Given(Request.Create()
                .WithPath("/payments")
                .UsingPost()
                .WithHeader("Authorization", $"Bearer {ApiKey}"))
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TRX-OK", status = "SUCCESS" }));

        _server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .AtPriority(10)
            .RespondWith(Response.Create().WithStatusCode(401));

        using var wrongKeyHttp = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var wrongKeyClient = new PaymentClient(wrongKeyHttp, "kunci-salah");

        var result = await wrongKeyClient.TopUpAsync("ORDER-KEY", 50_000m);

        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Unauthorized));
        Assert.That(result.TransactionId, Is.Null);
    }

    [TestCase("ORDER-A", 10_000)]
    [TestCase("ORDER-B", 75_000)]
    [TestCase("ORDER-C", 250_000)]
    public async Task TopUp_BodyMatchesJson_ReturnsSuccess(string orderId, int amount)
    {
        _server
            .Given(Request.Create()
                .WithPath("/payments")
                .UsingPost()
                .WithBody(new JsonMatcher(new { orderId, amount, currency = "IDR" })))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TRX-" + orderId, status = "SUCCESS" }));

        var result = await _client.TopUpAsync(orderId, amount);

        Assert.That(result.Status, Is.EqualTo(PaymentStatus.Success));
        Assert.That(result.TransactionId, Is.EqualTo("TRX-" + orderId));
    }

    [Test]
    public async Task TopUp_WhenBodyDoesNotMatchStub_ReturnsGatewayError()
    {
        _server
            .Given(Request.Create()
                .WithPath("/payments")
                .UsingPost()
                .WithBody(new JsonMatcher(new { orderId = "ORDER-X", amount = 50_000, currency = "USD" })))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TRX-X", status = "SUCCESS" }));

        var result = await _client.TopUpAsync("ORDER-X", 50_000m);

        Assert.That(result.Status, Is.EqualTo(PaymentStatus.GatewayError));
        Assert.That(result.TransactionId, Is.Null);
    }

    [Test]
    public async Task TopUp_WhenStubOnlyAcceptsGet_PostIsNotMatched()
    {
        _server
            .Given(Request.Create().WithPath("/payments").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        var result = await _client.TopUpAsync("ORDER-METHOD", 50_000m);

        Assert.That(result.Status, Is.EqualTo(PaymentStatus.GatewayError));
    }

    [Test]
    public async Task TopUp_WhenStubPathIsDifferent_IsNotMatched()
    {
        _server
            .Given(Request.Create().WithPath("/transactions").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var result = await _client.TopUpAsync("ORDER-PATH", 50_000m);

        Assert.That(result.Status, Is.EqualTo(PaymentStatus.GatewayError));
    }

    [Test]
    public async Task TopUp_WithValidAmount_SendsExactlyOneRequestToPaymentsEndpoint()
    {
        _server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { transactionId = "TRX-LOG", status = "SUCCESS" }));

        await _client.TopUpAsync("ORDER-LOG", 50_000m);

        var logs = _server.LogEntries.ToList();
        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs[0].RequestMessage!.Path, Is.EqualTo("/payments"));
        Assert.That(logs[0].RequestMessage!.Method, Is.EqualTo("POST").IgnoreCase);
    }

    [Test]
    public async Task TopUp_WhenAmountBelowMinimum_DoesNotCallGateway()
    {
        _server
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var result = await _client.TopUpAsync("ORDER-SMALL", 5_000m);

        Assert.That(result.Status, Is.EqualTo(PaymentStatus.InvalidAmount));
        Assert.That(_server.LogEntries, Is.Empty);
    }
}