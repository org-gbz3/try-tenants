using System.ComponentModel.DataAnnotations;
using Backend.Auth;
using Backend.Data;
using Backend.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.SystemAdmin)]
public class AdminUsersController(
    UserManager<ApplicationUser> userManager,
    IAuthEmailSender emailSender,
    PasskeySetupLinks setupLinks,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    // パスキーをすべて失った利用者の唯一の復旧手段(decisions/0002 参照)。本人確認は運用側で行う前提。
    [HttpPost("passkey-reset")]
    public async Task<IActionResult> PasskeyReset(PasskeyResetRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "ユーザーが見つかりません。");
        }

        // 紛失したパスキーが第三者の手にある可能性を考え、既存のパスキーはすべて無効にする。
        foreach (var passkey in await userManager.GetPasskeysAsync(user))
        {
            await userManager.RemovePasskeyAsync(user, passkey.CredentialId);
        }

        // スタンプ更新で既存セッションと発行済みの設定リンクを失効させる。新しいリンクは更新後に発行する。
        await userManager.UpdateSecurityStampAsync(user);
        var link = await setupLinks.CreateAsync(user);
        await emailSender.SendPasskeyResetLinkAsync(user.Email!, link, cancellationToken);

        logger.LogInformation("System admin {AdminId} reset passkeys of user {UserId}", userManager.GetUserId(User), user.Id);
        return Accepted();
    }
}

public record PasskeyResetRequest([Required, EmailAddress] string Email);
