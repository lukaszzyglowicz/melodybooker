using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;

namespace melody.Data;

/// <summary>
/// Seeds the two fixed roles (Administrator, Teacher) on startup. There is no self-registration
/// flow: the PRD's Access Control section restricts accounts to these two roles, created by an
/// administrator (FR-003/FR-004), not by public sign-up.
/// </summary>
public static class RoleSeeder
{
    public static readonly string[] Roles = ["Administrator", "Teacher"];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
