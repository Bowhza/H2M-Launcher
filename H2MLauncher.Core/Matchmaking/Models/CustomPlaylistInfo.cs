using System.Diagnostics.CodeAnalysis;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Matchmaking.Models
{
    /// <summary>
    /// Info about a custom playlist that is stored.
    /// </summary>
    public record CustomPlaylistInfo()
    {
        /// <summary>
        /// A unique identifier for this playlist.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Name of this playlist.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// The servers in this playlist.
        /// </summary>
        public required HashSet<ServerConnectionDetails> Servers { get; init; }

        /// <summary>
        /// When this playlist was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }


        [SetsRequiredMembers]
        public CustomPlaylistInfo(string name, IEnumerable<ServerConnectionDetails> servers) : this()
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            Servers = [.. servers];
            CreatedAt = DateTimeOffset.Now;
        }
    }
}
