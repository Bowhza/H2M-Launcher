using System.Diagnostics;

namespace H2MLauncher.Core.Game.Models
{
    public record DetectedGame(Process Process, string FileName, string GameDir, FileVersionInfo Version);
}
