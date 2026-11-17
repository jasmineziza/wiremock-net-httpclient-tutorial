# wiremock-net-httpclient-tutorial

Tutorial API & Service Integration Testing dengan **WireMock.Net + HttpClient** (C#/.NET)

> Studi kasus: **Payment Gateway untuk top-up saldo e-wallet kampus (CampusWallet)**

## Anggota Kelompok

| NRP | Nama | Bagian |
|---|---|---|
| 5053231003 | Jasmine Aziza | Kerangka project, `PaymentClient`, HTTP Stubbing |
| 5053241043 | Muhammad Alfaraldi Raihan | Request Matching |
| 5053241017 | Ledwino Galih Wandanu | Request Verification |
| 5053241023 | Mohammad Akbar | Error & Timeout |

## Daftar Isi

1. [Pengenalan](#pengenalan)
2. [Persiapan Lingkungan](#persiapan-lingkungan)
3. [Instalasi](#instalasi)
4. [Struktur Project](#struktur-project)
5. [Aplikasi yang Diuji: PaymentClient](#aplikasi-yang-diuji-paymentclient)
6. [Pengujian HTTP Stubbing](#pengujian-http-stubbing)
7. [Pengujian Request Matching](#pengujian-request-matching)
8. [Pengujian Request Verification](#pengujian-request-verification)
9. [Pengujian Error & Timeout](#pengujian-error--timeout)
10. [Cara Menjalankan Semua Test](#cara-menjalankan-semua-test)
11. [Hasil yang Diharapkan](#hasil-yang-diharapkan)

---

## Pengenalan

### Masalahnya

Aplikasi modern jarang berdiri sendiri. Aplikasi CampusWallet misalnya, harus
memanggil **payment gateway** (layanan pihak ketiga) setiap kali mahasiswa
melakukan top-up saldo. Kalau kita menguji aplikasi langsung ke gateway asli,
muncul banyak masalah:

- Setiap test bisa **memotong uang sungguhan** atau butuh akun sandbox.
- Gateway asli bisa **lambat, mati, atau dibatasi** (rate limit), sehingga test jadi tidak stabil.
- Kondisi seperti "saldo kurang", "server error 500", atau "gateway lambat"
  **sulit dibuat** dengan sengaja di gateway asli.

### Solusinya: WireMock.Net

[WireMock.Net](https://github.com/wiremock/WireMock.Net) adalah versi .NET dari
[WireMock](https://wiremock.org). Library ini menyalakan **server HTTP palsu**
di dalam test. Kita bisa menulis "naskah" untuk server palsu ini, misalnya
*"kalau ada POST ke /payments, jawab 200 dengan transactionId TRX-001"*.

Analogi sederhananya: WireMock itu seperti **aktor pengganti** (stunt double)
untuk payment gateway. Aplikasi kita tetap memakai `HttpClient` sungguhan dan
mengirim request HTTP sungguhan, tetapi yang menjawab adalah aktor pengganti
yang perilakunya kita atur sendiri.

### Konsep yang dibahas

| Konsep | Arti singkat |
|---|---|
| **HTTP Stubbing** | Mengatur jawaban server palsu untuk request tertentu |
| **Request Matching** | Mengatur syarat request (path, method, header, body) agar stub aktif |
| **Request Verification** | Memeriksa request apa saja yang benar-benar diterima server palsu |
| **Errors** | Mensimulasikan respons gagal seperti 401, 402, dan 500 |
| **Timeout** | Mensimulasikan gateway yang terlalu lama menjawab |

### Kenapa ini disebut integration testing?

Unit test biasa mengganti `HttpClient` dengan objek palsu (mock), sehingga
proses HTTP yang sebenarnya tidak pernah terjadi. Dengan WireMock.Net,
**seluruh jalur HTTP benar-benar dijalankan**: serialisasi JSON, header,
status code, sampai jaringan lokal. Yang dipalsukan hanya **layanan di ujung
sana**. Karena itu pengujian ini termasuk *API & Service Integration Testing*.

---

## Persiapan Lingkungan

| Kebutuhan | Versi yang dipakai | Keterangan |
|---|---|---|
| .NET SDK | 10.0 atau lebih baru | Project menargetkan `net10.0`. Unduh di [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Git | versi terbaru | Untuk clone repository |
| IDE (pilih salah satu) | Visual Studio 2026, VS Code + C# Dev Kit, atau JetBrains Rider | Opsional, semua bisa juga lewat terminal |

Cek apakah .NET SDK dan Git sudah terpasang:

```bash
dotnet --version
git --version
```

Kalau `dotnet --version` menampilkan angka `10.x` atau lebih tinggi, lingkungan
sudah siap.

Library yang dipakai (otomatis terunduh saat restore, tidak perlu dipasang manual):

| Package | Versi | Fungsi |
|---|---|---|
| `WireMock.Net` | 2.21.0 | Server HTTP palsu |
| `NUnit` | 4.3.2 | Framework testing |
| `NUnit3TestAdapter` | 5.0.0 | Supaya test NUnit bisa dijalankan `dotnet test` |
| `Microsoft.NET.Test.Sdk` | 17.14.0 | Infrastruktur test .NET |

---

## Instalasi

### Opsi A: Memakai repository ini (disarankan)

```bash
git clone https://github.com/jasmineziza/wiremock-net-httpclient-tutorial.git
cd wiremock-net-httpclient-tutorial
dotnet restore
dotnet build
```

Kalau build berhasil, langsung lanjut ke [Cara Menjalankan Semua Test](#cara-menjalankan-semua-test).

### Opsi B: Membuat project dari nol

Langkah ini untuk yang ingin membangun ulang struktur project sendiri:

```bash
# 1. Buat solution
mkdir wiremock-net-httpclient-tutorial
cd wiremock-net-httpclient-tutorial
dotnet new sln -n CampusWallet.Payments

# 2. Project aplikasi (class library)
dotnet new classlib -n CampusWallet.Payments -o src/CampusWallet.Payments -f net10.0

# 3. Project test (NUnit)
dotnet new nunit -n CampusWallet.Payments.Tests -o tests/CampusWallet.Payments.Tests -f net10.0

# 4. Daftarkan kedua project ke solution
dotnet sln add src/CampusWallet.Payments tests/CampusWallet.Payments.Tests

# 5. Project test perlu mengenal project aplikasi
dotnet add tests/CampusWallet.Payments.Tests reference src/CampusWallet.Payments

# 6. Pasang WireMock.Net di project test
dotnet add tests/CampusWallet.Payments.Tests package WireMock.Net --version 2.21.0
```

Setelah itu, hapus file contoh `UnitTest1.cs` dan `Class1.cs`, lalu salin kode
dari folder `src` dan `tests` di repository ini.

---

## Struktur Project

```
wiremock-net-httpclient-tutorial/
├── CampusWallet.Payments.slnx
├── src/
│   └── CampusWallet.Payments/          # aplikasi yang diuji
│       ├── PaymentModels.cs            # enum status + record request/response
│       └── PaymentClient.cs            # pemanggil payment gateway via HttpClient
└── tests/
    └── CampusWallet.Payments.Tests/    # test dengan WireMock.Net + NUnit
        ├── StubbingTests.cs            # HTTP stubbing
        ├── MatchingTest.cs             # request matching
        ├── VerificationTests.cs        # request verification
        └── ErrorAndTimeoutTests.cs     # errors & timeout
```

---

## Aplikasi yang Diuji: PaymentClient

`PaymentClient` adalah kelas yang memanggil payment gateway saat mahasiswa
melakukan top-up. Alurnya:

1. **Validasi nominal.** Kalau `amount` kurang dari `MinimumAmount` (Rp10.000),
   langsung kembalikan `InvalidAmount` **tanpa** memanggil gateway.
2. **Kirim request** `POST /payments` dengan:
   - header `Authorization: Bearer <apiKey>`
   - body JSON `{ "orderId": "...", "amount": 50000, "currency": "IDR" }`
3. **Terjemahkan jawaban gateway** menjadi `PaymentStatus`:

| Jawaban gateway | `PaymentStatus` |
|---|---|
| 200 + `transactionId` terisi | `Success` |
| 200 tetapi `transactionId` kosong / JSON rusak | `GatewayError` |
| 401 | `Unauthorized` |
| 402 | `Declined` |
| Status lain (404, 5xx, ...) | `GatewayError` |
| Tidak menjawab sampai `HttpClient.Timeout` habis | `Timeout` |
| Server tidak bisa dihubungi | `GatewayError` |

Karena `PaymentClient` menerima `HttpClient` lewat constructor, di dalam test
kita cukup mengarahkan `HttpClient.BaseAddress` ke alamat server WireMock.

---

## Pengujian HTTP Stubbing

### Tujuan
Membuktikan bahwa `PaymentClient` bisa membaca jawaban sukses dari gateway
dengan benar, memakai server palsu WireMock.Net sebagai pengganti gateway asli.

### Skenario yang diuji
| Skenario | Respons gateway palsu | Hasil yang diharapkan |
|---|---|---|
| Top-up berhasil | HTTP 200, body `{ "transactionId": "TRX-001", "status": "SUCCESS" }` | `PaymentStatus.Success` dengan `TransactionId = "TRX-001"` |

### Langkah implementasi
1. Buat file `tests/CampusWallet.Payments.Tests/StubbingTests.cs`.
2. Pada `[SetUp]`, nyalakan server palsu dengan `WireMockServer.Start()`.
   Tanpa argumen, server memakai **port acak** sehingga tidak bentrok antar test.
3. Buat `HttpClient` dengan `BaseAddress = new Uri(_server.Url!)`, lalu berikan
   ke `PaymentClient`.
4. Pada `[TearDown]`, matikan dan buang server (`Stop()` + `Dispose()`) agar
   port dilepas.
5. Di dalam test, tulis "naskah" stub dengan pola **Given → RespondWith**:
   - `Given(...)`: request seperti apa yang ditunggu
   - `RespondWith(...)`: jawaban yang dikirim balik

Potongan kode kunci:

```csharp
[SetUp]
public void SetUp()
{
    // Nyalakan server palsu di port acak, lalu arahkan HttpClient ke sana.
    _server = WireMockServer.Start();
    _httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
    _client = new PaymentClient(_httpClient, "test-api-key");
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
```

### Cara menjalankan
```bash
dotnet test --filter "FullyQualifiedName~StubbingTests"
```

### Hasil yang diharapkan
1 test lulus.

### Analisis kegagalan dan perbaikan
Coba ubah path stub dari `"/payments"` menjadi `"/payment"` (tanpa huruf **s**),
lalu jalankan ulang test.

- **Gejala:** test gagal dengan pesan kira-kira
  `Expected: Success But was: GatewayError`.
- **Penyebab:** aplikasi mengirim request ke `/payments`, tetapi stub menunggu
  di `/payment`. Karena tidak ada stub yang cocok, WireMock membalas
  **404 Not Found**, dan `PaymentClient` menerjemahkannya menjadi `GatewayError`.
- **Perbaikan:** samakan path stub dengan path yang dipakai aplikasi
  (`"/payments"`). Pelajaran: stub hanya menjawab request yang **persis**
  sesuai naskahnya, jadi typo kecil pun langsung terdeteksi.

---

## Pengujian Request Matching

### Tujuan
Server palsu WireMock.Net tidak hanya bisa menjawab request, tetapi juga bisa
**memilih** request mana yang dijawab. Stub hanya aktif kalau request dari
`PaymentClient` cocok dengan syarat yang ditentukan (path, method, header, dan
body). Dengan begitu kita bisa membuktikan bahwa aplikasi benar-benar mengirim
request yang tepat ke gateway, bukan sekadar menerima jawabannya.

### Skenario yang diuji
| Skenario | Syarat pada stub | Hasil yang diharapkan |
|---|---|---|
| Header Authorization benar | `Authorization: Bearer test-api-key` | `PaymentStatus.Success` |
| API key salah | Stub utama butuh key benar, stub cadangan membalas 401 | `PaymentStatus.Unauthorized` |
| Body JSON cocok (3 data uji) | `orderId`, `amount`, `currency: "IDR"` sama persis | `PaymentStatus.Success` |
| Body JSON tidak cocok | Stub mengharapkan `currency: "USD"` | `PaymentStatus.GatewayError` (404) |
| Method tidak cocok | Stub hanya untuk `GET`, client memakai `POST` | `PaymentStatus.GatewayError` (404) |
| Path tidak cocok | Stub dipasang di `/transactions` | `PaymentStatus.GatewayError` (404) |
| Request tercatat | Periksa `LogEntries` server palsu | Tepat 1 request `POST /payments` |
| Nominal di bawah minimum | Stub tersedia, tetapi tidak boleh dipanggil | `PaymentStatus.InvalidAmount`, `LogEntries` kosong |

### Langkah implementasi
1. Buat file `tests/CampusWallet.Payments.Tests/MatchingTest.cs`.
2. Pada `SetUp`, nyalakan `WireMockServer` dan buat `HttpClient` yang
   mengarah ke `_server.Url`.
3. Untuk mencocokkan header, tambahkan `.WithHeader("Authorization", "Bearer test-api-key")`
   pada `Request.Create()`.
4. Untuk mencocokkan body JSON, gunakan `.WithBody(new JsonMatcher(new { ... }))`.
   Nama properti memakai camelCase karena `JsonContent.Create` menulis JSON
   dengan format tersebut.
5. Untuk dua stub yang saling melengkapi (key benar dan key salah), atur urutan
   dengan `.AtPriority(...)`. Angka lebih kecil dicek lebih dulu.
6. Untuk memastikan request benar-benar terkirim atau tidak, periksa
   `_server.LogEntries`.

Potongan kode kunci (stub yang hanya cocok untuk header dan body tertentu):

```csharp
_server
    .Given(Request.Create()
        .WithPath("/payments")
        .UsingPost()
        .WithHeader("Authorization", "Bearer test-api-key")
        .WithBody(new JsonMatcher(new { orderId, amount, currency = "IDR" })))
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBodyAsJson(new { transactionId = "TRX-" + orderId, status = "SUCCESS" }));
```

### Cara menjalankan
```bash
dotnet test --filter "FullyQualifiedName~MatchingTest"
```

### Hasil yang diharapkan
Seluruh test lulus (10 dari 10 test case: 8 method, dengan satu method
berjalan 3 kali untuk data uji berbeda).

### Analisis kegagalan dan perbaikan
Coba comment-kan baris berikut di `PaymentClient.cs`, lalu jalankan ulang test:

```csharp
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
```

- **Gejala:** test `TopUp_SendsBearerApiKeyHeader_StubWithHeaderMatches` gagal,
  dengan pesan kira-kira `Expected: Success But was: GatewayError`.
- **Penyebab:** request dikirim tanpa header `Authorization`, sehingga tidak
  cocok dengan stub mana pun. WireMock membalas 404, dan `PaymentClient`
  menerjemahkannya menjadi `GatewayError`.
- **Perbaikan:** kembalikan baris yang mengisi header `Authorization` agar
  request sesuai dengan syarat stub.

---

## Pengujian Request Verification

<!-- TODO (Ledwino): isi bagian ini dengan format yang sama:
     Tujuan, Skenario yang diuji, Langkah implementasi, Potongan kode kunci,
     Cara menjalankan, Hasil yang diharapkan, Analisis kegagalan dan perbaikan -->

*Bagian ini sedang disusun.*

### Cara menjalankan
```bash
dotnet test --filter "FullyQualifiedName~VerificationTests"
```

### Hasil yang diharapkan
Seluruh test lulus (6 test case: 4 method, dengan satu method berjalan 3 kali
untuk nominal tidak valid yang berbeda).

---

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

---

## Cara Menjalankan Semua Test

Dari folder utama repository:

```bash
dotnet test
```

Untuk menjalankan satu kelompok test saja, gunakan `--filter`:

```bash
dotnet test --filter "FullyQualifiedName~StubbingTests"
dotnet test --filter "FullyQualifiedName~MatchingTest"
dotnet test --filter "FullyQualifiedName~VerificationTests"
dotnet test --filter "FullyQualifiedName~ErrorAndTimeoutTests"
```

Di Visual Studio, test juga bisa dijalankan lewat menu **Test → Test Explorer
→ Run All Tests**.

---

## Hasil yang Diharapkan

```
Test summary: total: 20, failed: 0, succeeded: 20, skipped: 0
```

| Kelompok test | Jumlah test case |
|---|---|
| HTTP Stubbing | 1 |
| Request Matching | 10 |
| Request Verification | 6 |
| Error & Timeout | 3 |
| **Total** | **20** |

Waktu jalan sekitar 10 sampai 15 detik. Sebagian besar dipakai test timeout
yang memang sengaja menunggu `HttpClient.Timeout` (1 detik) habis.

---

## Referensi

- WireMock: https://wiremock.org
- WireMock.Net: https://github.com/wiremock/WireMock.Net
- NUnit: https://nunit.org
- `HttpClient` di .NET: https://learn.microsoft.com/dotnet/api/system.net.http.httpclient

## Lisensi

[MIT](LICENSE)
