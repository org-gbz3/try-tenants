using System.Threading.RateLimiting;
using Backend.Data;
using Backend.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Auth;

public static class AuthServiceCollectionExtensions
{
    public static WebApplicationBuilder AddAppAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var isDevelopment = builder.Environment.IsDevelopment();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

        services.AddOptions<AppOptions>()
            .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IAuthEmailSender, SmtpAuthEmailSender>();
        services.AddScoped<PasskeySetupLinks>();

        services.AddDataProtection()
            .SetApplicationName("try-tenants")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                options.User.RequireUniqueEmail = true;
                // ユーザー名にはメールアドレスをそのまま使う。形式は登録 API の EmailAddress 検証に任せる。
                options.User.AllowedUserNameCharacters = "";
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddTokenProvider<PasskeySetupTokenProvider>(PasskeySetupTokenProvider.ProviderName);
        services.Configure<PasskeySetupTokenProviderOptions>(_ => { });

        services.Configure<IdentityPasskeyOptions>(options =>
        {
            // 未設定時は Host ヘッダーが RP ID になるため、配備先のドメインを明示する。
            options.ServerDomain = builder.Configuration["Passkey:ServerDomain"];
            // パスワードを持たずパスキー単独で認証するため、生体認証や PIN による本人確認を必須にする。
            options.UserVerificationRequirement = "required";
            // ユーザー名を入力せずにサインインできるよう、認証器に保存される discoverable credential を求める。
            options.ResidentKeyRequirement = "required";
        });

        // 管理者による再設定で既存セッションを速やかに失効させるため、スタンプの検証間隔を既定の 30 分から短くする。
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        var securePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
            // SPA から API を呼ぶ構成のため、ログイン画面へのリダイレクトではなく状態コードで返す。
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.SecurePolicy = securePolicy;
        });

        // 個別に許可しない限り認証を必須にし、保護漏れを防ぐ(decisions/0003 参照)。
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.AuthEmail, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        services.AddControllers(options =>
            options.Filters.Add<ValidateAntiforgeryTokenFilter>());

        return builder;
    }
}
