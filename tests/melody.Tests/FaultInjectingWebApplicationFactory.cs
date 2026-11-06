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
using Microsoft.Extensions.Logging;

namespace melody.Tests;

/// <summary>
/// A separate test host from <see cref="MelodyWebApplicationFactory"/>, used only by
/// <see cref="SwallowedExceptionLoggingTests"/>. Mirrors its SQLite-in-memory + <see
/// cref="TestAuthHandler"/> setup, but additionally swaps <see cref="ApplicationDbContext"/>'s
/// registration for <see cref="ThrowingApplicationDbContext"/> (so a test can force a real
/// <see cref="DbUpdateException"/> on demand) and registers <see cref="CapturingLoggerProvider"/>
/// (so a test can assert on what a controller logged). Kept entirely separate so none of
/// <see cref="MelodyWebApplicationFactory"/>'s 14 existing consumer test files are affected.
/// </summary>
public class FaultInjectingWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminTestEmail = "admin-fault-test@melodybooker.local";
    public const string AdminTestPassword = "Test#12345";
    public const string TeacherTestEmail = "teacher-fault-test@melodybooker.local";
    public const string TeacherTestPassword = "Test#12345";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CapturingLoggerProvider CapturedLogs { get; } = new();

    public FaultInjectingWebApplicationFactory()
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

        builder.ConfigureLogging(logging => logging.AddProvider(CapturedLogs));

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

            // AddDbContext above registers ApplicationDbContext itself (alongside
            // DbContextOptions<ApplicationDbContext>, which this registration reuses); swap just
            // that registration for ThrowingApplicationDbContext so the rest of the DbContext
            // wiring (options, warnings) stays identical to MelodyWebApplicationFactory's.
            services.RemoveAll<ApplicationDbContext>();
            services.AddScoped<ApplicationDbContext>(sp =>
                new ThrowingApplicationDbContext(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>()));

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
    /// When <paramref name="userId"/> is supplied, it is sent as the <c>X-Test-UserId</c> header so
    /// the authenticated principal's <c>NameIdentifier</c> claim matches a specific seeded
    /// <c>ApplicationUser.Id</c> instead of the default <c>"test-{role}"</c> value. Redirects are
    /// not followed automatically so tests can assert on the redirect response itself.
    /// </summary>
    public HttpClient CreateClientAs(string? role, string? userId = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, role);
        }

        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId);
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
