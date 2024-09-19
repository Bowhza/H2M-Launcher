using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.ViewModels;

public partial class SocialsViewModel : ObservableObject
{
    [RelayCommand]
    public void JoinDiscord()
    {
        Process.Start(new ProcessStartInfo(Constants.DISCORD_INVITE_LINK) { UseShellExecute = true });
    }

    [RelayCommand]
    public void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo(Constants.GITHUB_REPO) { UseShellExecute = true });
    }
}
