using melody.Models.Domain;

namespace melody.Data;

/// <summary>
/// Seeds the fixed room inventory on startup. There is no Room CRUD in the PRD (no FR covers
/// room provisioning), so the school's rooms are a static, hard-coded set rather than
/// admin-managed data.
/// </summary>
public static class RoomSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        if (context.Rooms.Any())
        {
            return;
        }

        context.Rooms.AddRange(
            new Room { Name = "Room 1" },
            new Room { Name = "Room 2" },
            new Room { Name = "Room 3" },
            new Room { Name = "Piano Room", SpecialistInstrument = Instrument.Piano },
            new Room { Name = "Xylophone Room", SpecialistInstrument = Instrument.Xylophone },
            new Room { Name = "Drum Room", SpecialistInstrument = Instrument.Drums });

        await context.SaveChangesAsync();
    }
}
