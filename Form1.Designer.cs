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
        label2 = new Label();
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
        SuspendLayout();
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.BackColor = Color.Transparent;
        label1.Font = new Font("Gadugi", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label1.ForeColor = Color.White;
        label1.Location = new Point(9, 9);
        label1.Name = "label1";
        label1.Size = new Size(200, 32);
        label1.TabIndex = 1;
        label1.Text = "H2M Launcher";
        // 
        // label2
        // 
        label2.AutoSize = true;
        label2.BackColor = Color.Transparent;
        label2.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label2.ForeColor = Color.White;
        label2.Location = new Point(12, 41);
        label2.Name = "label2";
        label2.Size = new Size(183, 19);
        label2.TabIndex = 2;
        label2.Text = "Press L to Launch H2M.";
        // 
        // label3
        // 
        label3.AutoSize = true;
        label3.BackColor = Color.Transparent;
        label3.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label3.ForeColor = Color.White;
        label3.Location = new Point(12, 60);
        label3.Name = "label3";
        label3.Size = new Size(249, 19);
        label3.TabIndex = 3;
        label3.Text = "Press R to refresh the server list.";
        // 
        // label4
        // 
        label4.AutoSize = true;
        label4.BackColor = Color.Transparent;
        label4.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label4.ForeColor = Color.White;
        label4.Location = new Point(12, 79);
        label4.Name = "label4";
        label4.Size = new Size(330, 19);
        label4.TabIndex = 4;
        label4.Text = "Press S to save the server list to favourites.";
        // 
        // ServersLabel
        // 
        ServersLabel.AutoSize = true;
        ServersLabel.BackColor = Color.Transparent;
        ServersLabel.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ServersLabel.ForeColor = Color.White;
        ServersLabel.Location = new Point(684, 9);
        ServersLabel.Name = "ServersLabel";
        ServersLabel.Size = new Size(68, 19);
        ServersLabel.TabIndex = 5;
        ServersLabel.Text = "Servers:";
        // 
        // PlayersLabel
        // 
        PlayersLabel.AutoSize = true;
        PlayersLabel.BackColor = Color.Transparent;
        PlayersLabel.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        PlayersLabel.ForeColor = Color.White;
        PlayersLabel.Location = new Point(684, 30);
        PlayersLabel.Name = "PlayersLabel";
        PlayersLabel.Size = new Size(68, 19);
        PlayersLabel.TabIndex = 6;
        PlayersLabel.Text = "Players:";
        // 
        // label7
        // 
        label7.AutoSize = true;
        label7.BackColor = Color.Transparent;
        label7.Font = new Font("Gadugi", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label7.ForeColor = Color.White;
        label7.Location = new Point(12, 98);
        label7.Name = "label7";
        label7.Size = new Size(211, 19);
        label7.TabIndex = 7;
        label7.Text = "Press ESC to Exit Launcher.";
        // 
        // ServerListView
        // 
        ServerListView.BackColor = SystemColors.InactiveCaptionText;
        ServerListView.BorderStyle = BorderStyle.FixedSingle;
        ServerListView.Columns.AddRange(new ColumnHeader[] { Hostname, Map, GameType, Players });
        ServerListView.Font = new Font("Gadugi", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ServerListView.ForeColor = Color.White;
        ServerListView.FullRowSelect = true;
        ServerListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        ServerListView.Location = new Point(12, 120);
        ServerListView.MultiSelect = false;
        ServerListView.Name = "ServerListView";
        ServerListView.Size = new Size(780, 329);
        ServerListView.TabIndex = 8;
        ServerListView.UseCompatibleStateImageBehavior = false;
        ServerListView.View = View.Details;
        ServerListView.ColumnWidthChanging += ServerListView_ColumnWidthChanging;
        ServerListView.MouseDoubleClick += ServerListView_MouseDoubleClick;
        // 
        // Hostname
        // 
        Hostname.Text = "Host Name (Double Click to Join)";
        Hostname.Width = 475;
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
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
        BackgroundImageLayout = ImageLayout.Stretch;
        ClientSize = new Size(804, 461);
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
        MaximumSize = new Size(804, 461);
        MinimumSize = new Size(804, 461);
        Name = "Form1";
        Text = "H2M Launcher";
        KeyDown += Form1_KeyPress;
        MouseDown += Form1_MouseDown;
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
    private Label label1;
    private Label label2;
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
}