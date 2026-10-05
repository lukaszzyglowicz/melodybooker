namespace melody.Models.Domain;

/// <summary>
/// Instruments a student can play. Piano, Xylophone, and Drums are the non-portable
/// specialist instruments that require a matching specialist room (FR-011).
/// </summary>
public enum Instrument
{
    Piano,
    Xylophone,
    Drums,
    Violin,
    Viola,
    Trumpet,
    Guitar,
    Flute,
    Other
}

public static class InstrumentExtensions
{
    private static readonly HashSet<Instrument> SpecialistInstruments =
    [
        Instrument.Piano,
        Instrument.Xylophone,
        Instrument.Drums
    ];

    /// <summary>
    /// True for non-portable specialist instruments (piano, xylophone, drums) per FR-011/FR-012.
    /// </summary>
    public static bool IsSpecialist(this Instrument instrument) => SpecialistInstruments.Contains(instrument);
}
