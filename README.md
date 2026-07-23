# ApiPollingWorker

.NET 10 Worker Service; mevcut REST API'yi periyodik olarak sorgular ve bulunan kayıtları mevcut SignalR Hub metoduna iletir. Hub/server/controller içermez.

## Oluşturma komutları

```powershell
dotnet new worker --framework net10.0 --name ApiPollingWorker
cd ApiPollingWorker
dotnet add package Microsoft.AspNetCore.SignalR.Client --version 10.0.10
dotnet add package Microsoft.Extensions.Http --version 10.0.10
dotnet add package Microsoft.Extensions.Hosting.WindowsServices --version 10.0.10
dotnet add package Microsoft.Extensions.Options.DataAnnotations --version 10.0.10
```

`Microsoft.Extensions.Hosting` 10.0.10 Worker şablonunun temel paketidir. Uygulamadan önce `appsettings.json` adreslerini güncelleyin; token ve API anahtarlarını dosyaya yazmak yerine environment variable/User Secrets/secret store kullanın. Örnek environment variable adları: `Api__BearerToken`, `Api__ApiKey`, `SignalR__AccessToken`.

## Çalıştırma ve doğrulama

```powershell
dotnet restore
dotnet build --configuration Release
dotnet run --project .\ApiPollingWorker.csproj
```

## Windows Service yayınlama

Yönetici PowerShell penceresinde:

```powershell
dotnet publish .\ApiPollingWorker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o C:\Services\ApiPollingWorker
sc.exe create ApiPollingWorker binPath= "C:\Services\ApiPollingWorker\ApiPollingWorker.exe" start= auto DisplayName= "API Polling Worker"
sc.exe description ApiPollingWorker "REST API kayıtlarını mevcut SignalR Hub'a iletir."
sc.exe start ApiPollingWorker
sc.exe query ApiPollingWorker
```

Güncelleme/kaldırma:

```powershell
sc.exe stop ApiPollingWorker
sc.exe delete ApiPollingWorker
```

Servis hesabına yayın klasörü ve gerekiyorsa sertifika/ağ erişimi verilmelidir. Production ortamında geçerli ve güvenilen TLS sertifikaları kullanılmalıdır.

## Test senaryoları

1. API boş liste veya `null` döndürür: bilgi logu yazılır, Hub çağrılmaz.
2. API birden çok geçerli kayıt döndürür: her kayıt yapılandırılmış metot adına gönderilir ve sayılar loglanır.
3. API 4xx/5xx döndürür: status/body loglanır, servis çalışmaya devam eder.
4. API erişilemez veya timeout olur: hata türü ayrı loglanır, sonraki turda tekrar denenir.
5. API bozuk JSON döndürür: deserialize uyarısı yazılır, servis çökmez.
6. Hub kapalıdır: bağlantı hatası loglanır; sonraki kayıt/tur yeniden dener.
7. Hub bağlantısı sonradan kopar: otomatik reconnect olayları loglanır.
8. İşlem periyottan uzun sürer: ikinci tur paralel başlamaz; tamamlandıktan sonra tam interval beklenir.
9. Servis işlem sırasında durdurulur: HTTP/SignalR işlemi iptal edilir ve kapanış iptali hata olarak loglanmaz.
10. Zorunlu URL/metot/interval/timeout ayarı geçersizdir: options validation başlangıçta anlaşılır hata üretir.

## Klasör yapısı

```text
ApiPollingWorker/
├── Clients/
│   ├── ISourceApiClient.cs
│   ├── SourceApiClient.cs
│   ├── ISignalRClient.cs
│   └── SignalRClient.cs
├── Configuration/
│   ├── WorkerOptions.cs
│   ├── ApiOptions.cs
│   └── SignalROptions.cs
├── Models/
│   ├── ApiRecordDto.cs
│   └── SignalRMessageDto.cs
├── Services/
│   ├── IRecordProcessor.cs
│   └── RecordProcessor.cs
├── Workers/
│   └── ApiPollingWorker.cs
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```
