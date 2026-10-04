using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Tests.Infrastructure;

namespace Backend.Tests;

public sealed class AuthApiTests : IDisposable
{
    private const string Email = "user@example.com";

    private readonly TestAppFactory _factory = new();
    private readonly TestClient _client;

    public AuthApiTests()
    {
        _client = _factory.CreateTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact(DisplayName = "登録APIは未登録と登録済みのメールアドレスで同じ応答を返す")]
    public async Task Register_ReturnsSameResponseRegardlessOfExistence()
    {
        using var authenticator = await _client.RegisterWithPasskeyAsync(Email);
        var sentBefore = _factory.Emails.Sent.Count;

        var existing = await _client.RegisterAsync(Email);
        var fresh = await _client.RegisterAsync("new@example.com");

        Assert.Equal(HttpStatusCode.Accepted, existing.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, fresh.StatusCode);
        // 登録済みの利用者にはメールを送らず、未登録の利用者にだけ送る。
        Assert.Equal(sentBefore + 1, _factory.Emails.Sent.Count);
    }

    [Fact(DisplayName = "確認メールのリンクでパスキーを登録するとサインイン状態になる")]
    public async Task PasskeySetup_SignsIn()
    {
        using var authenticator = await _client.RegisterWithPasskeyAsync(Email);

        var me = await _client.GetMeAsync();

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Email, body.GetProperty("email").GetString());
    }

    [Fact(DisplayName = "パスキー設定リンクは一度使うと再利用できない")]
    public async Task PasskeySetup_TokenIsSingleUse()
    {
        using var authenticator = await _client.RegisterWithPasskeyAsync(Email);
        var (userId, token) = _factory.Emails.Last(Email).SetupParameters;

        var response = await _client.PostAsync("/api/auth/passkey-setup/options", new { userId, token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "別ユーザーのIDと設定トークンの組み合わせでは設定できない")]
    public async Task PasskeySetup_RejectsTokenOfAnotherUser()
    {
        await _client.RegisterAsync("a@example.com");
        await _client.RegisterAsync("b@example.com");
        var (userIdA, _) = _factory.Emails.Last("a@example.com").SetupParameters;
        var (_, tokenB) = _factory.Emails.Last("b@example.com").SetupParameters;

        var response = await _client.PostAsync("/api/auth/passkey-setup/options", new { userId = userIdA, token = tokenB });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "登録済みのパスキーでサインインできる")]
    public async Task Login_WithRegisteredPasskey_SignsIn()
    {
        using var authenticator = await _client.RegisterWithPasskeyAsync(Email);
        await _client.LogoutAsync();

        var login = await _client.LoginAsync(authenticator);

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetMeAsync()).StatusCode);
    }

    [Fact(DisplayName = "登録されていないパスキーではサインインできない")]
    public async Task Login_WithUnknownPasskey_Fails()
    {
        using var registered = await _client.RegisterWithPasskeyAsync(Email);
        await _client.LogoutAsync();
        using var unknown = new SoftwareAuthenticator();

        var login = await _client.LoginAsync(unknown);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetMeAsync()).StatusCode);
    }

    [Fact(DisplayName = "CSRFトークンの無い更新リクエストは拒否される")]
    public async Task UnsafeRequest_WithoutCsrfToken_IsRejected()
    {
        var response = await _client.Http.PostAsJsonAsync("/api/auth/register", new { email = Email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Emails.Sent);
    }

    [Fact(DisplayName = "ログイン前に取得したCSRFトークンはログイン後に使えない")]
    public async Task CsrfToken_IssuedBeforeLogin_IsRejectedAfterLogin()
    {
        var anonymousToken = await _client.GetCsrfTokenAsync();
        using var authenticator = await _client.RegisterWithPasskeyAsync(Email);

        var response = await _client.SendAsync(HttpMethod.Post, "/api/auth/logout", csrfToken: anonymousToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "未認証で保護されたAPIを呼ぶとリダイレクトせず401を返す")]
    public async Task ProtectedApi_WithoutAuthentication_Returns401()
    {
        var response = await _client.GetMeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact(DisplayName = "最後のパスキーは削除できない")]
    public async Task DeletePasskey_LastOne_IsRejected()
    {
        using var authenticator = await _client.RegisterWithPasskeyAsync(Email);
        var passkeys = await _client.Http.GetFromJsonAsync<JsonElement[]>("/api/auth/passkeys");

        var response = await _client.SendAsync(HttpMethod.Delete, $"/api/auth/passkeys/{passkeys![0].GetProperty("id").GetString()}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "パスキーを追加すると元のパスキーを削除できる")]
    public async Task AddPasskey_AllowsDeletingOriginal()
    {
        using var original = await _client.RegisterWithPasskeyAsync(Email);
        using var added = new SoftwareAuthenticator();
        (await _client.AddPasskeyAsync(added, "追加した端末")).EnsureSuccessStatusCode();
        var passkeys = await _client.Http.GetFromJsonAsync<JsonElement[]>("/api/auth/passkeys");
        var originalId = passkeys!.Single(p => p.GetProperty("name").ValueKind == JsonValueKind.Null).GetProperty("id").GetString();

        var response = await _client.SendAsync(HttpMethod.Delete, $"/api/auth/passkeys/{originalId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single((await _client.Http.GetFromJsonAsync<JsonElement[]>("/api/auth/passkeys"))!);
    }

    [Fact(DisplayName = "一般ユーザーは管理者APIを呼べない")]
    public async Task AdminApi_ForRegularUser_Returns403()
    {
        using var authenticator = await _client.RegisterWithPasskeyAsync(Email);

        var response = await _client.PostAsync("/api/admin/users/passkey-reset", new { email = Email });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "管理者が再設定すると対象ユーザーの既存セッションが失効する")]
    public async Task AdminPasskeyReset_InvalidatesSessions()
    {
        using var userAuthenticator = await _client.RegisterWithPasskeyAsync(Email);
        using var admin = await CreateSignedInAdminAsync();

        (await admin.Client.PostAsync("/api/admin/users/passkey-reset", new { email = Email })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetMeAsync()).StatusCode);
    }

    [Fact(DisplayName = "管理者が再設定すると対象ユーザーの旧パスキーでサインインできなくなる")]
    public async Task AdminPasskeyReset_RevokesPasskeys()
    {
        using var userAuthenticator = await _client.RegisterWithPasskeyAsync(Email);
        await _client.LogoutAsync();
        using var admin = await CreateSignedInAdminAsync();

        (await admin.Client.PostAsync("/api/admin/users/passkey-reset", new { email = Email })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.LoginAsync(userAuthenticator)).StatusCode);
    }

    [Fact(DisplayName = "管理者の再設定メールのリンクで新しいパスキーを登録できる")]
    public async Task AdminPasskeyReset_SendsUsableSetupLink()
    {
        using var userAuthenticator = await _client.RegisterWithPasskeyAsync(Email);
        await _client.LogoutAsync();
        using var admin = await CreateSignedInAdminAsync();
        (await admin.Client.PostAsync("/api/admin/users/passkey-reset", new { email = Email })).EnsureSuccessStatusCode();
        var resetEmail = _factory.Emails.Last(Email);
        using var newAuthenticator = new SoftwareAuthenticator();

        var response = await _client.SetupPasskeyAsync(resetEmail, newAuthenticator);

        Assert.Equal(SentEmailKind.PasskeyReset, resetEmail.Kind);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact(DisplayName = "認証APIの応答はキャッシュさせない")]
    public async Task AuthApi_ResponseIsNoStore()
    {
        var response = await _client.Http.GetAsync("/api/auth/csrf");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact(DisplayName = "存在しないAPIはSPAのHTMLではなく404を返す")]
    public async Task UnknownApi_Returns404WithoutHtml()
    {
        var response = await _client.Http.GetAsync("/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "未認証でも画面のルートにはSPAのHTMLを返す")]
    public async Task ClientRoute_WithoutAuthentication_ReturnsSpa()
    {
        var response = await _client.Http.GetAsync("/account");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<AdminSession> CreateSignedInAdminAsync()
    {
        const string adminEmail = "admin@example.com";
        var client = _factory.CreateTestClient();
        var authenticator = await client.RegisterWithPasskeyAsync(adminEmail);
        await _factory.GrantSystemAdminAsync(adminEmail);
        // ロールは Cookie 発行時に載るため、付与後にサインインし直す。
        await client.LogoutAsync();
        (await client.LoginAsync(authenticator)).EnsureSuccessStatusCode();
        return new AdminSession(client, authenticator);
    }

    private sealed record AdminSession(TestClient Client, SoftwareAuthenticator Authenticator) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Authenticator.Dispose();
        }
    }
}
