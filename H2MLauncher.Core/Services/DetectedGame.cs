using System.Diagnostics;

namespace H2MLauncher.Core.Services
{
    public record DetectedGame(Process Process, string FileName, string GameDir, FileVersionInfo Version);
}
