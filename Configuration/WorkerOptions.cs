using System.ComponentModel.DataAnnotations;

namespace ApiPollingWorker.Configuration;

public sealed class WorkerOptions
{
    // appsettings.json içinde okunacak bölümün adı.
    public const string SectionName = "Worker";

    // İki API sorgulama turu arasında beklenecek dakika.
    [Range(0.01, 1440)]
    public double IntervalMinutes { get; init; } = 1;

    // true: Açılır açılmaz çalışır. false: İlk çalışmadan önce süre kadar bekler.
    public bool RunImmediately { get; init; } = true;
}
