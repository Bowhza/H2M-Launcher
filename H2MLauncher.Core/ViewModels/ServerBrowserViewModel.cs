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
        private List<RaidMaxServer> servers = [];
        private int totalServers = 0;
        private int totalPlayers = 0;
        private string filter = "";

        public string Filter
        {
            get => filter;
            set => SetProperty(ref filter, value);
        }

        public IAsyncRelayCommand RefreshServersCommand { get; }
        public ObservableCollection<RaidMaxServer> Servers { get; set; } = [];

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
            servers = await _raidMaxService.GetServerInfosAsync(CancellationToken.None);
            Servers.Clear();
            servers.ForEach(s => Servers.Add(s));
            TotalServers = servers.Count;
            TotalPlayers = servers.Sum(server => server.ClientNum);

            OnPropertyChanged(nameof(Servers));
        }
    }
}
