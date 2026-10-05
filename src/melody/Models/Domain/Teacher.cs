namespace melody.Models.Domain;

/// <summary>
/// A teacher account holder. Each teacher is linked to an ApplicationUser for login and
/// owns a set of assigned students (FR-004, FR-005, FR-007).
/// </summary>
public class Teacher
{
    public int Id { get; set; }

    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser? ApplicationUser { get; set; }

    public string FullName { get; set; } = string.Empty;

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
