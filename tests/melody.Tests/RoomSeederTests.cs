using melody.Data;
using melody.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Integration tests proving <see cref="RoomSeeder"/> runs correctly against the real host:
/// the fixed 6-room inventory is seeded exactly once, with the right specialist/general split,
/// and re-running the seeder does not duplicate rows (idempotency).
/// </summary>
public class RoomSeederTests : IClassFixture<MelodyWebApplicationFactory>, IAsyncLifetime
{
    private readonly MelodyWebApplicationFactory _factory;

    public RoomSeederTests(MelodyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureSeededAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedAsync_SeedsSixRoomsWithExpectedSpecialistSplit()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var rooms = await context.Rooms.ToListAsync();

        Assert.Equal(6, rooms.Count);
        Assert.Single(rooms, r => r.SpecialistInstrument == Instrument.Piano);
        Assert.Single(rooms, r => r.SpecialistInstrument == Instrument.Xylophone);
        Assert.Single(rooms, r => r.SpecialistInstrument == Instrument.Drums);
        Assert.Equal(3, rooms.Count(r => r.SpecialistInstrument == null));
    }

    [Fact]
    public async Task SeedAsync_CalledAgain_DoesNotDuplicateRooms()
    {
        using var scope = _factory.Services.CreateScope();

        await RoomSeeder.SeedAsync(scope.ServiceProvider);

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roomCount = await context.Rooms.CountAsync();

        Assert.Equal(6, roomCount);
    }
}
