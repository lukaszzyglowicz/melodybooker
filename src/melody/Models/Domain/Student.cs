using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace melody.Models.Domain;

/// <summary>
/// A student has no login (see Access Control). Class (1-8) drives lesson duration (FR-009):
/// 20 minutes for classes 1-4, 40 minutes for classes 5-8.
/// </summary>
public class Student
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Range(1, 8)]
    public int Class { get; set; }

    public Instrument Instrument { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    /// <summary>
    /// Lesson duration derived from class, never user input (FR-009).
    /// </summary>
    [NotMapped]
    public TimeSpan LessonDuration => Class <= 4 ? TimeSpan.FromMinutes(20) : TimeSpan.FromMinutes(40);
}
