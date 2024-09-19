namespace H2MLauncher.Core.Game
{
    public interface IPlayerNameProvider
    {
        string PlayerName { get; }

        event Action<string, string>? PlayerNameChanged;
    }
}