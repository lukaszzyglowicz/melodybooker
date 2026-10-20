using System.Net;
using System.Text.RegularExpressions;
using melody.Data;
using melody.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Integration tests proving the end-to-end booking flow through
/// <c>TeacherReservationsController.Create</c>: a successful booking carries the student's
/// derived lesson duration (FR-009), the same room/day/time slot is always hard-blocked against
/// double-booking (FR-008, FR-010) whether the conflict is an exact repeat or a partial overlap,
/// a back-to-back booking (no gap) is allowed, a teacher cannot book for a student assigned to
/// another teacher, and non-Teacher roles remain forbidden on the route (regression against
/// <see cref="AuthorizationTests"/>'s existing role-boundary coverage).
/// </summary>
public class TeacherBooksRoomTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public TeacherBooksRoomTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_SuccessfulBooking_CreatesReservationWithStudentsLessonDuration()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Success", classNumber: 6);
        var roomId = await SeedRoomAsync();
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        var token = await GetAntiForgeryTokenAsync(client, studentId);

        var response = await PostCreateAsync(client, token, studentId, roomId, DayOfWeek.Monday, 540);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await context.Reservations.SingleAsync(r => r.StudentId == studentId);
        Assert.Equal(TimeSpan.FromMinutes(40), reservation.Duration);
        Assert.Equal(TimeSpan.FromMinutes(540), reservation.StartTime);
    }

    [Fact]
    public async Task Create_ExactSameRoomDayTimeTwice_SecondAttemptIsRejected_NoSecondRowCreated()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Duplicate", classNumber: 3);
        var roomId = await SeedRoomAsync();
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        var firstToken = await GetAntiForgeryTokenAsync(client, studentId);
        var firstResponse = await PostCreateAsync(client, firstToken, studentId, roomId, DayOfWeek.Tuesday, 540);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondToken = await GetAntiForgeryTokenAsync(client, studentId);
        var secondResponse = await PostCreateAsync(client, secondToken, studentId, roomId, DayOfWeek.Tuesday, 540);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var body = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("already booked", body);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Reservations.CountAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Tuesday);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Create_OverlappingDifferentStartTime_SameRoomAndDay_IsRejected()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync("Teacher Overlap", classNumber: 7);
        var roomId = await SeedRoomAsync();
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        // Existing booking occupies 9:00-9:40 (class 7 => 40-minute lesson).
        var firstToken = await GetAntiForgeryTokenAsync(client, studentId);
        var firstResponse = await PostCreateAsync(client, firstToken, studentId, roomId, DayOfWeek.Wednesday, 540);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        // A second booking starting at 9:20 overlaps the 9:00-9:40 slot without matching the
        // start time exactly.
        var secondToken = await GetAntiForgeryTokenAsync(client, studentId);
        var overlappingResponse = await PostCreateAsync(client, secondToken, studentId, roomId, DayOfWeek.Wednesday, 560);

        Assert.Equal(HttpStatusCode.OK, overlappingResponse.StatusCode);
        var body = await overlappingResponse.Content.ReadAsStringAsync();
        Assert.Contains("already booked", body);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Reservations.CountAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Wednesday);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Create_BackToBackStartTime_NoGapFromExistingBooking_Succeeds()
    {
        var (teacherUserId, _, studentId) = await SeedTeacherWithStudentAsync("Teacher BackToBack", classNumber: 7);
        var roomId = await SeedRoomAsync();
        var client = _factory.CreateClientAs("Teacher", teacherUserId);

        // Existing booking occupies 9:00-9:40 (class 7 => 40-minute lesson).
        var firstToken = await GetAntiForgeryTokenAsync(client, studentId);
        var firstResponse = await PostCreateAsync(client, firstToken, studentId, roomId, DayOfWeek.Thursday, 540);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        // The new booking starts exactly when the existing one ends (9:40 = 580 minutes), a raw
        // StartMinutes value not restricted to the Create view's 15-minute dropdown increments.
        var secondToken = await GetAntiForgeryTokenAsync(client, studentId);
        var backToBackResponse = await PostCreateAsync(client, secondToken, studentId, roomId, DayOfWeek.Thursday, 580);

        Assert.Equal(HttpStatusCode.Redirect, backToBackResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Reservations.CountAsync(r => r.RoomId == roomId && r.DayOfWeek == DayOfWeek.Thursday);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Create_Get_ForAnotherTeachersStudent_ReturnsNotFound()
    {
        var (_, _, studentAId) = await SeedTeacherWithStudentAsync("Teacher A Owner", classNumber: 3);
        var (teacherBUserId, _, _) = await SeedTeacherWithStudentAsync("Teacher B Other", classNumber: 3);
        var client = _factory.CreateClientAs("Teacher", teacherBUserId);

        var response = await client.GetAsync($"/TeacherReservations/Create?studentId={studentAId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Post_ForAnotherTeachersStudent_ReturnsNotFound()
    {
        var (_, _, studentAId) = await SeedTeacherWithStudentAsync("Teacher A Owner Post", classNumber: 3);
        var (teacherBUserId, _, studentBId) = await SeedTeacherWithStudentAsync("Teacher B Other Post", classNumber: 3);
        var roomId = await SeedRoomAsync();
        var client = _factory.CreateClientAs("Teacher", teacherBUserId);

        // Valid antiforgery token comes from a page this teacher can legitimately reach (their
        // own student's Create page); the mismatch under test is the posted StudentId.
        var token = await GetAntiForgeryTokenAsync(client, studentBId);

        var response = await PostCreateAsync(client, token, studentAId, roomId, DayOfWeek.Friday, 540);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Administrator_IsForbidden()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher For Admin Check", classNumber: 3);
        var client = _factory.CreateClientAs("Administrator");

        var response = await client.GetAsync($"/TeacherReservations/Create?studentId={studentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Anonymous_RedirectsToLogin()
    {
        var (_, _, studentId) = await SeedTeacherWithStudentAsync("Teacher For Anonymous Check", classNumber: 3);
        var client = _factory.CreateClientAs(null);

        var response = await client.GetAsync($"/TeacherReservations/Create?studentId={studentId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.ToString());
    }

    private async Task<string> GetAntiForgeryTokenAsync(HttpClient client, int studentId)
    {
        var response = await client.GetAsync($"/TeacherReservations/Create?studentId={studentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractAntiForgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client, string token, int studentId, int roomId, DayOfWeek dayOfWeek, int startMinutes)
    {
        return await client.PostAsync(
            "/TeacherReservations/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["StudentId"] = studentId.ToString(),
                ["RoomId"] = roomId.ToString(),
                ["DayOfWeek"] = ((int)dayOfWeek).ToString(),
                ["StartMinutes"] = startMinutes.ToString(),
                ["__RequestVerificationToken"] = token,
            }));
    }

    /// <summary>
    /// Seeds a new Teacher (with a backing ApplicationUser) and one assigned Student. Returns the
    /// teacher's ApplicationUser.Id, Teacher.Id, and the new Student.Id.
    /// </summary>
    private async Task<(string ApplicationUserId, int TeacherId, int StudentId)> SeedTeacherWithStudentAsync(
        string studentName, int classNumber)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"teacher-books-room-{Guid.NewGuid():N}@melodybooker.local",
            Email = $"teacher-books-room-{Guid.NewGuid():N}@melodybooker.local",
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
            Instrument = Instrument.Guitar,
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
