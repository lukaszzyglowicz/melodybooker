using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;

namespace melody.Data;

/// <summary>
/// Seeds exactly one Administrator-role user from configuration (<c>Admin:Email</c> /
/// <c>Admin:Password</c>) on startup. There is no public sign-up flow (see <see cref="RoleSeeder"/>),
/// so this is the only way an admin account comes into existence.
/// </summary>
public static class AdminUserSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AdminUserSeeder));
            logger.LogWarning(
                "Admin:Email/Admin:Password are not configured; skipping administrator account seeding. " +
                "Set them via dotnet user-secrets (local dev) or Admin__Email/Admin__Password app settings (production).");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Administrator");
        }
    }
}
