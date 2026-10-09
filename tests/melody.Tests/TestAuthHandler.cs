using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace melody.Tests;

/// <summary>
/// Test-only authentication handler that stands in for cookie-based Identity sign-in. Reads the
/// <see cref="RoleHeaderName"/> request header and, when present, authenticates the caller with
/// that role claim; otherwise the request is treated as anonymous. This avoids needing a real
/// cookie login flow per test while still exercising the same <c>[Authorize]</c>/role-based
/// authorization pipeline configured in <c>Program.cs</c>.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RoleHeaderName = "X-Test-Role";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeaderName, out var roleValues) ||
            string.IsNullOrWhiteSpace(roleValues.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = roleValues.ToString();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"test-{role.ToLowerInvariant()}"),
            new Claim(ClaimTypes.Name, $"test-{role.ToLowerInvariant()}"),
            new Claim(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Mirrors Program.cs's ConfigureApplicationCookie LoginPath so anonymous-access
        // assertions reflect real sign-in behavior without standing up cookie auth in tests.
        Response.Redirect("/Identity/Account/Login");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
