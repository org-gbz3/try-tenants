using Backend.Data;
using Microsoft.AspNetCore.Identity;

namespace Backend.Auth;

// 最初の管理者を作る手段。設定値で自動付与すると設定ミスがそのまま権限昇格になるため、明示的なコマンドに限る。
public static class GrantSystemAdminCommand
{
    public const string Name = "grant-system-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine($"使い方: dotnet run --project backend -- {Name} <email>");
            return 1;
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var user = await userManager.FindByEmailAsync(args[1]);
        if (user is null)
        {
            Console.Error.WriteLine("ユーザーが見つかりません。先に画面から登録してください。");
            return 1;
        }

        if (!await roleManager.RoleExistsAsync(Roles.SystemAdmin))
        {
            await roleManager.CreateAsync(new IdentityRole(Roles.SystemAdmin));
        }

        if (!await userManager.IsInRoleAsync(user, Roles.SystemAdmin))
        {
            var result = await userManager.AddToRoleAsync(user, Roles.SystemAdmin);
            if (!result.Succeeded)
            {
                Console.Error.WriteLine(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
                return 1;
            }
        }

        Console.WriteLine($"{args[1]} に {Roles.SystemAdmin} を付与しました。");
        return 0;
    }
}
