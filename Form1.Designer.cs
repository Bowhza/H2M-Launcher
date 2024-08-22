namespace H2M_Launcher;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
        label1 = new Label();
        label3 = new Label();
        label4 = new Label();
        ServersLabel = new Label();
        PlayersLabel = new Label();
        label7 = new Label();
        ServerListView = new ListView();
        Hostname = new ColumnHeader();
        Map = new ColumnHeader();
        GameType = new ColumnHeader();
        Players = new ColumnHeader();
        Ping = new ColumnHeader();
        Filter_Tbx = new TextBox();
        label5 = new Label();
        label6 = new Label();
        button1 = new Button();
        label2 = new Label();
        button2 = new Button();
        button3 = new Button();
        button4 = new Button();
        button5 = new Button();
        SuspendLayout();
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.BackColor = Color.Transparent;
        label1.Font = new Font("Gadugi", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label1.ForeColor = Color.FromArgb(128, 179, 0);
        label1.Location = new Point(9, 9);
        label1.Name = "label1";
        label1.Size = new Size(200, 32);
        label1.TabIndex = 1;
        label1.Text = "H2M Launcher";
        label1.MouseDown += Form1_MouseDown;
        // 
        // label3
        // 
        label3.AutoSize = true;
        label3.BackColor = Color.Transparent;
        label3.Font = new Font("Consolas", 9.75F, FontStyle.Bold);
        label3.ForeColor = Color.FromArgb(128, 179, 0);
        label3.Location = new Point(17, 68);
        label3.Name = "label3";
        label3.Size = new Size(252, 15);
        label3.TabIndex = 3;
        label3.Text = "Press R to refresh the server list.";
        label3.MouseDown += Form1_MouseDown;
        // 
        // label4
        // 
        label4.AutoSize = true;
        label4.BackColor = Color.Transparent;
        label4.Font = new Font("Consolas", 9.75F, FontStyle.Bold);
        label4.ForeColor = Color.FromArgb(128, 179, 0);
        label4.Location = new Point(17, 87);
        label4.Name = "label4";
        label4.Size = new Size(329, 15);
        label4.TabIndex = 4;
        label4.Text = "Press S to save the server list to favourites.";
        label4.MouseDown += Form1_MouseDown;
        // 
        // ServersLabel
        // 
        ServersLabel.BackColor = Color.Transparent;
        ServersLabel.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ServersLabel.ForeColor = Color.FromArgb(128, 179, 0);
        ServersLabel.Location = new Point(12, 132);
        ServersLabel.Name = "ServersLabel";
        ServersLabel.Size = new Size(106, 19);
        ServersLabel.TabIndex = 5;
        ServersLabel.Text = "Servers: 0";
        ServersLabel.MouseDown += Form1_MouseDown;
        // 
        // PlayersLabel
        // 
        PlayersLabel.BackColor = Color.Transparent;
        PlayersLabel.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        PlayersLabel.ForeColor = Color.FromArgb(128, 179, 0);
        PlayersLabel.Location = new Point(124, 132);
        PlayersLabel.Name = "PlayersLabel";
        PlayersLabel.Size = new Size(99, 19);
        PlayersLabel.TabIndex = 6;
        PlayersLabel.Text = "Players: 0";
        PlayersLabel.MouseDown += Form1_MouseDown;
        // 
        // label7
        // 
        label7.AutoSize = true;
        label7.BackColor = Color.Transparent;
        label7.Font = new Font("Consolas", 9.75F, FontStyle.Bold);
        label7.ForeColor = Color.FromArgb(128, 179, 0);
        label7.Location = new Point(17, 106);
        label7.Name = "label7";
        label7.Size = new Size(196, 15);
        label7.TabIndex = 7;
        label7.Text = "Press ESC to Exit Launcher.";
        label7.MouseDown += Form1_MouseDown;
        // 
        // ServerListView
        // 
        ServerListView.BackColor = SystemColors.InactiveCaptionText;
        ServerListView.BorderStyle = BorderStyle.FixedSingle;
        ServerListView.Columns.AddRange(new ColumnHeader[] { Hostname, Map, GameType, Players, Ping });
        ServerListView.Font = new Font("Gadugi", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ServerListView.ForeColor = Color.White;
        ServerListView.FullRowSelect = true;
        ServerListView.Location = new Point(12, 158);
        ServerListView.MultiSelect = false;
        ServerListView.Name = "ServerListView";
        ServerListView.Size = new Size(876, 253);
        ServerListView.TabIndex = 8;
        ServerListView.UseCompatibleStateImageBehavior = false;
        ServerListView.View = View.Details;
        ServerListView.ColumnClick += ServerListView_ColumnClick;
        ServerListView.MouseDoubleClick += ServerListView_MouseDoubleClick;
        // 
        // Hostname
        // 
        Hostname.Text = "Host Name (Double Click to Join)";
        Hostname.Width = 510;
        // 
        // Map
        // 
        Map.Text = "Map";
        Map.Width = 130;
        // 
        // GameType
        // 
        GameType.Text = "Game Type";
        GameType.Width = 90;
        // 
        // Players
        // 
        Players.Text = "Players";
        Players.Width = 65;
        // 
        // Ping
        // 
        Ping.Text = "Ping";
        // 
        // Filter_Tbx
        // 
        Filter_Tbx.BackColor = Color.Black;
        Filter_Tbx.BorderStyle = BorderStyle.FixedSingle;
        Filter_Tbx.Font = new Font("Gadugi", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        Filter_Tbx.ForeColor = Color.FromArgb(128, 179, 0);
        Filter_Tbx.Location = new Point(446, 126);
        Filter_Tbx.Name = "Filter_Tbx";
        Filter_Tbx.Size = new Size(241, 25);
        Filter_Tbx.TabIndex = 9;
        Filter_Tbx.TextChanged += Filter_Tbx_TextChanged;
        // 
        // label5
        // 
        label5.AutoSize = true;
        label5.BackColor = Color.Transparent;
        label5.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label5.ForeColor = Color.FromArgb(128, 179, 0);
        label5.Location = new Point(392, 129);
        label5.Name = "label5";
        label5.Size = new Size(52, 19);
        label5.TabIndex = 10;
        label5.Text = "Filter:";
        label5.MouseDown += Form1_MouseDown;
        // 
        // label6
        // 
        label6.BackColor = Color.Transparent;
        label6.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label6.ForeColor = Color.FromArgb(128, 179, 0);
        label6.Location = new Point(9, 417);
        label6.Name = "label6";
        label6.RightToLeft = RightToLeft.No;
        label6.Size = new Size(765, 32);
        label6.TabIndex = 11;
        label6.Text = "Info:";
        label6.TextAlign = ContentAlignment.MiddleLeft;
        label6.MouseDown += Form1_MouseDown;
        // 
        // button1
        // 
        button1.FlatAppearance.BorderColor = Color.FromArgb(128, 179, 0);
        button1.FlatStyle = FlatStyle.Flat;
        button1.ForeColor = Color.FromArgb(128, 179, 0);
        button1.Location = new Point(793, 126);
        button1.Name = "button1";
        button1.Size = new Size(95, 25);
        button1.TabIndex = 12;
        button1.Text = "Launch H2M";
        button1.UseVisualStyleBackColor = true;
        button1.Click += button1_Click;
        // 
        // label2
        // 
        label2.AutoSize = true;
        label2.BackColor = Color.Transparent;
        label2.Font = new Font("Consolas", 9.75F, FontStyle.Bold);
        label2.ForeColor = Color.FromArgb(128, 179, 0);
        label2.Location = new Point(17, 49);
        label2.Name = "label2";
        label2.Size = new Size(161, 15);
        label2.TabIndex = 2;
        label2.Text = "Press L to Launch H2M.";
        label2.MouseDown += Form1_MouseDown;
        // 
        // button2
        // 
        button2.FlatAppearance.BorderColor = Color.FromArgb(128, 179, 0);
        button2.FlatStyle = FlatStyle.Flat;
        button2.ForeColor = Color.FromArgb(128, 179, 0);
        button2.Location = new Point(693, 126);
        button2.Name = "button2";
        button2.Size = new Size(94, 25);
        button2.TabIndex = 13;
        button2.Text = "Refresh";
        button2.UseVisualStyleBackColor = true;
        button2.Click += button2_Click;
        // 
        // button3
        // 
        button3.FlatAppearance.BorderColor = Color.FromArgb(128, 179, 0);
        button3.FlatStyle = FlatStyle.Flat;
        button3.ForeColor = Color.FromArgb(128, 179, 0);
        button3.Location = new Point(780, 417);
        button3.Name = "button3";
        button3.Size = new Size(108, 32);
        button3.TabIndex = 14;
        button3.Text = "Join Server";
        button3.UseVisualStyleBackColor = true;
        button3.Click += button3_Click;
        // 
        // button4
        // 
        button4.FlatAppearance.BorderColor = Color.FromArgb(128, 179, 0);
        button4.FlatStyle = FlatStyle.Flat;
        button4.ForeColor = Color.FromArgb(128, 179, 0);
        button4.Location = new Point(834, 9);
        button4.Name = "button4";
        button4.Size = new Size(54, 23);
        button4.TabIndex = 15;
        button4.Text = "Close";
        button4.UseVisualStyleBackColor = true;
        button4.Click += button4_Click;
        // 
        // button5
        // 
        button5.FlatAppearance.BorderColor = Color.FromArgb(128, 179, 0);
        button5.FlatStyle = FlatStyle.Flat;
        button5.ForeColor = Color.FromArgb(128, 179, 0);
        button5.Location = new Point(774, 9);
        button5.Name = "button5";
        button5.Size = new Size(54, 23);
        button5.TabIndex = 16;
        button5.Text = "Hide";
        button5.UseVisualStyleBackColor = true;
        button5.Click += button5_Click;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.Black;
        BackgroundImageLayout = ImageLayout.Stretch;
        ClientSize = new Size(900, 461);
        Controls.Add(button5);
        Controls.Add(button4);
        Controls.Add(button3);
        Controls.Add(button2);
        Controls.Add(button1);
        Controls.Add(label6);
        Controls.Add(label5);
        Controls.Add(Filter_Tbx);
        Controls.Add(ServerListView);
        Controls.Add(label7);
        Controls.Add(PlayersLabel);
        Controls.Add(ServersLabel);
        Controls.Add(label4);
        Controls.Add(label3);
        Controls.Add(label2);
        Controls.Add(label1);
        FormBorderStyle = FormBorderStyle.None;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximumSize = new Size(900, 461);
        MinimumSize = new Size(900, 461);
        Name = "Form1";
        Text = "H2M Launcher";
        KeyDown += Form1_KeyPress;
        MouseDown += Form1_MouseDown;
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
    private Label label1;
    private Label label3;
    private Label label4;
    private Label ServersLabel;
    private Label PlayersLabel;
    private Label label7;
    private ListView ServerListView;
    private ColumnHeader Hostname;
    private ColumnHeader Map;
    private ColumnHeader GameType;
    private ColumnHeader Players;
    private ColumnHeader Ping;
    private TextBox Filter_Tbx;
    private Label label5;
    private Button button1;
    private Label label2;
    private Button button2;
    private Button button3;
    private Label label6;
    private Button button4;
    private Button button5;
}