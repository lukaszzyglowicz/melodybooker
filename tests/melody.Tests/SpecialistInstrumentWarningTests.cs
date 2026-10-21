using System.Net;
using System.Text.RegularExpressions;
using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Integration tests proving the end-to-end warn → confirm → save flow for specialist-instrument
/// room mismatches (FR-011) through <c>TeacherReservationsController.Create</c>: an unconfirmed
/// mismatch is a soft warning that re-renders the form without saving, a confirmation pinned to
/// the exact Room/Day/Time combination saves with <c>HadInstrumentMismatchWarning == true</c>, a
/// matching-instrument booking and a non-specialist room never warn, and a confirmation pinned to
/// a different slot than the one actually submitted re-warns instead of saving (stale
/// confirmations never carry over).
/// </summary>
public class SpecialistInstrumentWarningTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public SpecialistInstrumentWarningTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_InstrumentMismatchNotConfirmed_DoesNotSaveAndShowsWarning()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync(
            "Teacher Mismatch Unconfirmed", classNumber: 3, Instrument.Violin);
        var roomId = await SeedRoomAsync(Instrument.Piano);
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        var token = await GetAntiForgeryTokenAsync(client, studentId);
        var response = await PostCreateAsync(client, token, studentId, roomId, DayOfWeek.Monday, 540);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("alert-warning", body);
        Assert.Contains("Teacher Mismatch Unconfirmed", body);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Reservations.CountAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Monday);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Create_InstrumentMismatchConfirmed_SavesWithFlagTrue()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync(
            "Teacher Mismatch Confirmed", classNumber: 3, Instrument.Violin);
        var roomId = await SeedRoomAsync(Instrument.Piano);
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        var token = await GetAntiForgeryTokenAsync(client, studentId);
        var response = await PostCreateAsync(
            client, token, studentId, roomId, DayOfWeek.Tuesday, 540,
            confirmed: true, confirmedRoomId: roomId, confirmedDayOfWeek: DayOfWeek.Tuesday, confirmedStartMinutes: 540);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await context.Reservations.SingleAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Tuesday);
        Assert.True(reservation.HadInstrumentMismatchWarning);
    }

    [Fact]
    public async Task Create_MatchingInstrument_SavesImmediatelyWithFlagFalse()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync(
            "Teacher Matching Instrument", classNumber: 3, Instrument.Piano);
        var roomId = await SeedRoomAsync(Instrument.Piano);
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        var token = await GetAntiForgeryTokenAsync(client, studentId);
        var response = await PostCreateAsync(client, token, studentId, roomId, DayOfWeek.Wednesday, 540);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await context.Reservations.SingleAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Wednesday);
        Assert.False(reservation.HadInstrumentMismatchWarning);
    }

    [Fact]
    public async Task Create_NonSpecialistRoom_NeverWarnsRegardlessOfInstrument()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync(
            "Teacher Non Specialist Room", classNumber: 3, Instrument.Violin);
        var roomId = await SeedRoomAsync(specialistInstrument: null);
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        var token = await GetAntiForgeryTokenAsync(client, studentId);
        var response = await PostCreateAsync(client, token, studentId, roomId, DayOfWeek.Thursday, 540);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await context.Reservations.SingleAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Thursday);
        Assert.False(reservation.HadInstrumentMismatchWarning);
    }

    [Fact]
    public async Task Create_ConfirmedForDifferentSlot_ReWarnsInsteadOfSaving()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync(
            "Teacher Stale Confirmation", classNumber: 3, Instrument.Violin);
        var roomId = await SeedRoomAsync(Instrument.Piano);
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        // Confirmation is pinned to Monday, but the teacher actually submits Friday: the stale
        // confirmation must not carry over to a different slot.
        var token = await GetAntiForgeryTokenAsync(client, studentId);
        var response = await PostCreateAsync(
            client, token, studentId, roomId, DayOfWeek.Friday, 540,
            confirmed: true, confirmedRoomId: roomId, confirmedDayOfWeek: DayOfWeek.Monday, confirmedStartMinutes: 540);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("alert-warning", body);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Reservations.CountAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Friday);
        Assert.Equal(0, count);
    }

    private async Task<string> GetAntiForgeryTokenAsync(HttpClient client, int studentId)
    {
        var response = await client.GetAsync($"/TeacherReservations/Create?studentId={studentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractAntiForgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        string token,
        int studentId,
        int roomId,
        DayOfWeek dayOfWeek,
        int startMinutes,
        bool? confirmed = null,
        int? confirmedRoomId = null,
        DayOfWeek? confirmedDayOfWeek = null,
        int? confirmedStartMinutes = null)
    {
        var formValues = new Dictionary<string, string>
        {
            ["StudentId"] = studentId.ToString(),
            ["RoomId"] = roomId.ToString(),
            ["DayOfWeek"] = ((int)dayOfWeek).ToString(),
            ["StartMinutes"] = startMinutes.ToString(),
            ["__RequestVerificationToken"] = token,
        };

        if (confirmed.HasValue)
        {
            formValues["Confirmed"] = confirmed.Value.ToString();
            formValues["ConfirmedRoomId"] = confirmedRoomId!.Value.ToString();
            formValues["ConfirmedDayOfWeek"] = ((int)confirmedDayOfWeek!.Value).ToString();
            formValues["ConfirmedStartMinutes"] = confirmedStartMinutes!.Value.ToString();
        }

        return await client.PostAsync("/TeacherReservations/Create", new FormUrlEncodedContent(formValues));
    }

    /// <summary>
    /// Seeds a new Teacher (with a backing ApplicationUser) and one assigned Student with the
    /// given <see cref="Instrument"/>. Returns the teacher's ApplicationUser.Id, Teacher.Id, and
    /// the new Student.Id.
    /// </summary>
    private async Task<(string ApplicationUserId, int TeacherId, int StudentId)> SeedTeacherWithStudentAsync(
        string studentName, int classNumber, Instrument instrument)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"specialist-warning-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"specialist-warning-{Guid.NewGuid():N}@melodybooker.local",
            EmailConfirmed = true,
            DisplayName = studentName,
        };

        var createResult = await userManager.CreateAsync(user, "Test#12345");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, "Teacher");

        var teacher = new Teacher
        {
            ApplicationUserId = user.Id,
            FullName = studentName,
        };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        var student = new Student
        {
            FullName = studentName,
            Class = classNumber,
            Instrument = instrument,
            TeacherId = teacher.Id,
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return (user.Id, teacher.Id, student.Id);
    }

    private async Task<int> SeedRoomAsync(Instrument? specialistInstrument)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var room = new Room
        {
            Name = $"Room {Guid.NewGuid():N}",
            SpecialistInstrument = specialistInstrument,
        };
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        return room.Id;
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
        {
            match = Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        }

        Assert.True(match.Success, "Could not find an antiforgery token in the Create page.");
        return match.Groups[1].Value;
    }
}
