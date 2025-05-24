using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MatchmakingServer.Database.Entities;

public class UserDbo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public List<UserKeyDbo>? Keys { get; set; }
}
