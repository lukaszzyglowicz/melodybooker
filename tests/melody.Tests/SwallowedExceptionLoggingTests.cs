using System.Net;
using System.Text.RegularExpressions;
using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace melody.Tests;

/// <summary>
/// Proves Phase 1's logging actually fires: forces a real <see cref="DbUpdateException"/> via
/// <see cref="ThrowingApplicationDbContext"/> (through <see cref="FaultInjectingWebApplicationFactory"/>)
/// for each of the three controllers that catch it, and asserts a Warning-level log entry was
/// captured with the exception and the expected domain ids, while the existing user-facing
/// fallback behavior (generic error message / re-rendered view) stays unchanged.
/// </summary>
public class SwallowedExceptionLoggingTests : IClassFixture<FaultInjectingWebApplicationFactory>, IAsyncLifetime
{
    private readonly FaultInjectingWebApplicationFactory _factory;

    public SwallowedExceptionLoggingTests(FaultInjectingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Edit_WhenSaveFails_LogsWarningWithReservationContext()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Fault Edit", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync();
        var otherRoomId = await SeedRoomAsync();
        var reservationId = await SeedReservationAsync(studentId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs("Administrator");
        var token = await GetAntiForgeryTokenAsync(client, $"/AdminReservations/Edit/{reservationId}");

        ThrowingApplicationDbContext.ThrowOnNextSave = true;
        var response = await client.PostAsync(
            "/AdminReservations/Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = reservationId.ToString(),
                ["RoomId"] = otherRoomId.ToString(),
                ["DayOfWeek"] = ((int)DayOfWeek.Wednesday).ToString(),
                ["StartMinutes"] = "600",
                ["__RequestVerificationToken"] = token,
            }));

        // Existing fallback behavior is unchanged: re-rendered view with the generic message.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("already booked", body);

        var entry = Assert.Single(_factory.CapturedLogs.Entries, e => e.Category.Contains("AdminReservationsController"));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.Contains(reservationId.ToString(), entry.Message);
        Assert.Contains(otherRoomId.ToString(), entry.Message);
    }

    [Fact]
    public async Task Create_WhenSaveFails_LogsWarningWithReservationContext()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Fault Create", classNumber: 6, Instrument.Guitar);
        var roomId = await SeedRoomAsync();

        var client = _factory.CreateClientAs("Teacher", teacherUserId);
        var token = await GetAntiForgeryTokenAsync(client, $"/TeacherReservations/Create?studentId={studentId}");

        ThrowingApplicationDbContext.ThrowOnNextSave = true;
        var response = await client.PostAsync(
            "/TeacherReservations/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["StudentId"] = studentId.ToString(),
                ["RoomId"] = roomId.ToString(),
                ["DayOfWeek"] = ((int)DayOfWeek.Monday).ToString(),
                ["StartMinutes"] = "540",
                ["__RequestVerificationToken"] = token,
            }));

        // Existing fallback behavior is unchanged: re-rendered view with the generic message.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("already booked", body);

        var entry = Assert.Single(_factory.CapturedLogs.Entries, e => e.Category.Contains("TeacherReservationsController"));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.Contains(studentId.ToString(), entry.Message);
        Assert.Contains(roomId.ToString(), entry.Message);

        // No reservation row was actually persisted; the injected SaveChangesAsync failure
        // prevented the insert from committing.
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await context.Reservations.CountAsync(r => r.StudentId == studentId));
    }

    [Fact]
    public async Task Create_WhenSaveFails_LogsWarningWithApplicationUserId()
    {
        var client = _factory.CreateClientAs("Administrator");
        var token = await GetAntiForgeryTokenAsync(client, "/AdminTeachers/Create");
        var email = $"fault-teacher-{Guid.NewGuid():N}@melodybooker.local";

        // UserManager.CreateAsync and AddToRoleAsync each save the same ApplicationDbContext
        // before the controller's own SaveChangesAsync inside its try block, so the fault must
        // skip those two saves to land on the one under test.
        ThrowingApplicationDbContext.Arm(skipSaves: 2);
        var response = await client.PostAsync(
            "/AdminTeachers/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"] = "Fault Teacher",
                ["Email"] = email,
                ["Password"] = "Test#12345",
                ["ConfirmPassword"] = "Test#12345",
                ["__RequestVerificationToken"] = token,
            }));

        // Existing fallback behavior is unchanged: re-rendered Create view with the generic
        // message, and no Teacher profile left behind without its login account (FR compensation).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Something went wrong while creating the teacher profile", body);

        var entry = Assert.Single(_factory.CapturedLogs.Entries, e => e.Category.Contains("AdminTeachersController"));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.NotNull(entry.Exception);

        var match = Regex.Match(entry.Message, @"ApplicationUser (\S+);");
        Assert.True(match.Success, $"Expected the log message to contain an ApplicationUser id: {entry.Message}");
        var loggedUserId = match.Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(loggedUserId));

        // The compensating delete (triggered by the same catch block) rolled the account back,
        // proving the logged id corresponds to the account actually created for this request.
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByIdAsync(loggedUserId));
        Assert.Null(await userManager.FindByEmailAsync(email));
    }

    private async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string getUrl)
    {
        var response = await client.GetAsync(getUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractAntiForgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
        {
            match = Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        }

        Assert.True(match.Success, "Could not find an antiforgery token in the page.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Seeds a new Teacher (with a backing ApplicationUser) and one assigned Student with the
    /// given <see cref="Instrument"/>. Returns the teacher's ApplicationUser.Id, Teacher.Id, and
    /// the new Student.Id.
    /// </summary>
    private async Task<(string ApplicationUserId, int TeacherId, int StudentId)> SeedTeacherWithStudentAsync(
        string teacherName, int classNumber, Instrument instrument)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"fault-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"fault-{Guid.NewGuid():N}@melodybooker.local",
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

        var student = new Student
        {
            FullName = $"{teacherName}'s Student",
            Class = classNumber,
            Instrument = instrument,
            TeacherId = teacher.Id,
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return (user.Id, teacher.Id, student.Id);
    }

    private async Task<int> SeedRoomAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var room = new Room
        {
            Name = $"Room {Guid.NewGuid():N}",
            SpecialistInstrument = null,
        };
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        return room.Id;
    }

    private async Task<int> SeedReservationAsync(int studentId, int roomId, DayOfWeek dayOfWeek, TimeSpan startTime)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var student = await context.Students.SingleAsync(s => s.Id == studentId);

        var reservation = new Reservation
        {
            RoomId = roomId,
            StudentId = studentId,
            TeacherId = student.TeacherId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            Duration = student.LessonDuration,
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        return reservation.Id;
    }
}
