using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Unit tests for <see cref="AdminUserSeeder"/>: idempotency across repeated calls, and graceful
/// no-op behavior when <c>Admin:Email</c>/<c>Admin:Password</c> are not configured.
/// </summary>
public class AdminUserSeederTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfiguration(string email, string password)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"] = email,
                ["Admin:Password"] = password,
            })
            .Build();
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_CreatesExactlyOneAdministrator()
    {
        using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var config = BuildConfiguration("admin@test.local", "Test#12345");

        // AdminUserSeeder assumes the Administrator role already exists (Program.cs always
        // runs RoleSeeder first), so seed it here too.
        await RoleSeeder.SeedAsync(scope.ServiceProvider);

        await AdminUserSeeder.SeedAsync(scope.ServiceProvider, config);
        await AdminUserSeeder.SeedAsync(scope.ServiceProvider, config);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admins = await userManager.GetUsersInRoleAsync("Administrator");

        Assert.Single(admins);
    }

    [Fact]
    public async Task SeedAsync_WithMissingConfiguration_DoesNotThrowAndCreatesNoUser()
    {
        using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var config = BuildConfiguration(string.Empty, string.Empty);

        var exception = await Record.ExceptionAsync(() => AdminUserSeeder.SeedAsync(scope.ServiceProvider, config));

        Assert.Null(exception);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Empty(userManager.Users);
    }
}
