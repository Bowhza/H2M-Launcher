using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    public partial class ServerFilterViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private bool _showEmpty = true;

        [ObservableProperty]
        private bool _showFull = true;

        [ObservableProperty]
        private bool _showPrivate = true;

        [ObservableProperty]
        private int _maxPing = 999;

        [ObservableProperty]
        private int _minPlayers = 1;

        [ObservableProperty]
        private int _maxPlayers = 32;

        [ObservableProperty]
        private int _maxSlots = 32;

        [ObservableProperty]
        private int[] _maxSlotsItems = [6, 12, 18, 24, 32];

        [ObservableProperty]
        private string _filterText = "";

        public ICommand ApplyCommand { get; set; }

        public ICommand ResetCommand { get; set; }

        public ServerFilterViewModel()
        {
            ApplyCommand = new RelayCommand(() => CloseCommand.Execute(true), () => CloseCommand.CanExecute(true));
            ResetCommand = new RelayCommand(() =>
            {
                ShowEmpty = true;
                ShowFull = true;
                ShowPrivate = true;
                MaxPing = 999;
                MinPlayers = 1;
                MaxPlayers = 32;
                MaxSlots = 32;
            });
        }

        private bool ApplyTextFilter(ServerViewModel server)
        {
            if (string.IsNullOrEmpty(FilterText))
            {
                return true;
            }

            string lowerCaseFilter = FilterText.ToLower();

            if (!server.ToString().Contains(lowerCaseFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public bool ApplyFilter(ServerViewModel server)
        {
            if (!ApplyTextFilter(server))
            {
                return false;
            }

            if (server.ClientNum > MaxPlayers)
            {
                return false;
            }

            if (server.ClientNum < MinPlayers)
            {
                return false;
            }

            if (server.ClientNum == 0 && !ShowEmpty)
            {
                return false;
            }

            if (server.ClientNum == server.MaxClientNum && !ShowFull)
            {
                return false;
            }


            if (server.MaxClientNum > MaxSlots)
            {
                return false;
            }

            if (server.Ping > MaxPing)
            {
                return false;
            }

            if (server.IsPrivate && !ShowPrivate)
            {
                return false;
            }

            return true;
        }
    }
}
