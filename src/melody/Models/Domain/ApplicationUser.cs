using Microsoft.AspNetCore.Identity;

namespace melody.Models.Domain;

/// <summary>
/// Identity user for the two supported roles: Administrator and Teacher (see Access Control in the PRD).
/// Students have no login/account.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Navigation to the Teacher profile when this user has the Teacher role. Null for Administrator users.
    /// </summary>
    public Teacher? Teacher { get; set; }
}
