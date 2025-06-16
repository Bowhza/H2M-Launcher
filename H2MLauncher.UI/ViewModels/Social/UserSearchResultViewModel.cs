using CommunityToolkit.Mvvm.ComponentModel;

namespace H2MLauncher.UI.ViewModels;

public sealed partial class UserSearchResultViewModel : ObservableObject
{
    public required string Id { get; init; }

    public required string UserName { get; init; }

    public string? PlayerName { get; init; }

    [ObservableProperty]
    private bool _hasRequested = false;
}
