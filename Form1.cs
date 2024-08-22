using System.Collections;
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

        //refreshing
        private bool refreshing = false;

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
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        //Windows API constants
        internal const int WM_NCLBUTTONDOWN = 0xA1;
        internal const int HT_CAPTION = 0x2;
        internal const int WM_CHAR = 0x0102; // Message code for sending a character
        internal const int WM_KEYDOWN = 0x0100; // Message code for key down
        internal const int WM_KEYUP = 0x0101;   // Message code for key up


        internal const string processGame = "h2m-mod.exe";

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
                var processName = System.IO.Path.GetFileNameWithoutExtension(processGame);
                var runningProcess = Process.GetProcessesByName(processName).FirstOrDefault();

                if (runningProcess != null)
                {
                    label6.Text = "Info: " + "Info: h2m-mod.exe is already running.";
                    return;
                }

                // Proceed to launch the process if it's not running
                if (File.Exists("./" + processGame))
                {
                    Process.Start("./" + processGame);
                }
                else
                {
                    label6.Text = "Info: " + $"{processGame} not found!";
                }
            }
            catch (Exception ex)
            {
                label6.Text = "Info: " + $"Error launching {processGame}: " + ex.Message;
            }
        }

        private void SendConnectCommand(string command)
        {
            IntPtr h2mModWindow = FindH2MModWindow();

            if (h2mModWindow != IntPtr.Zero)
            {
                // Open In-Game Terminal Window
                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)Keys.Oemtilde, IntPtr.Zero);

                // Send the "connect" command to the terminal window
                foreach (char c in command)
                {
                    SendMessage(h2mModWindow, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                    Thread.Sleep(1);
                }

                // Sleep for 1ms to allow the command to be processed
                Thread.Sleep(1);

                // Simulate pressing the Enter key
                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)Keys.Enter, IntPtr.Zero);
                Thread.Sleep(1);
                SendMessage(h2mModWindow, WM_KEYUP, (IntPtr)Keys.Enter, IntPtr.Zero);
                Thread.Sleep(1);
                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)Keys.Oemtilde, IntPtr.Zero);

                Thread.Sleep(100);

                // Find the game window by its title or class name
                IntPtr gameWindow = FindWindow(null, "h2m-mod"); // Replace with your game's window name

                if (gameWindow != IntPtr.Zero)
                {
                    // Bring the game window to the foreground
                    SetForegroundWindow(gameWindow);
                }
                else
                {
                    label6.Text = "Info: " + "Could not find the game window.";
                }
            }
            else
            {
                label6.Text = "Info: " + "Could not find the h2m-mod terminal window.";
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
            if (refreshing) return;
            refreshing = true;

            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (OperationCanceledException) { }
            finally
            {
                _cancellationTokenSource?.Dispose();
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            if (ServerListView.InvokeRequired)
            {
                ServerListView.Invoke(new Action(() =>
                {
                    ServerListView.Items.Clear();
                    _originalItems.Clear();
                }));
            }
            else
            {
                ServerListView.Items.Clear();
                _originalItems.Clear();
            }

            try
            {
                List<ServerInfo> servers = await Servers.GetServerInfosAsync(token).ConfigureAwait(false);

                if (ServersLabel.InvokeRequired)
                {
                    ServersLabel.Invoke(new Action(() =>
                    {
                        ServersLabel.Text = $"Servers: {servers.Count}";
                        PlayersLabel.Text = $"Players: {servers.Sum(x => int.Parse(x.ClientNum!))}";
                    }));
                }
                else
                {
                    ServersLabel.Text = $"Servers: {servers.Count}";
                    PlayersLabel.Text = $"Players: {servers.Sum(x => int.Parse(x.ClientNum!))}";
                }

                var itemsToAdd = new List<ListViewItem>();

                var tasks = servers.Select(async server =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    var item = new ListViewItem(server.Hostname);
                    item.SubItems.Add(server.Map); // Keep the Map color default (normal)
                    item.SubItems.Add(server.GameType);
                    item.SubItems.Add($"{server.ClientNum}/{server.MaxClientNum}");

                    if (serverPings.TryGetValue(server.Ip!, out var ping))
                    {
                        server.Ping = ping;
                    }

                    if (server.Ping == "N/A")
                    {
                        await server.PingHostAsync(token).ConfigureAwait(false);
                        serverPings.TryAdd(server.Ip!, server.Ping);
                    }

                    item.SubItems.Add($"{server.Ping}");
                    item.Tag = server.ToString();

                    // Optional: Set color for other subitems, but skip the Map subitem
                    Color color = Color.White;
                    if (int.TryParse(server.Ping, out int pingValue))
                    {
                        if (pingValue < 50)
                            color = ColorTranslator.FromHtml("#00FF00"); // Green for low ping
                        else if (pingValue < 150)
                            color = ColorTranslator.FromHtml("#FFFF00"); // Yellow for medium ping
                        else
                            color = ColorTranslator.FromHtml("#FF0000"); // Red for high ping
                    }

                    item.UseItemStyleForSubItems = false;
                    item.SubItems[0].ForeColor = color; // Change color of Hostname
                                                        // Do not change color of item.SubItems[1] (Map)
                                                        //item.SubItems[2].BackColor = color; // Change background color of GameType
                    item.SubItems[4].ForeColor = color; // Change color of Ping

                    lock (itemsToAdd)
                    {
                        itemsToAdd.Add(item);
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);

                if (!token.IsCancellationRequested)
                {
                    if (ServerListView.InvokeRequired)
                    {
                        ServerListView.Invoke(new Action(() =>
                        {
                            ServerListView.Items.AddRange(itemsToAdd.ToArray());
                            _originalItems.AddRange(itemsToAdd);
                        }));
                    }
                    else
                    {
                        ServerListView.Items.AddRange(itemsToAdd.ToArray());
                        _originalItems.AddRange(itemsToAdd);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation if needed
            }
            catch (Exception ex)
            {
                label6.Text = "Info: " + $"Error fetching servers: {ex.Message}";
            }

            refreshing = false;

        }

        private void ServerListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(processGame);
            var runningProcess = Process.GetProcessesByName(processName).FirstOrDefault();

            if (runningProcess == null)
            {
                label6.Text = "Info: " + "H2M-Mod is not running. Make sure to run the game before trying to connect to a server.";
                return;
            }

            ListViewItem item = ServerListView.SelectedItems[0];

            if (item == null) return;

            var serverAddress = item.Tag?.ToString();
            SendConnectCommand(serverAddress!);
        }

        private void ServerListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Check if the column is already sorted
            if (_sortedColumnIndex == e.Column)
            {
                // Toggle the sort direction
                _sortAscending = !_sortAscending;
            }
            else
            {
                // Sort ascending if it's a new column
                _sortAscending = true;
                _sortedColumnIndex = e.Column;
            }

            // Sort the items in the list view
            SortListViewByColumn(e.Column);
        }

        private void SortListViewByColumn(int columnIndex)
        {
            // Retrieve and sort items based on the selected column
            var sortedItems = ServerListView.Items.Cast<ListViewItem>()
                .OrderBy(item => GetColumnValue(item, columnIndex))
                .ToList();

            // Reverse the list if the sort direction is descending
            if (!_sortAscending)
            {
                sortedItems.Reverse();
            }

            // Clear the list view and add the sorted items
            ServerListView.Items.Clear();
            ServerListView.Items.AddRange(sortedItems.ToArray());
        }

        private object GetColumnValue(ListViewItem item, int columnIndex)
        {
            var value = item.SubItems[columnIndex].Text;

            if (columnIndex == 4) // Assuming the 5th column (index 4) is Ping
            {
                // Handle "N/A" as a high value, so it appears last in sorting
                return int.TryParse(value, out var pingValue) ? pingValue : int.MaxValue;
            }

            if (columnIndex == 3) // Assuming the 4th column (index 3) is Player Count (e.g., "0/18")
            {
                // Extract the current player count for sorting
                var playerCount = value.Split('/')[0];
                return int.TryParse(playerCount, out var count) ? count : 0;
            }

            // For non-numeric columns, return the value as-is
            return value;
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

        private void button2_Click(object sender, EventArgs e)
        {
            FetchServersAsync();
        }

        private void SortListViewByPing(bool ascending)
        {
            // Use a custom comparer to sort by Ping column
            ServerListView.ListViewItemSorter = new PingComparer(ascending);
            ServerListView.Sort();
        }

        private class PingComparer : IComparer
        {
            private readonly bool _ascending;

            public PingComparer(bool ascending)
            {
                _ascending = ascending;
            }

            public int Compare(object x, object y)
            {
                var itemX = x as ListViewItem;
                var itemY = y as ListViewItem;

                // Assuming Ping is in the 5th column (index 4)
                var pingX = itemX?.SubItems[4].Text;
                var pingY = itemY?.SubItems[4].Text;

                // Convert ping values to integers and handle "N/A" cases
                var pingValueX = int.TryParse(pingX, out var xValue) ? xValue : int.MaxValue;
                var pingValueY = int.TryParse(pingY, out var yValue) ? yValue : int.MaxValue;

                // Compare the ping values
                int result = pingValueX.CompareTo(pingValueY);

                // If sorting is descending, invert the result
                return _ascending ? result : -result;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            LaunchH2MMod();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(processGame);
            var runningProcess = Process.GetProcessesByName(processName).FirstOrDefault();

            if (runningProcess == null)
            {
                label6.Text = "Info: " + "H2M-Mod is not running. Make sure to run the game before trying to connect to a server.";
                return;
            }

            ListViewItem item = ServerListView.SelectedItems[0];

            if (item == null) return;

            var serverAddress = item.Tag?.ToString();
            SendConnectCommand(serverAddress!);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
    }
}
