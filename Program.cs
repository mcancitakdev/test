using System.Net.Http.Headers;
using ApiPollingWorker.Clients;
using ApiPollingWorker.Configuration;
using ApiPollingWorker.Services;
using ApiPollingWorker.Workers;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Windows Service olarak kurulduğunda görünen ad. "dotnet run" kullanımını etkilemez.
builder.Services.AddWindowsService(options => options.ServiceName = "API Polling Worker");

// appsettings.json içindeki "Worker" bölümünü WorkerOptions sınıfına bağlar.
// Geçersiz bir ayar varsa servis açılışta durur; çalışırken sürpriz hata oluşmaz.
builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Kaynak REST API'nin adres, endpoint ve zaman aşımı ayarlarını yükleyip doğrular.
builder.Services.AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Api:BaseUrl mutlak bir URL olmalıdır.")
    .Validate(options => options.Endpoint.StartsWith('/'), "Api:Endpoint '/' ile başlamalıdır.")
    .ValidateOnStart();

// Mesajların gönderileceği SignalR Hub ayarlarını yükleyip doğrular.
builder.Services.AddOptions<SignalROptions>()
    .Bind(builder.Configuration.GetSection(SignalROptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.HubUrl, UriKind.Absolute, out _), "SignalR:HubUrl mutlak bir URL olmalıdır.")
    .ValidateOnStart();

// HttpClientFactory bağlantı ömrünü güvenli biçimde yönetir.
// SourceApiClient her istekte aşağıdaki ortak adresi, süreyi ve başlıkları kullanır.
builder.Services.AddHttpClient<ISourceApiClient, SourceApiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    if (!string.IsNullOrWhiteSpace(options.BearerToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
    }

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);
    }
});

// Singleton: Uygulama boyunca bu sınıflardan yalnızca birer örnek oluşturulur.
// Böylece özellikle SignalR bağlantısı her turda yeniden yaratılmaz.
builder.Services.AddSingleton<ISignalRClient, SignalRClient>();
builder.Services.AddSingleton<IRecordProcessor, RecordProcessor>();

// Host açılınca BackgroundService.ExecuteAsync metodunu otomatik başlatır.
builder.Services.AddHostedService<global::ApiPollingWorker.Workers.ApiPollingWorker>();

// Servisi oluşturur ve uygulama kapanana kadar çalıştırır.
await builder.Build().RunAsync();
