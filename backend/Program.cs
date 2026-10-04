using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 送信先は環境変数でのみ指定する方針のため、appsettings などの値で有効化されないよう IConfiguration ではなく環境変数を直接見る。
// 送信先の無い環境(テスト・本番の未設定時など)では SDK 自体を登録せず、送信失敗のリトライや計測の負荷を生じさせない。
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var otelDisabled = string.Equals(Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED"), "true", StringComparison.OrdinalIgnoreCase);
if (!string.IsNullOrWhiteSpace(otlpEndpoint) && !otelDisabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource =>
        {
            // AddService は OTEL_SERVICE_NAME から検出した値を上書きするため、環境変数で指定された場合は呼ばない。
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")))
            {
                resource.AddService("try-tenants-backend");
            }
        })
        // 計装オプションは既定のままにする。既定では URL クエリはマスクされ、リクエスト本文や SQL パラメーター値は記録されない。
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            // .NET 9 以降はランタイムが System.Runtime メーターを公開するため、Runtime 計装パッケージは使わない。
            .AddMeter("System.Runtime"))
        .WithLogging(configureBuilder: null, configureOptions: options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        })
        // 送信先・プロトコルは OTEL_EXPORTER_OTLP_ENDPOINT / OTEL_EXPORTER_OTLP_PROTOCOL から読み込まれる。
        .UseOtlpExporter();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// 未定義の API に SPA の HTML を返さないようにする。
app.MapFallback("/api/{**path}", () => Results.NotFound());

// クライアント側のルートへ直接アクセスした場合も SPA を起動する。
app.MapFallbackToFile("index.html");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
