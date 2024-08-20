using System.Diagnostics;
using System.Runtime.InteropServices;

namespace H2M_Launcher
{
    public partial class Form1 : Form
    {
        private static readonly CancellationTokenSource _cancellationTokenSource = new();

        delegate void AddItemToListViewCallback(ListViewItem listViewItem);

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
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
                Servers.SaveServerList();
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
                ServerListView.Items.Add(item);
        }

        private async void FetchServersAsync()
        {
            // cancel (if exists) a previous server fetch
            _cancellationTokenSource.TryReset();
            ServerListView.Items.Clear();

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
                await server.PingHostAsync(token);
                item.SubItems.Add($"{server.Ping}");
                item.Tag = server.ToString();

                if (token.IsCancellationRequested)
                    return;
                AddItemToListView(item);
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

        // Windows API functions to send input to a window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_CHAR = 0x0102; // Message code for sending a character
        private const int WM_KEYDOWN = 0x0100; // Message code for key down
        private const int WM_KEYUP = 0x0101;   // Message code for key up

        private void ServerListView_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.Cancel = true;
            e.NewWidth = ServerListView.Columns[e.ColumnIndex].Width;
        }
    }
}
