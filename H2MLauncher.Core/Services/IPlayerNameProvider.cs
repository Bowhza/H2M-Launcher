
namespace H2MLauncher.Core.Services
{
    public interface IPlayerNameProvider
    {
        string PlayerName { get; }

        event Action<string, string>? PlayerNameChanged;
    }
}