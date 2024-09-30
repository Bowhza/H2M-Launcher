namespace H2MLauncher.Core.Models;

public interface IFullServerConnectionDetails : IServerConnectionDetails
{
    public string? Password { get; }
}
