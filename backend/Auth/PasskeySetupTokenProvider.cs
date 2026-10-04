using Backend.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backend.Auth;

// 登録確認メールと管理者による再設定メールの両方で、パスキー登録を許可する使い捨てトークンとして使う。
// DataProtector トークンはセキュリティスタンプを含むため、登録成功時にスタンプを更新すると再利用できなくなる。
public class PasskeySetupTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasskeySetupTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PasskeySetup";
    public const string Purpose = "PasskeySetup";
}

public class PasskeySetupTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasskeySetupTokenProviderOptions()
    {
        Name = PasskeySetupTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(24);
    }
}
