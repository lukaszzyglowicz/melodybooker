using System.Net;
using System.Text.RegularExpressions;
using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Integration tests proving the FR-006 deletion-blocking rules actually fire end-to-end:
/// a teacher with assigned students, or a student with active reservations, cannot be deleted
/// through <c>AdminTeachersController</c>/<c>AdminStudentsController</c>.
/// </summary>
public class AdminRosterDeletionTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public AdminRosterDeletionTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeleteTeacher_WithAssignedStudent_IsBlocked()
    {
        var teacherId = await SeedTeacherWithAssignedStudentAsync();
        var client = _factory.CreateClientAs("Administrator");

        // The teacher already has an assigned student, so the Delete confirmation GET renders
        // the blocked message directly (no form/token). Pull a valid antiforgery token+cookie
        // from the Create page instead, which always renders a plain form.
        var tokenPageResponse = await client.GetAsync("/AdminTeachers/Create");
        Assert.Equal(HttpStatusCode.OK, tokenPageResponse.StatusCode);
        var token = ExtractAntiForgeryToken(await tokenPageResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            $"/AdminTeachers/Delete/{teacherId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = teacherId.ToString(),
                ["__RequestVerificationToken"] = token,
            }));

        // Blocked deletes re-render the Delete view with the blocking message, not a redirect.
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var body = await postResponse.Content.ReadAsStringAsync();
        Assert.Contains("cannot be deleted", body);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(await context.Teachers.FindAsync(teacherId));
    }

    [Fact]
    public async Task DeleteStudent_WithActiveReservation_IsBlocked()
    {
        var studentId = await SeedStudentWithReservationAsync();
        var client = _factory.CreateClientAs("Administrator");

        // The student already has an active reservation, so the Delete confirmation GET renders
        // the blocked message directly (no form/token). Pull a valid antiforgery token+cookie
        // from the Create page instead, which always renders a plain form.
        var tokenPageResponse = await client.GetAsync("/AdminStudents/Create");
        Assert.Equal(HttpStatusCode.OK, tokenPageResponse.StatusCode);
        var token = ExtractAntiForgeryToken(await tokenPageResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            $"/AdminStudents/Delete/{studentId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = studentId.ToString(),
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var body = await postResponse.Content.ReadAsStringAsync();
        Assert.Contains("cannot be deleted", body);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(await context.Students.FindAsync(studentId));
    }

    /// <summary>
    /// Seeds a Teacher (with a backing ApplicationUser) that has one assigned Student, so the
    /// teacher delete action must be blocked (FR-006).
    /// </summary>
    private async Task<int> SeedTeacherWithAssignedStudentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"teacher-delete-block-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"teacher-delete-block-{Guid.NewGuid():N}@melodybooker.local",
            EmailConfirmed = true,
            DisplayName = "Blocked Teacher",
        };

        var createResult = await userManager.CreateAsync(user, "Test#12345");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, "Teacher");

        var teacher = new Teacher
        {
            ApplicationUserId = user.Id,
            FullName = "Blocked Teacher",
        };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        context.Students.Add(new Student
        {
            FullName = "Assigned Student",
            Class = 3,
            Instrument = Instrument.Guitar,
            TeacherId = teacher.Id,
        });
        await context.SaveChangesAsync();

        return teacher.Id;
    }

    /// <summary>
    /// Seeds a Student (with its own Teacher) that has one active Reservation in a Room, so the
    /// student delete action must be blocked (FR-006).
    /// </summary>
    private async Task<int> SeedStudentWithReservationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"teacher-for-student-block-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"teacher-for-student-block-{Guid.NewGuid():N}@melodybooker.local",
            EmailConfirmed = true,
            DisplayName = "Reservation Teacher",
        };

        var createResult = await userManager.CreateAsync(user, "Test#12345");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, "Teacher");

        var teacher = new Teacher
        {
            ApplicationUserId = user.Id,
            FullName = "Reservation Teacher",
        };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        var student = new Student
        {
            FullName = "Reserved Student",
            Class = 6,
            Instrument = Instrument.Violin,
            TeacherId = teacher.Id,
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        var room = new Room
        {
            Name = $"Room {Guid.NewGuid():N}",
            SpecialistInstrument = null,
        };
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        context.Reservations.Add(new Reservation
        {
            RoomId = room.Id,
            StudentId = student.Id,
            TeacherId = teacher.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = TimeSpan.FromHours(10),
            Duration = student.LessonDuration,
        });
        await context.SaveChangesAsync();

        return student.Id;
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
        {
            match = Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        }

        Assert.True(match.Success, "Could not find an antiforgery token in the Delete confirmation page.");
        return match.Groups[1].Value;
    }
}
