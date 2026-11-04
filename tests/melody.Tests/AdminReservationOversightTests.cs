using System.Net;
using System.Text.RegularExpressions;
using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Integration tests proving the Administrator-only reservation oversight flow through
/// <c>AdminReservationsController</c>: the Index lists every reservation across teachers sorted
/// by Teacher then Day then Start time, editing a reservation's Room/Day/Time persists and
/// correctly excludes itself from the double-booking conflict check (the Phase 1 "exclude self"
/// guarantee), editing into a genuine conflict with a different reservation is hard-blocked,
/// editing into a specialist-instrument mismatch re-runs the same warn-then-confirm flow as
/// <see cref="SpecialistInstrumentWarningTests"/>, cancelling removes the row, and every action
/// remains forbidden/redirect-to-login for a non-Administrator caller (matching
/// <see cref="AuthorizationTests"/>'s existing role-boundary pattern).
/// </summary>
public class AdminReservationOversightTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public AdminReservationOversightTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Index_ListsReservationsAcrossTeachers_SortedByTeacherThenDayThenStart()
    {
        var (_, _, studentAId) = await SeedTeacherWithStudentAsync("Zed Teacher", classNumber: 3, Instrument.Guitar);
        var (_, _, studentBId) = await SeedTeacherWithStudentAsync("Ann Teacher", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync(specialistInstrument: null);

        await SeedReservationAsync(studentAId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(600));
        await SeedReservationAsync(studentBId, roomId, DayOfWeek.Tuesday, TimeSpan.FromMinutes(540));
        await SeedReservationAsync(studentBId, roomId, DayOfWeek.Tuesday, TimeSpan.FromMinutes(480));

        var client = _factory.CreateClientAs("Administrator");

        var response = await client.GetAsync("/AdminReservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        var annIndex = body.IndexOf("Ann Teacher", StringComparison.Ordinal);
        var zedIndex = body.IndexOf("Zed Teacher", StringComparison.Ordinal);
        Assert.True(annIndex >= 0 && zedIndex >= 0 && annIndex < zedIndex,
            "Expected Ann Teacher's rows (alphabetically first) to appear before Zed Teacher's.");

        var earlierStartIndex = body.IndexOf("08:00", StringComparison.Ordinal);
        var laterStartIndex = body.IndexOf("09:00", StringComparison.Ordinal);
        Assert.True(earlierStartIndex >= 0 && laterStartIndex >= 0 && earlierStartIndex < laterStartIndex,
            "Expected Ann Teacher's two Tuesday reservations to be ordered by start time.");
    }

    [Fact]
    public async Task Edit_ChangingRoomDayTime_PersistsNewValues()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Edit Success", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync(specialistInstrument: null);
        var otherRoomId = await SeedRoomAsync(specialistInstrument: null);
        var reservationId = await SeedReservationAsync(studentId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs("Administrator");
        var token = await GetAntiForgeryTokenAsync(client, reservationId);

        var response = await PostEditAsync(client, token, reservationId, otherRoomId, DayOfWeek.Wednesday, 600);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await context.Reservations.SingleAsync(r => r.Id == reservationId);
        Assert.Equal(otherRoomId, reservation.RoomId);
        Assert.Equal(DayOfWeek.Wednesday, reservation.DayOfWeek);
        Assert.Equal(TimeSpan.FromMinutes(600), reservation.StartTime);
    }

    [Fact]
    public async Task Edit_IntoConflictWithAnotherReservation_IsHardBlocked_NoChangePersisted()
    {
        var (_, _, studentAId) = await SeedTeacherWithStudentAsync("Teacher Conflict A", classNumber: 3, Instrument.Guitar);
        var (_, _, studentBId) = await SeedTeacherWithStudentAsync("Teacher Conflict B", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync(specialistInstrument: null);

        // Existing booking occupies Monday 9:00-9:20 (class 3 => 20-minute lesson).
        await SeedReservationAsync(studentAId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));
        var editingReservationId = await SeedReservationAsync(studentBId, roomId, DayOfWeek.Tuesday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs("Administrator");
        var token = await GetAntiForgeryTokenAsync(client, editingReservationId);

        // Move studentB's reservation onto the exact same room/day/time already held by studentA.
        var response = await PostEditAsync(client, token, editingReservationId, roomId, DayOfWeek.Monday, 540);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("already booked", body);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await context.Reservations.SingleAsync(r => r.Id == editingReservationId);
        Assert.Equal(DayOfWeek.Tuesday, reservation.DayOfWeek);
        Assert.Equal(TimeSpan.FromMinutes(540), reservation.StartTime);
    }

    [Fact]
    public async Task Edit_ResubmittingOwnCurrentValues_IsNotTreatedAsSelfConflict()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Self Resubmit", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync(specialistInstrument: null);
        var reservationId = await SeedReservationAsync(studentId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs("Administrator");
        var token = await GetAntiForgeryTokenAsync(client, reservationId);

        // No-op edit: resubmits the exact Room/Day/Time the reservation already has.
        var response = await PostEditAsync(client, token, reservationId, roomId, DayOfWeek.Monday, 540);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Reservations.CountAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Monday);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Edit_IntoSpecialistMismatch_WarnsThenSavesOnConfirmation()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Mismatch Edit", classNumber: 3, Instrument.Violin);
        var plainRoomId = await SeedRoomAsync(specialistInstrument: null);
        var pianoRoomId = await SeedRoomAsync(specialistInstrument: Instrument.Piano);
        var reservationId = await SeedReservationAsync(studentId, plainRoomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs("Administrator");
        var token = await GetAntiForgeryTokenAsync(client, reservationId);

        var unconfirmedResponse = await PostEditAsync(client, token, reservationId, pianoRoomId, DayOfWeek.Monday, 540);

        Assert.Equal(HttpStatusCode.OK, unconfirmedResponse.StatusCode);
        var unconfirmedBody = await unconfirmedResponse.Content.ReadAsStringAsync();
        Assert.Contains("alert-warning", unconfirmedBody);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reservation = await context.Reservations.SingleAsync(r => r.Id == reservationId);
            Assert.Equal(plainRoomId, reservation.RoomId);
        }

        var confirmedToken = await GetAntiForgeryTokenAsync(client, reservationId);
        var confirmedResponse = await PostEditAsync(
            client, confirmedToken, reservationId, pianoRoomId, DayOfWeek.Monday, 540,
            confirmed: true, confirmedRoomId: pianoRoomId, confirmedDayOfWeek: DayOfWeek.Monday, confirmedStartMinutes: 540);

        Assert.Equal(HttpStatusCode.Redirect, confirmedResponse.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedReservation = await verifyContext.Reservations.SingleAsync(r => r.Id == reservationId);
        Assert.Equal(pianoRoomId, savedReservation.RoomId);
        Assert.True(savedReservation.HadInstrumentMismatchWarning);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesReservationRow()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Cancel", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync(specialistInstrument: null);
        var reservationId = await SeedReservationAsync(studentId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs("Administrator");

        var getResponse = await client.GetAsync($"/AdminReservations/Delete/{reservationId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            $"/AdminReservations/Delete/{reservationId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = reservationId.ToString(),
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await context.Reservations.FindAsync(reservationId));
    }

    [Fact]
    public async Task Teacher_IsForbidden_OnEveryAdminReservationsAction()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Boundary Check", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync(specialistInstrument: null);
        var reservationId = await SeedReservationAsync(studentId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs("Teacher");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/AdminReservations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/AdminReservations/Edit/{reservationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            "/AdminReservations/Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = reservationId.ToString(),
                ["RoomId"] = roomId.ToString(),
                ["DayOfWeek"] = ((int)DayOfWeek.Monday).ToString(),
                ["StartMinutes"] = "540",
            }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/AdminReservations/Delete/{reservationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            $"/AdminReservations/Delete/{reservationId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = reservationId.ToString(),
            }))).StatusCode);
    }

    [Fact]
    public async Task Anonymous_RedirectsToLogin_OnEveryAdminReservationsAction()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher For Anonymous Oversight Check", classNumber: 3, Instrument.Guitar);
        var roomId = await SeedRoomAsync(specialistInstrument: null);
        var reservationId = await SeedReservationAsync(studentId, roomId, DayOfWeek.Monday, TimeSpan.FromMinutes(540));

        var client = _factory.CreateClientAs(null);

        var response = await client.GetAsync("/AdminReservations");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.ToString());
    }

    private async Task<string> GetAntiForgeryTokenAsync(HttpClient client, int reservationId)
    {
        var response = await client.GetAsync($"/AdminReservations/Edit/{reservationId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractAntiForgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostEditAsync(
        HttpClient client,
        string token,
        int id,
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
            ["Id"] = id.ToString(),
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

        return await client.PostAsync("/AdminReservations/Edit", new FormUrlEncodedContent(formValues));
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
            UserName = $"admin-oversight-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"admin-oversight-{Guid.NewGuid():N}@melodybooker.local",
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
}
