using melody.Models.Domain;

namespace melody.Tests;

/// <summary>
/// Unit tests for <see cref="Reservation.Overlaps"/>, the half-open interval overlap rule
/// FR-010's double-booking guarantee rests on. Pure unit tests: no DB/host required.
/// </summary>
public class ReservationOverlapTests
{
    [Fact]
    public void Overlaps_ExactMatchRanges_ReturnsTrue()
    {
        var startA = TimeSpan.FromHours(9);
        var endA = TimeSpan.FromHours(10);

        Assert.True(Reservation.Overlaps(startA, endA, startA, endA));
    }

    [Fact]
    public void Overlaps_OverlappingDifferentStart_ReturnsTrue()
    {
        var startA = TimeSpan.FromHours(9);
        var endA = TimeSpan.FromHours(10);
        var startB = TimeSpan.FromMinutes(9 * 60 + 30);
        var endB = TimeSpan.FromHours(11);

        Assert.True(Reservation.Overlaps(startA, endA, startB, endB));
    }

    [Fact]
    public void Overlaps_BackToBackRanges_ReturnsFalse()
    {
        var startA = TimeSpan.FromHours(9);
        var endA = TimeSpan.FromHours(10);
        var startB = TimeSpan.FromHours(10);
        var endB = TimeSpan.FromHours(11);

        Assert.False(Reservation.Overlaps(startA, endA, startB, endB));
    }

    [Fact]
    public void Overlaps_DisjointRanges_ReturnsFalse()
    {
        var startA = TimeSpan.FromHours(9);
        var endA = TimeSpan.FromHours(10);
        var startB = TimeSpan.FromHours(11);
        var endB = TimeSpan.FromHours(12);

        Assert.False(Reservation.Overlaps(startA, endA, startB, endB));
    }

    [Fact]
    public void Overlaps_OneRangeFullyContainsAnother_ReturnsTrue()
    {
        var startA = TimeSpan.FromHours(9);
        var endA = TimeSpan.FromHours(12);
        var startB = TimeSpan.FromHours(10);
        var endB = TimeSpan.FromHours(11);

        Assert.True(Reservation.Overlaps(startA, endA, startB, endB));
    }
}
