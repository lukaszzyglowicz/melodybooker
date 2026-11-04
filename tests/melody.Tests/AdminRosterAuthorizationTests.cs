using System.Net;
using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Teacher_IsForbidden_OnEveryAdminTeachersAction()
    {
        // No assigned student, so DeleteConfirmed's FR-006 block never enters the picture here —
        // only the role check is under test.
        var teacherId = await SeedTeacherAsync();
        var client = _factory.CreateClientAs("Teacher");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/AdminTeachers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/AdminTeachers/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            "/AdminTeachers/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"] = "Blocked Teacher",
                ["Email"] = "blocked-teacher@melodybooker.local",
                ["Password"] = "Test#12345",
            }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/AdminTeachers/Edit/{teacherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            "/AdminTeachers/Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = teacherId.ToString(),
                ["FullName"] = "Blocked Teacher Renamed",
            }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/AdminTeachers/Delete/{teacherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            $"/AdminTeachers/Delete/{teacherId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = teacherId.ToString(),
            }))).StatusCode);
    }

    [Fact]
    public async Task Teacher_IsForbidden_OnEveryAdminStudentsAction()
    {
        // The student has no reservations, so DeleteConfirmed's FR-006 block never enters the
        // picture here — only the role check is under test.
        var (teacherId, studentId) = await SeedTeacherWithStudentAsync();
        var client = _factory.CreateClientAs("Teacher");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/AdminStudents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/AdminStudents/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            "/AdminStudents/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"] = "Blocked Student",
                ["Class"] = "3",
                ["Instrument"] = ((int)Instrument.Guitar).ToString(),
                ["TeacherId"] = teacherId.ToString(),
            }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/AdminStudents/Edit/{studentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            "/AdminStudents/Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = studentId.ToString(),
                ["FullName"] = "Blocked Student Renamed",
                ["Class"] = "3",
                ["Instrument"] = ((int)Instrument.Guitar).ToString(),
                ["TeacherId"] = teacherId.ToString(),
            }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/AdminStudents/Delete/{studentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            $"/AdminStudents/Delete/{studentId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = studentId.ToString(),
            }))).StatusCode);
    }

    /// <summary>
    /// Seeds a Teacher (with a backing ApplicationUser) and no assigned students, so Delete isn't
    /// blocked by the FR-006 rule — that rule is orthogonal to the role-authorization boundary
    /// under test here.
    /// </summary>
    private async Task<int> SeedTeacherAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"teacher-authz-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"teacher-authz-{Guid.NewGuid():N}@melodybooker.local",
            EmailConfirmed = true,
            DisplayName = "Authz Teacher",
        };

        var createResult = await userManager.CreateAsync(user, "Test#12345");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, "Teacher");

        var teacher = new Teacher
        {
            ApplicationUserId = user.Id,
            FullName = "Authz Teacher",
        };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        return teacher.Id;
    }

    /// <summary>
    /// Seeds a Teacher (with a backing ApplicationUser) and one assigned Student that has no
    /// reservations, so Delete isn't blocked by the FR-006 rule — that rule is orthogonal to the
    /// role-authorization boundary under test here.
    /// </summary>
    private async Task<(int TeacherId, int StudentId)> SeedTeacherWithStudentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"teacher-for-student-authz-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"teacher-for-student-authz-{Guid.NewGuid():N}@melodybooker.local",
            EmailConfirmed = true,
            DisplayName = "Authz Student's Teacher",
        };

        var createResult = await userManager.CreateAsync(user, "Test#12345");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, "Teacher");

        var teacher = new Teacher
        {
            ApplicationUserId = user.Id,
            FullName = "Authz Student's Teacher",
        };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        var student = new Student
        {
            FullName = "Authz Student",
            Class = 3,
            Instrument = Instrument.Guitar,
            TeacherId = teacher.Id,
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return (teacher.Id, student.Id);
    }
}
