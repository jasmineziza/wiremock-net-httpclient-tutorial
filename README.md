# wiremock-net-httpclient-tutorial
Tutorial API &amp; Service Integration Testing dengan WireMock.Net + HttpClient (C#/.NET)


##


##


##


## Pengujian Error & Timeout

### Tujuan
Gateway pembayaran asli tidak selalu menjawab "sukses". Dengan WireMock.Net kita
bisa memaksa gateway palsu mengembalikan error atau menjawab sangat lambat,
tanpa menyentuh gateway sungguhan.

### Skenario yang diuji
| Skenario | Respons gateway palsu | Hasil yang diharapkan |
|---|---|---|
| Saldo kurang | HTTP 402 | `PaymentStatus.Declined` |
| Server bermasalah | HTTP 500 | `PaymentStatus.GatewayError` |
| Gateway lambat | Jawaban tertunda 5 detik | `PaymentStatus.Timeout` |

### Langkah implementasi
1. Buat file `tests/CampusWallet.Payments.Tests/ErrorAndTimeoutTests.cs`.
2. Pada `SetUp`, nyalakan `WireMockServer` dan buat `HttpClient` dengan
   `Timeout` pendek (1 detik) agar test timeout cepat.
3. Untuk error, atur stub dengan `.WithStatusCode(402)` atau `.WithStatusCode(500)`.
4. Untuk timeout, tambahkan `.WithDelay(TimeSpan.FromSeconds(5))` pada respons.

Potongan kode kunci (simulasi gateway lambat):

```csharp
_server
    .Given(Request.Create().WithPath("/payments").UsingPost())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBodyAsJson(new { transactionId = "TRX-SLOW", status = "SUCCESS" })
        .WithDelay(TimeSpan.FromSeconds(5)));
```

### Cara menjalankan
```bash
dotnet test --filter "FullyQualifiedName~ErrorAndTimeoutTests"
```

### Hasil yang diharapkan
Ketiga test lulus (402, 500, dan timeout).

### Analisis kegagalan dan perbaikan
Coba comment-kan blok `catch (TaskCanceledException)` di `PaymentClient.cs`,
lalu jalankan ulang test.

- **Gejala:** test timeout gagal dengan `TaskCanceledException`.
- **Penyebab:** `HttpClient` melempar exception saat waktu tunggu habis, dan
  tidak ada kode yang menerjemahkannya menjadi status yang dipahami aplikasi.
- **Perbaikan:** kembalikan blok `catch` agar exception diubah menjadi
  `PaymentStatus.Timeout`.
