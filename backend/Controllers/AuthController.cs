using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Backend.Auth;
using Backend.Data;
using Backend.Email;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAntiforgery antiforgery,
    IAuthEmailSender emailSender,
    PasskeySetupLinks setupLinks,
    ILogger<AuthController> logger) : ControllerBase
{
    private const int MaxPasskeyNameLength = 100;

    [HttpGet("csrf")]
    [AllowAnonymous]
    public CsrfResponse Csrf()
    {
        // トークンは現在のユーザーに紐づくため、ログイン前後で取り直してもらう前提で毎回発行する。
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new CsrfResponse(tokens.RequestToken!);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthEmail)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        // 登録済みかどうかを推測されないよう、結果にかかわらず同じ応答を返す。
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email };
            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                logger.LogWarning("Failed to create user: {Errors}", string.Join(",", created.Errors.Select(e => e.Code)));
                return Accepted();
            }
        }
        else if (user.EmailConfirmed)
        {
            return Accepted();
        }

        var link = await setupLinks.CreateAsync(user);
        await emailSender.SendRegistrationLinkAsync(email, link, cancellationToken);
        return Accepted();
    }

    [HttpPost("passkey-setup/options")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthEmail)]
    public async Task<IActionResult> PasskeySetupOptions(PasskeySetupOptionsRequest request)
    {
        var user = await setupLinks.FindUserAsync(request.UserId, request.Token);
        if (user is null)
        {
            return InvalidSetupLink();
        }

        var options = await signInManager.MakePasskeyCreationOptionsAsync(ToUserEntity(user));
        return Content(options, "application/json");
    }

    [HttpPost("passkey-setup")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthEmail)]
    public async Task<IActionResult> PasskeySetup(PasskeySetupRequest request)
    {
        var user = await setupLinks.FindUserAsync(request.UserId, request.Token);
        if (user is null)
        {
            return InvalidSetupLink();
        }

        var attestation = await TryPerformAttestationAsync(request.Credential);
        // 作成オプションを発行したユーザーと、リンクのユーザーが一致する場合だけ受け付ける。
        if (attestation is not { Succeeded: true } || attestation.UserEntity.Id != user.Id)
        {
            return PasskeyRegistrationFailed();
        }

        attestation.Passkey.Name = NormalizePasskeyName(request.Name);
        var added = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        if (!added.Succeeded)
        {
            return PasskeyRegistrationFailed();
        }

        // パスキーを登録できたことをもってメールアドレスの所有確認とし、スタンプ更新でリンクを使い捨てにする。
        user.EmailConfirmed = true;
        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "passkey");
        return NoContent();
    }

    [HttpPost("login/options")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginOptions()
    {
        // ユーザーを指定せず、認証器に保存された discoverable credential から選んでもらう。
        var options = await signInManager.MakePasskeyRequestOptionsAsync(user: null);
        return Content(options, "application/json");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        SignInResult result;
        try
        {
            result = await signInManager.PasskeySignInAsync(request.Credential.GetRawText());
        }
        catch (InvalidOperationException)
        {
            // login/options を経ずに呼ばれた場合。
            result = SignInResult.Failed;
        }

        if (!result.Succeeded)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "サインインできませんでした。");
        }

        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return new MeResponse(user.Email!, [.. roles]);
    }

    [HttpGet("passkeys")]
    public async Task<ActionResult<IEnumerable<PasskeyResponse>>> GetPasskeys()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        return passkeys
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PasskeyResponse(WebEncoders.Base64UrlEncode(p.CredentialId), p.Name, p.CreatedAt, p.IsBackedUp))
            .ToList();
    }

    [HttpPost("passkeys/options")]
    public async Task<IActionResult> AddPasskeyOptions()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var options = await signInManager.MakePasskeyCreationOptionsAsync(ToUserEntity(user));
        return Content(options, "application/json");
    }

    [HttpPost("passkeys")]
    public async Task<IActionResult> AddPasskey(AddPasskeyRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var attestation = await TryPerformAttestationAsync(request.Credential);
        if (attestation is not { Succeeded: true } || attestation.UserEntity.Id != user.Id)
        {
            return PasskeyRegistrationFailed();
        }

        attestation.Passkey.Name = NormalizePasskeyName(request.Name);
        var added = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        return added.Succeeded ? NoContent() : PasskeyRegistrationFailed();
    }

    [HttpDelete("passkeys/{id}")]
    public async Task<IActionResult> DeletePasskey(string id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        byte[] credentialId;
        try
        {
            credentialId = WebEncoders.Base64UrlDecode(id);
        }
        catch (FormatException)
        {
            return NotFound();
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        if (!passkeys.Any(p => p.CredentialId.AsSpan().SequenceEqual(credentialId)))
        {
            return NotFound();
        }

        // パスキーのみの認証で最後の 1 つを消すと、管理者による再設定以外に戻る手段が無くなるため拒否する。
        if (passkeys.Count <= 1)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "最後のパスキーは削除できません。");
        }

        var removed = await userManager.RemovePasskeyAsync(user, credentialId);
        return removed.Succeeded ? NoContent() : Problem(detail: "パスキーを削除できませんでした。");
    }

    private async Task<PasskeyAttestationResult?> TryPerformAttestationAsync(JsonElement credential)
    {
        try
        {
            return await signInManager.PerformPasskeyAttestationAsync(credential.GetRawText());
        }
        catch (InvalidOperationException)
        {
            // 作成オプションの発行を経ずに呼ばれた場合。
            return null;
        }
    }

    private static PasskeyUserEntity ToUserEntity(ApplicationUser user) => new()
    {
        Id = user.Id,
        Name = user.Email!,
        DisplayName = user.Email!,
    };

    private static string? NormalizePasskeyName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxPasskeyNameLength ? trimmed[..MaxPasskeyNameLength] : trimmed;
    }

    private ObjectResult InvalidSetupLink() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, detail: "リンクが無効か、有効期限が切れています。");

    private ObjectResult PasskeyRegistrationFailed() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, detail: "パスキーを登録できませんでした。");
}

public record CsrfResponse(string Token);

public record RegisterRequest([Required, EmailAddress, MaxLength(256)] string Email);

public record PasskeySetupOptionsRequest([Required] string UserId, [Required] string Token);

public record PasskeySetupRequest([Required] string UserId, [Required] string Token, JsonElement Credential, string? Name);

public record LoginRequest(JsonElement Credential);

public record AddPasskeyRequest(JsonElement Credential, string? Name);

public record MeResponse(string Email, string[] Roles);

public record PasskeyResponse(string Id, string? Name, DateTimeOffset CreatedAt, bool IsBackedUp);
