using System.Net;
using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Integration tests proving the FR-level data-isolation guarantee of
/// <c>TeacherController.Index</c>: a teacher sees only their own assigned students, a teacher
/// with zero students sees the friendly empty state, and non-Teacher roles remain forbidden on
/// <c>/Teacher</c> (regression check against <see cref="AuthorizationTests"/>'s existing
/// role-boundary coverage, exercised here with the new user-id-scoped client).
/// </summary>
public class TeacherStudentListTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public TeacherStudentListTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Index_ShowsOnlyOwnStudents_NotAnotherTeachersStudents()
    {
        var (teacherAUserId, _) = await SeedTeacherWithStudentAsync("Teacher A", "Student Alpha");
        var (_, _) = await SeedTeacherWithStudentAsync("Teacher B", "Student Bravo");

        var client = _factory.CreateClientAs("Teacher", teacherAUserId);

        var response = await client.GetAsync("/Teacher");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Student Alpha", body);
        Assert.DoesNotContain("Student Bravo", body);
    }

    [Fact]
    public async Task Index_WithNoAssignedStudents_ShowsEmptyStateMessage()
    {
        var (teacherUserId, _) = await SeedTeacherWithStudentAsync("Teacher With No Students", student: null);

        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        var response = await client.GetAsync("/Teacher");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("You don't have any assigned students yet.", body);
    }

    [Fact]
    public async Task Administrator_CannotAccessTeacherStudentList()
    {
        var (teacherUserId, _) = await SeedTeacherWithStudentAsync("Teacher C", "Student Charlie");
        _ = teacherUserId; // Seeded to ensure data exists that must not leak via another role.

        var client = _factory.CreateClientAs("Administrator");

        var response = await client.GetAsync("/Teacher");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Seeds a Teacher (with a backing ApplicationUser) and, when <paramref name="student"/> is
    /// non-null, one assigned Student with that name. Returns the teacher's ApplicationUser.Id
    /// (for use with <see cref="MelodyWebApplicationFactory.CreateClientAs"/>) and the Teacher.Id.
    /// </summary>
    private async Task<(string ApplicationUserId, int TeacherId)> SeedTeacherWithStudentAsync(
        string teacherName, string? student)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"teacher-student-list-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"teacher-student-list-{Guid.NewGuid():N}@melodybooker.local",
            EmailConfirmed = true,
            DisplayName = teacherName,
        };

        var createResult = await userManager.CreateAsync(user, "Test#12345");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, "Teacher");

        var teacher = new Teacher
        {
            ApplicationUserId = user.Id,
            FullName = teacherName,
        };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        if (student is not null)
        {
            context.Students.Add(new Student
            {
                FullName = student,
                Class = 3,
                Instrument = Instrument.Guitar,
                TeacherId = teacher.Id,
            });
            await context.SaveChangesAsync();
        }

        return (user.Id, teacher.Id);
    }
}
