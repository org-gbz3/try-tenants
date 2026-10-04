using Backend.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backend.Auth;

public class PasskeySetupLinks(UserManager<ApplicationUser> userManager, IOptions<AppOptions> appOptions)
{
    public async Task<string> CreateAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateUserTokenAsync(user, PasskeySetupTokenProvider.ProviderName, PasskeySetupTokenProvider.Purpose);
        var query = QueryString.Create(new Dictionary<string, string?> { ["userId"] = user.Id, ["token"] = token });
        return $"{appOptions.Value.PublicBaseUrl.TrimEnd('/')}/auth/setup-passkey{query}";
    }

    public async Task<ApplicationUser?> FindUserAsync(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var valid = await userManager.VerifyUserTokenAsync(user, PasskeySetupTokenProvider.ProviderName, PasskeySetupTokenProvider.Purpose, token);
        return valid ? user : null;
    }
}
