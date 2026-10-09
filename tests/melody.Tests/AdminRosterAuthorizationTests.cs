using System.Net;

namespace melody.Tests;

/// <summary>
/// Integration tests confirming the new Administrator-only roster controllers
/// (<c>AdminTeachersController</c>, <c>AdminStudentsController</c>) inherit the existing
/// role-authorization boundary, matching the pattern in <see cref="AuthorizationTests"/>.
/// </summary>
public class AdminRosterAuthorizationTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public AdminRosterAuthorizationTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Administrator_CanAccessAdminTeachers_AndAdminStudents()
    {
        var client = _factory.CreateClientAs("Administrator");

        var teachersResponse = await client.GetAsync("/AdminTeachers");
        Assert.Equal(HttpStatusCode.OK, teachersResponse.StatusCode);

        var studentsResponse = await client.GetAsync("/AdminStudents");
        Assert.Equal(HttpStatusCode.OK, studentsResponse.StatusCode);
    }

    [Fact]
    public async Task Teacher_CannotAccessAdminTeachers_OrAdminStudents()
    {
        var client = _factory.CreateClientAs("Teacher");

        var teachersResponse = await client.GetAsync("/AdminTeachers");
        Assert.Equal(HttpStatusCode.Forbidden, teachersResponse.StatusCode);

        var studentsResponse = await client.GetAsync("/AdminStudents");
        Assert.Equal(HttpStatusCode.Forbidden, studentsResponse.StatusCode);
    }
}
