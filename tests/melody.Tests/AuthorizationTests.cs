using System.Net;

namespace melody.Tests;

/// <summary>
/// Integration tests covering the role-based authorization behavior introduced across
/// Phases 1, 3, and 4: the global fallback policy, the Administrator-only Register page, and the
/// role-scoped Admin/Teacher controllers.
/// </summary>
public class AuthorizationTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public AuthorizationTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Anonymous_Root_RedirectsToLoginPath()
    {
        var client = _factory.CreateClientAs(null);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Anonymous_Register_IsNotOk()
    {
        var client = _factory.CreateClientAs(null);

        var response = await client.GetAsync("/Identity/Account/Register");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_Register_IsForbidden()
    {
        var client = _factory.CreateClientAs("Teacher");

        var response = await client.GetAsync("/Identity/Account/Register");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_CanAccessAdmin_ButNotTeacher()
    {
        var client = _factory.CreateClientAs("Administrator");

        var adminResponse = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        var teacherResponse = await client.GetAsync("/Teacher");
        Assert.Equal(HttpStatusCode.Forbidden, teacherResponse.StatusCode);
    }

    [Fact]
    public async Task Teacher_CanAccessTeacher_ButNotAdmin()
    {
        var client = _factory.CreateClientAs("Teacher");

        var teacherResponse = await client.GetAsync("/Teacher");
        Assert.Equal(HttpStatusCode.OK, teacherResponse.StatusCode);

        var adminResponse = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
    }
}
