using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace melody.Tests;

/// <summary>
/// Test host for <c>melody</c>: swaps the real SQL Server <see cref="ApplicationDbContext"/> for
/// a SQLite in-memory database (Program.cs calls <c>Database.MigrateAsync()</c> at startup, which
/// the EF Core InMemory provider does not support since it's a relational-only operation, so
/// SQLite in-memory is used instead), and registers <see cref="TestAuthHandler"/> as the default
/// authentication scheme so tests can authenticate as a given role via a header instead of a real
/// cookie login flow.
/// </summary>
public class MelodyWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminTestEmail = "admin-test@melodybooker.local";
    public const string AdminTestPassword = "Test#12345";
    public const string TeacherTestEmail = "teacher-test@melodybooker.local";
    public const string TeacherTestPassword = "Test#12345";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public MelodyWebApplicationFactory()
    {
        // The SQLite in-memory database only exists while a connection to it is open, so this
        // connection must be kept alive for the lifetime of the factory.
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tells Program.cs to use EnsureCreatedAsync instead of MigrateAsync at startup,
        // since the SqlServer-authored migrations can't run against SQLite.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            // Gives AdminUserSeeder (already invoked by Program.cs at startup) known
            // credentials to seed, instead of the empty values in the local appsettings.json.
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"] = AdminTestEmail,
                ["Admin:Password"] = AdminTestPassword,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Program.cs's own AddDbContext call (UseSqlServer) registers an
            // IDbContextOptionsConfiguration<ApplicationDbContext> delegate that EF Core 7+
            // composes alongside any later AddDbContext call rather than replacing it, so
            // removing only DbContextOptions<ApplicationDbContext> leaves the SqlServer
            // configuration delegate in place and both providers end up registered. Both
            // descriptors must be removed before re-adding with Sqlite.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options
                .UseSqlite(_connection)
                // The committed migrations were authored against SqlServer, so their model
                // snapshot legitimately differs from the one Sqlite produces here; that's
                // expected in this test-only swap, not a real pending-migration problem.
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            // AddIdentity's own AddAuthentication call already set DefaultAuthenticateScheme
            // and DefaultChallengeScheme explicitly (to the Identity cookie scheme), and those
            // take precedence over DefaultScheme. All of them must be overridden here or the
            // auth middleware keeps using the cookie scheme and ignores TestAuthHandler entirely.
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                options.DefaultSignInScheme = TestAuthHandler.SchemeName;
            })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>
    /// Triggers host startup (which runs RoleSeeder + AdminUserSeeder against the SQLite
    /// in-memory database) and additionally creates a Teacher test user, since no existing
    /// seeder covers Teacher accounts. Call once before tests that need seeded users.
    /// </summary>
    public async Task EnsureSeededAsync()
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByEmailAsync(TeacherTestEmail) is null)
        {
            var teacherUser = new ApplicationUser
            {
                UserName = TeacherTestEmail,
                Email = TeacherTestEmail,
                EmailConfirmed = true,
                DisplayName = "Test Teacher",
            };

            var result = await userManager.CreateAsync(teacherUser, TeacherTestPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(teacherUser, "Teacher");
            }
        }
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that authenticates as <paramref name="role"/> via the
    /// <see cref="TestAuthHandler"/> header, or as anonymous when <paramref name="role"/> is null.
    /// Redirects are not followed automatically so tests can assert on the redirect response itself.
    /// </summary>
    public HttpClient CreateClientAs(string? role)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, role);
        }

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
