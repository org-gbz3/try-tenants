using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Backend.Tests.Infrastructure;

// ブラウザーの SPA と同じ手順(更新の直前に CSRF トークンを取得)で API を呼ぶ。Cookie は HttpClient が保持する。
public sealed class TestClient(HttpClient http, CapturingEmailSender emails) : IDisposable
{
    public HttpClient Http => http;

    public async Task<string> GetCsrfTokenAsync()
    {
        var response = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        return response.GetProperty("token").GetString()!;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? csrfToken = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : new StringContent(body as string ?? JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken ?? await GetCsrfTokenAsync());
        return await http.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public async Task<string> PostForJsonStringAsync(string path, object? body = null)
    {
        var response = await PostAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public Task<HttpResponseMessage> RegisterAsync(string email) => PostAsync("/api/auth/register", new { email });

    public async Task<HttpResponseMessage> SetupPasskeyAsync(SentEmail email, SoftwareAuthenticator authenticator, string? name = null)
    {
        var (userId, token) = email.SetupParameters;
        var options = await PostForJsonStringAsync("/api/auth/passkey-setup/options", new { userId, token });
        var credential = JsonDocument.Parse(authenticator.CreateAttestation(options)).RootElement;
        return await PostAsync("/api/auth/passkey-setup", new { userId, token, credential, name });
    }

    // 登録 → 確認メール → パスキー登録までを行い、サインイン状態にする。
    public async Task<SoftwareAuthenticator> RegisterWithPasskeyAsync(string email)
    {
        (await RegisterAsync(email)).EnsureSuccessStatusCode();
        var authenticator = new SoftwareAuthenticator();
        (await SetupPasskeyAsync(emails.Last(email), authenticator)).EnsureSuccessStatusCode();
        return authenticator;
    }

    public async Task<HttpResponseMessage> LoginAsync(SoftwareAuthenticator authenticator)
    {
        var options = await PostForJsonStringAsync("/api/auth/login/options");
        var credential = JsonDocument.Parse(authenticator.CreateAssertion(options)).RootElement;
        return await PostAsync("/api/auth/login", new { credential });
    }

    public async Task<HttpResponseMessage> AddPasskeyAsync(SoftwareAuthenticator authenticator, string? name = null)
    {
        var options = await PostForJsonStringAsync("/api/auth/passkeys/options");
        var credential = JsonDocument.Parse(authenticator.CreateAttestation(options)).RootElement;
        return await PostAsync("/api/auth/passkeys", new { credential, name });
    }

    public Task<HttpResponseMessage> LogoutAsync() => PostAsync("/api/auth/logout");

    public Task<HttpResponseMessage> GetMeAsync() => http.GetAsync("/api/auth/me");

    public void Dispose() => http.Dispose();
}
