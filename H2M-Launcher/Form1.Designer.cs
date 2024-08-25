using System.Windows.Forms;

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
        FavoritesCheck = new ColumnHeader();
        Ping = new ColumnHeader();
        Filter_Tbx = new TextBox();
        label5 = new Label();
        tabControl1 = new TabControl();
        tabPage1 = new TabPage();
        tabPage2 = new TabPage();
        listView1 = new ListView();
        columnHeader1 = new ColumnHeader();
        columnHeader2 = new ColumnHeader();
        columnHeader3 = new ColumnHeader();
        columnHeader4 = new ColumnHeader();
        columnHeader5 = new ColumnHeader();
        columnHeader6 = new ColumnHeader();
        tabControl1.SuspendLayout();
        tabPage1.SuspendLayout();
        tabPage2.SuspendLayout();
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
        ServersLabel.Location = new Point(785, 9);
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
        PlayersLabel.Location = new Point(785, 30);
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
        ServerListView.Columns.AddRange(new ColumnHeader[] { Hostname, Map, GameType, Players, FavoritesCheck, Ping });
        ServerListView.Font = new Font("Gadugi", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ServerListView.ForeColor = Color.White;
        ServerListView.FullRowSelect = true;
        ServerListView.Location = new Point(0, 0);
        ServerListView.MultiSelect = false;
        ServerListView.Name = "ServerListView";
        ServerListView.Size = new Size(999, 320);
        ServerListView.TabIndex = 8;
        ServerListView.UseCompatibleStateImageBehavior = false;
        ServerListView.View = View.Details;
        ServerListView.ColumnClick += ServerListView_ColumnClick;
        ServerListView.MouseClick += ServerListView_MouseClick;
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
        // FavoritesCheck
        // 
        FavoritesCheck.Text = "Favorites";
        FavoritesCheck.Width = 65;
        // 
        // Ping
        // 
        Ping.Text = "Ping";
        // 
        // Filter_Tbx
        // 
        Filter_Tbx.BorderStyle = BorderStyle.FixedSingle;
        Filter_Tbx.Font = new Font("Gadugi", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        Filter_Tbx.Location = new Point(647, 87);
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
        label5.ForeColor = Color.White;
        label5.Location = new Point(593, 90);
        label5.Name = "label5";
        label5.Size = new Size(52, 19);
        label5.TabIndex = 10;
        label5.Text = "Filter:";
        // 
        // tabControl1
        // 
        tabControl1.Controls.Add(tabPage1);
        tabControl1.Controls.Add(tabPage2);
        tabControl1.Location = new Point(9, 118);
        tabControl1.Name = "tabControl1";
        tabControl1.SelectedIndex = 0;
        tabControl1.Size = new Size(1003, 331);
        tabControl1.TabIndex = 11;
        tabControl1.SelectedIndexChanged += new EventHandler(tabControl1_SelectedIndexChanged);

        // 
        // tabPage1
        // 
        tabPage1.Controls.Add(ServerListView);
        tabPage1.Location = new Point(4, 24);
        tabPage1.Name = "tabPage1";
        tabPage1.Padding = new Padding(3);
        tabPage1.Size = new Size(995, 303);
        tabPage1.TabIndex = 0;
        tabPage1.Text = "Server Browser";
        tabPage1.UseVisualStyleBackColor = true;
        // 
        // tabPage2
        // 
        tabPage2.Controls.Add(listView1);
        tabPage2.Location = new Point(4, 24);
        tabPage2.Name = "tabPage2";
        tabPage2.Padding = new Padding(3);
        tabPage2.Size = new Size(995, 303);
        tabPage2.TabIndex = 1;
        tabPage2.Text = "Favorites";
        tabPage2.UseVisualStyleBackColor = true;
        // 
        // listView1
        // 
        listView1.BackColor = SystemColors.InactiveCaptionText;
        listView1.BorderStyle = BorderStyle.FixedSingle;
        listView1.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2, columnHeader3, columnHeader4, columnHeader5, columnHeader6 });
        listView1.Font = new Font("Gadugi", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        listView1.ForeColor = Color.White;
        listView1.FullRowSelect = true;
        listView1.Location = new Point(0, 0);
        listView1.MultiSelect = false;
        listView1.Name = "listView1";
        listView1.Size = new Size(999, 320);
        listView1.TabIndex = 9;
        listView1.UseCompatibleStateImageBehavior = false;
        listView1.View = View.Details;
        // 
        // columnHeader1
        // 
        columnHeader1.Text = "Host Name (Double Click to Join)";
        columnHeader1.Width = 510;
        // 
        // columnHeader2
        // 
        columnHeader2.Text = "Map";
        columnHeader2.Width = 130;
        // 
        // columnHeader3
        // 
        columnHeader3.Text = "Game Type";
        columnHeader3.Width = 90;
        // 
        // columnHeader4
        // 
        columnHeader4.Text = "Players";
        columnHeader4.Width = 65;
        // 
        // columnHeader5
        // 
        columnHeader5.Text = "Favorites";
        columnHeader5.Width = 65;
        // 
        // columnHeader6
        // 
        columnHeader6.Text = "Ping";
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
        BackgroundImageLayout = ImageLayout.Stretch;
        ClientSize = new Size(1024, 461);
        Controls.Add(tabControl1);
        Controls.Add(label5);
        Controls.Add(Filter_Tbx);
        Controls.Add(label7);
        Controls.Add(PlayersLabel);
        Controls.Add(ServersLabel);
        Controls.Add(label4);
        Controls.Add(label3);
        Controls.Add(label2);
        Controls.Add(label1);
        FormBorderStyle = FormBorderStyle.None;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximumSize = new Size(1024, 461);
        MinimumSize = new Size(1024, 461);
        Name = "Form1";
        Text = "H2M Launcher";
        KeyDown += Form1_KeyPress;
        MouseDown += Form1_MouseDown;
        tabControl1.ResumeLayout(false);
        tabPage1.ResumeLayout(false);
        tabPage2.ResumeLayout(false);
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
    private ColumnHeader FavoritesCheck;
    private ColumnHeader Ping;
    private TextBox Filter_Tbx;
    private Label label5;
    private TabControl tabControl1;
    private TabPage tabPage1;
    private TabPage tabPage2;
    private ListView listView1;
    private ColumnHeader columnHeader1;
    private ColumnHeader columnHeader2;
    private ColumnHeader columnHeader3;
    private ColumnHeader columnHeader4;
    private ColumnHeader columnHeader5;
    private ColumnHeader columnHeader6;
}