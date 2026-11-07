using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CampusWallet.Payments;

/// <summary>
/// Klien untuk memanggil API payment gateway menggunakan HttpClient.
/// </summary>
public sealed class PaymentClient
{
    public const decimal MinimumAmount = 10_000m;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public PaymentClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    public async Task<PaymentResult> TopUpAsync(
        string orderId, decimal amount, CancellationToken cancellationToken = default)
    {
        // 1. Validasi: nominal tidak valid -> gateway TIDAK dipanggil sama sekali.
        if (amount < MinimumAmount)
        {
            return new PaymentResult(PaymentStatus.InvalidAmount);
        }

        // 2. Siapkan request: POST /payments + header Authorization + body JSON.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/payments")
        {
            Content = JsonContent.Create(new PaymentRequest(orderId, amount, "IDR"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            // 3. Kirim request dan terjemahkan status code menjadi PaymentStatus.
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var body = await response.Content
                        .ReadFromJsonAsync<PaymentResponse>(cancellationToken);

                    return string.IsNullOrWhiteSpace(body?.TransactionId)
                        ? new PaymentResult(PaymentStatus.GatewayError)
                        : new PaymentResult(PaymentStatus.Success, body.TransactionId);

                case HttpStatusCode.Unauthorized:     // 401
                    return new PaymentResult(PaymentStatus.Unauthorized);

                case HttpStatusCode.PaymentRequired:  // 402
                    return new PaymentResult(PaymentStatus.Declined);

                default:                              // 5xx dan lainnya
                    return new PaymentResult(PaymentStatus.GatewayError);
            }
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 4. HttpClient.Timeout habis: gateway terlalu lama menjawab.
            return new PaymentResult(PaymentStatus.Timeout);
        }
        catch (HttpRequestException)
        {
            // Gateway tidak bisa dihubungi sama sekali (server mati / koneksi putus).
            return new PaymentResult(PaymentStatus.GatewayError);
        }
        catch (JsonException)
        {
            // Gateway membalas 200 tapi isinya bukan JSON yang benar.
            return new PaymentResult(PaymentStatus.GatewayError);
        }
    }
}