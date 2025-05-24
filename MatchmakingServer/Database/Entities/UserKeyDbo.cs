using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingServer.Database.Entities;

public class UserKeyDbo
{
    /// <summary>
    /// Unique ID for this specific public key entry
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign Key to the Users table.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the Users table.
    /// </summary>
    public UserDbo? User { get; set; }

    /// <summary>
    /// The full Base64 encoded SPKI public key.
    /// </summary>
    public required string PublicKeySPKI { get; set; }

    /// <summary>
    /// The SHA256 hash of <see cref="PublicKeySPKI">.
    /// </summary>
    public required string PublicKeyHash { get; set; }

    /// <summary>
    /// True if the key is currently authorized to log in. Used for revocation.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedDate { get; set; }

    public DateTime? RevokedDate { get; set; }
}
