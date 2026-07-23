using System.ComponentModel.DataAnnotations;

namespace ApiPollingWorker.Configuration;

public sealed class SignalROptions
{
    // Bu sınıf appsettings.json içindeki "SignalR" bölümünü temsil eder.
    public const string SectionName = "SignalR";

    // Bağlanılacak Hub'ın tam adresi.
    [Required, Url]
    public string HubUrl { get; init; } = string.Empty;

    // Hub üzerinde çağrılacak sunucu metodunun adı.
    [Required]
    public string MethodName { get; init; } = string.Empty;

    // Hub yetkilendirme istiyorsa bağlantıda gönderilecek token.
    public string AccessToken { get; init; } = string.Empty;
}
