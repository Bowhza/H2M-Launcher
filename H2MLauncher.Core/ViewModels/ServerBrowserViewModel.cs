using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using System.Collections.ObjectModel;

namespace H2MLauncher.Core.ViewModels
{
    public class ServerBrowserViewModel : ObservableObject
    {
        private readonly RaidMaxService _raidMaxService;
        private int totalServers = 0;
        private int totalPlayers = 0;

        public IAsyncRelayCommand RefreshServersCommand { get; }
        public ObservableCollection<RaidMaxServer> Servers { get; private set; } = [];

        public int TotalServers
        {
            get => totalServers;
            private set => SetProperty(ref totalServers, value);
        }
        public int TotalPlayers
        {
            get => totalPlayers;
            private set => SetProperty(ref totalPlayers, value);
        }

        public ServerBrowserViewModel(RaidMaxService raidMaxService)
        {
            _raidMaxService = raidMaxService ?? throw new ArgumentNullException(nameof(raidMaxService));

            RefreshServersCommand = new AsyncRelayCommand(LoadServersAsync);
        }

        private async Task LoadServersAsync()
        {
            Servers = new ObservableCollection<RaidMaxServer>(await _raidMaxService.GetServerInfosAsync(CancellationToken.None));
            TotalServers = Servers.Count;
            TotalPlayers = Servers.Sum(server => server.ClientNum);

            OnPropertyChanged(nameof(Servers));
            OnPropertyChanged(nameof(TotalServers));
            OnPropertyChanged(nameof(TotalPlayers));
        }
    }
}
