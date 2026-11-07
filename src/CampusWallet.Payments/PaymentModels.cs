namespace CampusWallet.Payments;

/// <summary>
/// Kesimpulan akhir proses top-up yang dilihat oleh aplikasi.
/// </summary>
public enum PaymentStatus
{
    Success,        // 200: pembayaran berhasil
    InvalidAmount,  // nominal tidak valid, request TIDAK dikirim ke gateway
    Unauthorized,   // 401: API key ditolak gateway
    Declined,       // 402: pembayaran ditolak (misalnya saldo kartu kurang)
    GatewayError,   // 5xx atau respons lain yang tidak terduga
    Timeout         // gateway terlalu lama menjawab
}

/// <summary>
/// "Formulir" yang dikirim aplikasi ke payment gateway (body JSON).
/// </summary>
public sealed record PaymentRequest(string OrderId, decimal Amount, string Currency);

/// <summary>
/// "Struk" balasan dari payment gateway saat pembayaran berhasil.
/// </summary>
public sealed record PaymentResponse(string TransactionId, string Status);

/// <summary>
/// Hasil akhir yang dikembalikan PaymentClient ke pemanggilnya.
/// </summary>
public sealed record PaymentResult(PaymentStatus Status, string? TransactionId = null);