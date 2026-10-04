using Backend.Data;
using Backend.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Backend.Tests.Infrastructure;

// テストごとに生成し、SQLite のメモリ DB・メール送信・Web ルートを独立させる。
public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public const string Origin = "http://localhost";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _webRoot = Directory.CreateTempSubdirectory("backend-tests-").FullName;

    static TestAppFactory()
    {
        // 開発コンテナの OTel 送信先設定を引き継いで、テストのテレメトリをダッシュボードへ送らないようにする。
        Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");
    }

    public TestAppFactory()
    {
        _connection.Open();
        // フロントエンドのビルド有無に依存しないよう、SPA のシェルを模したファイルを置く。
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>spa</title>");
    }

    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseWebRoot(_webRoot);
        builder.UseSetting("App:PublicBaseUrl", Origin);
        builder.UseSetting("Passkey:ServerDomain", "localhost");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IAuthEmailSender>();
            services.AddSingleton<IAuthEmailSender>(Emails);

            // 管理者による再設定の直後にセッションが失効することを確認するため、毎回スタンプを検証させる。
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    public TestClient CreateTestClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // パスキーの検証は Origin ヘッダーと clientDataJSON の origin の一致を確認するため、ブラウザーと同じく付与する。
        client.DefaultRequestHeaders.Add("Origin", Origin);
        return new TestClient(client, Emails);
    }

    public async Task GrantSystemAdminAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roleManager.CreateAsync(new IdentityRole(Backend.Auth.Roles.SystemAdmin));
        var user = await userManager.FindByEmailAsync(email);
        await userManager.AddToRoleAsync(user!, Backend.Auth.Roles.SystemAdmin);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            if (Directory.Exists(_webRoot))
            {
                Directory.Delete(_webRoot, recursive: true);
            }
        }
    }
}
