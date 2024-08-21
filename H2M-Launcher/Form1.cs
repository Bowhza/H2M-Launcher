using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace H2M_Launcher
{
    public partial class Form1 : Form
    {
        //Dictionary to store server pings
        private static readonly ConcurrentDictionary<string, string> serverPings = new ConcurrentDictionary<string, string>();
        //CancellationTokenSource to cancel the server fetch
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        // List to store the original items in the list view
        private List<ListViewItem> _originalItems = new List<ListViewItem>();
        //Delegate to add items to the list view
        delegate void AddItemToListViewCallback(ListViewItem listViewItem);

        //ListView sorting variables
        private int _sortedColumnIndex = -1;
        private bool _sortAscending = true;

        //Windows API functions to send input to a window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        //Windows API constants
        internal const int WM_NCLBUTTONDOWN = 0xA1;
        internal const int HT_CAPTION = 0x2;
        internal const int WM_CHAR = 0x0102; // Message code for sending a character
        internal const int WM_KEYDOWN = 0x0100; // Message code for key down
        internal const int WM_KEYUP = 0x0101;   // Message code for key up

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            //Set the active control to null
            ActiveControl = null;

            //Check if the left mouse button is pressed
            if (e.Button == MouseButtons.Left)
            {
                //Release the capture and send the message to move the form
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        public Form1()
        {
            InitializeComponent();
            KeyPreview = true;
            KeyDown += Form1_KeyPress!;
            Focus();
            FetchServersAsync();
        }

        private void Form1_KeyPress(object sender, KeyEventArgs e)
        {
            if (ActiveControl is TextBox)
                return;

            if (e.KeyCode == Keys.L)
            {
                LaunchH2MMod();
            }
            else if (e.KeyCode == Keys.R)
            {
                FetchServersAsync();
            }
            else if (e.KeyCode == Keys.S)
            {
                Servers.SaveServerListAsync();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        private void LaunchH2MMod()
        {
            try
            {
                // Check if the process is already running
                var runningProcess = Process.GetProcessesByName("h2m-mod").FirstOrDefault();

                if (runningProcess != null)
                {
                    MessageBox.Show("h2m-mod.exe is already running.", Text);
                    return;
                }

                // Proceed to launch the process if it's not running
                if (File.Exists("./h2m-mod.exe"))
                {
                    Process.Start("./h2m-mod.exe");
                }
                else
                {
                    MessageBox.Show("h2m-mod.exe not found!", Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error launching h2m-mod.exe: " + ex.Message, Text);
            }
        }

        private static void SendConnectCommand(string command)
        {
            IntPtr h2mModWindow = FindH2MModWindow();

            if (h2mModWindow != IntPtr.Zero)
            {
                //Open In Game Terminal Window
                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)Keys.Oemtilde, IntPtr.Zero);

                //Send the "connect" command to the terminal window
                foreach (char c in command)
                {
                    SendMessage(h2mModWindow, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                }

                //Sleep for 1ms to allow the command to be processed
                Thread.Sleep(1);

                //Simulate pressing the Enter key
                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)Keys.Enter, IntPtr.Zero);
                SendMessage(h2mModWindow, WM_KEYUP, (IntPtr)Keys.Enter, IntPtr.Zero);

                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)Keys.Oemtilde, IntPtr.Zero);
            }
            else
            {
                MessageBox.Show("Could not find the h2m-mod terminal window.");
            }
        }

        private static IntPtr FindH2MModWindow()
        {
            foreach (Process proc in Process.GetProcesses())
            {
                // Find the process by name and check if the main window title contains "h2m-mod"
                if (proc.MainWindowTitle.Contains("h2m-mod", StringComparison.OrdinalIgnoreCase))
                {
                    return proc.MainWindowHandle;
                }
            }
            return IntPtr.Zero;
        }

        private void AddItemToListView(ListViewItem item)
        {
            if (ServerListView.InvokeRequired)
            {
                AddItemToListViewCallback cb = new(AddItemToListView);
                Invoke(cb, [item]);
            }
            else
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    ServerListView.Items.Add(item);
                }
            }
        }

        private async void FetchServersAsync()
        {
            // cancel (if exists) a previous server fetch
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();

            ServerListView.Items.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            var token = _cancellationTokenSource.Token;

            List<ServerInfo> servers = await Servers.GetServerInfosAsync(token);

            ServersLabel.Text = $"Servers: {servers.Count}";
            PlayersLabel.Text = $"Players: {servers.Sum(x => int.Parse(x.ClientNum!))}";

            await Parallel.ForEachAsync(servers, async (server, token) =>
            {
                var item = new ListViewItem(server.Hostname);
                item.SubItems.Add(server.Map);
                item.SubItems.Add(server.GameType);
                item.SubItems.Add($"{server.ClientNum}/{server.MaxClientNum}");

                if (serverPings.TryGetValue(server.Ip!, out var ping))
                {
                    server.Ping = ping;
                }

                if (server.Ping == "N/A")
                {
                    await server.PingHostAsync(token);
                    serverPings.TryAdd(server.Ip!, server.Ping);
                }

                item.SubItems.Add($"{server.Ping}");
                item.Tag = server.ToString();

                if (token.IsCancellationRequested)
                    return;
                AddItemToListView(item);
                _originalItems.Add(item);
            });
        }

        private void ServerListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var runningProcess = Process.GetProcessesByName("h2m-mod").FirstOrDefault();

            if (runningProcess == null)
            {
                MessageBox.Show("H2M-Mod is not running. Make sure to run the game before trying to connect to a server.", Text);
                return;
            }

            ListViewItem item = ServerListView.SelectedItems[0];

            if (item == null) return;

            var serverAddress = item.Tag?.ToString();
            SendConnectCommand(serverAddress!);
        }

        private void ServerListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            //Check if the column is already sorted
            if (_sortedColumnIndex == e.Column)
            {
                //Toggle the sort direction
                _sortAscending = !_sortAscending;
            }
            else
            {
                //Sort ascending if it's a new column
                _sortAscending = true;
                _sortedColumnIndex = e.Column;
            }

            //Sort the items in the list view
            List<ListViewItem> sortedItems = ServerListView.Items.Cast<ListViewItem>()
                .OrderBy(x => x.SubItems[e.Column].Text)
                .ToList();

            //Reverse the list if the sort direction is descending
            if (!_sortAscending) sortedItems.Reverse();

            //Clear the list view and add the sorted items
            ServerListView.Items.Clear();
            ServerListView.Items.AddRange(sortedItems.ToArray());
        }

        private void Filter_Tbx_TextChanged(object sender, EventArgs e)
        {
            //Get the filter text
            string filterText = Filter_Tbx.Text;

            //Check if the filter text is empty
            if (string.IsNullOrEmpty(filterText))
            {
                ServerListView.Items.Clear();
                ServerListView.Items.AddRange(_originalItems.ToArray());
                return;
            }

            //Filter the items in the list view
            List<ListViewItem> filteredItems = ServerListView.Items.Cast<ListViewItem>()
                .Where(x => x.SubItems.Cast<ListViewItem.ListViewSubItem>()
                .Any(y => y.Text.Contains(Filter_Tbx.Text, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            //Clear the list view and add the filtered items
            ServerListView.Items.Clear();
            ServerListView.Items.AddRange(filteredItems.ToArray());
        }
    }
}
