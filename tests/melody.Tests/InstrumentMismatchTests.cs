using melody.Models.Domain;

namespace melody.Tests;

/// <summary>
/// Unit tests for <see cref="Reservation.IsInstrumentMismatch"/>, the FR-011 soft-warning rule
/// for specialist rooms booked against a student's instrument. Pure unit tests: no DB/host required.
/// </summary>
public class InstrumentMismatchTests
{
    [Fact]
    public void IsInstrumentMismatch_SpecialistRoomMatchingInstrument_ReturnsFalse()
    {
        var room = new Room { SpecialistInstrument = Instrument.Piano };
        var student = new Student { Instrument = Instrument.Piano };

        Assert.False(Reservation.IsInstrumentMismatch(room, student));
    }

    [Fact]
    public void IsInstrumentMismatch_SpecialistRoomDifferentSpecialistInstrument_ReturnsTrue()
    {
        var room = new Room { SpecialistInstrument = Instrument.Piano };
        var student = new Student { Instrument = Instrument.Drums };

        Assert.True(Reservation.IsInstrumentMismatch(room, student));
    }

    [Fact]
    public void IsInstrumentMismatch_SpecialistRoomNonSpecialistInstrument_ReturnsTrue()
    {
        var room = new Room { SpecialistInstrument = Instrument.Piano };
        var student = new Student { Instrument = Instrument.Violin };

        Assert.True(Reservation.IsInstrumentMismatch(room, student));
    }

    [Fact]
    public void IsInstrumentMismatch_SpecialistRoomOtherInstrument_ReturnsTrue()
    {
        var room = new Room { SpecialistInstrument = Instrument.Xylophone };
        var student = new Student { Instrument = Instrument.Other };

        Assert.True(Reservation.IsInstrumentMismatch(room, student));
    }

    [Fact]
    public void IsInstrumentMismatch_NonSpecialistRoom_ReturnsFalse()
    {
        var room = new Room { SpecialistInstrument = null };
        var student = new Student { Instrument = Instrument.Violin };

        Assert.False(Reservation.IsInstrumentMismatch(room, student));
    }
}
