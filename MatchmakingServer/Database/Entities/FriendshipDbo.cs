namespace MatchmakingServer.Database.Entities;

public class FriendshipDbo
{
    public Guid FromUserId { get; set; }
    public UserDbo? FromUser { get; set; }

    public Guid ToUserId { get; set; }
    public UserDbo? ToUser { get; set; }

    public FriendshipStatus Status { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdateDate { get; set; }

    public UserDbo? GetUserById(Guid userId) => FromUserId == userId ? FromUser : ToUser;

    public UserDbo? GetFriendByUserId(Guid userId) => FromUserId == userId ? ToUser : FromUser;
}