using System.ComponentModel.DataAnnotations;

namespace melody.Models.Domain;

/// <summary>
/// A rehearsal room. SpecialistInstrument is set when the room has a non-portable
/// specialist instrument (piano/xylophone/drums); null means an ordinary room.
/// </summary>
public class Room
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public Instrument? SpecialistInstrument { get; set; }

    public bool IsSpecialistRoom => SpecialistInstrument.HasValue;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
