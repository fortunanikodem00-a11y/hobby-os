using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

public class PhoenixWinDesktop
{
    private static string repoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos\Hobby OS\Hobby OS\bin\Debug\net10.0-windows\src_repo");
    private static string githubApi = "https://github.com";
    private static Panel desktop = null!, startMenu = null!, startGrid = null!;
    private static TextBox txtSearch = null!;
    private static bool isDragging = false;
    private static Point dragOffset;

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        if (!Directory.Exists(repoPath)) Directory.CreateDirectory(repoPath);

        Form f = new Form { Text = "Phoenix OS // Windows 11 Edition", Size = new Size(1280, 800), BackColor = Color.FromArgb(18, 22, 33), StartPosition = FormStartPosition.CenterScreen };

        desktop = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 22, 33), AutoScroll = true };
        desktop.Click += (s, e) => startMenu.Visible = false;
        f.Controls.Add(desktop);

        Panel taskbar = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.FromArgb(28, 32, 42) };
        f.Controls.Add(taskbar);

        Button btnStart = new Button { Text = "⊞ Start", Size = new Size(90, 36), Location = new Point(500, 6), BackColor = Color.FromArgb(40, 45, 58), ForeColor = Color.DeepSkyBlue, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
        btnStart.FlatAppearance.BorderSize = 0;
        btnStart.Click += (s, e) => { startMenu.Visible = !startMenu.Visible; if (startMenu.Visible) { startMenu.BringToFront(); OdswiezStart(""); } };

        Button btnStore = new Button { Text = "🛍️ HobbyStore", Size = new Size(120, 36), Location = new Point(600, 6), BackColor = Color.FromArgb(40, 45, 58), ForeColor = Color.Gold, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
        btnStore.FlatAppearance.BorderSize = 0;
        btnStore.Click += (s, e) => OtworzHobbyStore();

        Button btnRefresh = new Button { Text = "↻", Size = new Size(36, 36), Location = new Point(730, 6), BackColor = Color.FromArgb(40, 45, 58), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (s, e) => OdswiezPulpit();

        Label lblClock = new Label { Text = DateTime.Now.ToString("HH:mm\ndd.MM.yyyy"), Size = new Size(90, 40), Location = new Point(1170, 4), ForeColor = Color.White, Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        System.Windows.Forms.Timer tmr = new System.Windows.Forms.Timer { Interval = 1000 };
        tmr.Tick += (s, e) => lblClock.Text = DateTime.Now.ToString("HH:mm\ndd.MM.yyyy");
        tmr.Start();

        taskbar.Controls.AddRange(new Control[] { btnStart, btnStore, btnRefresh, lblClock });

        startMenu = new Panel { Size = new Size(440, 500), Location = new Point(420, 240), BackColor = Color.FromArgb(32, 36, 48), Visible = false, BorderStyle = BorderStyle.FixedSingle };
        f.Controls.Add(startMenu);

        txtSearch = new TextBox { Location = new Point(20, 15), Size = new Size(400, 28), BackColor = Color.FromArgb(22, 25, 34), ForeColor = Color.White, Font = new Font("Segoe UI", 11), BorderStyle = BorderStyle.FixedSingle };
        txtSearch.TextChanged += (s, e) => OdswiezStart(txtSearch.Text);
        startMenu.Controls.Add(txtSearch);

        startGrid = new Panel { Location = new Point(20, 55), Size = new Size(400, 380), AutoScroll = true };
        startMenu.Controls.Add(startGrid);

        Button btnExit = new Button { Text = "⏻ Zamknij Phoenix OS", Location = new Point(20, 445), Size = new Size(400, 36), BackColor = Color.FromArgb(160, 40, 40), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnExit.FlatAppearance.BorderSize = 0;
        btnExit.Click += (s, e) => Application.Exit();
        startMenu.Controls.Add(btnExit);

        OdswiezPulpit();
        Application.Run(f);
    }

    private static void OdswiezPulpit()
    {
        desktop.Controls.Clear();
        int x = 25, y = 25;
        foreach (string file in Directory.GetFiles(repoPath, "*.cs"))
        {
            string name = Path.GetFileName(file);
            Panel icon = new Panel { Size = new Size(90, 95), Location = new Point(x, y), BackColor = Color.Transparent, Cursor = Cursors.Hand };
            Label pic = new Label { Text = "📄", Size = new Size(90, 50), Font = new Font("Segoe UI Emoji", 24), ForeColor = Color.DeepSkyBlue, TextAlign = ContentAlignment.MiddleCenter };
            Label lbl = new Label { Text = name, Size = new Size(90, 40), Location = new Point(0, 52), Font = new Font("Segoe UI", 8.5f), ForeColor = Color.White, TextAlign = ContentAlignment.TopCenter };

            MouseEventHandler down = (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; dragOffset = e.Location; icon.BringToFront(); } };
            MouseEventHandler move = (s, e) => { if (isDragging) { icon.Left += e.X - dragOffset.X; icon.Top += e.Y - dragOffset.Y; } };
            MouseEventHandler up = (s, e) => isDragging = false;
            EventHandler dbl = (s, e) => OdpalApke(file);

            foreach (Control c in new Control[] { icon, pic, lbl }) { c.MouseDown += down; c.MouseMove += move; c.MouseUp += up; c.DoubleClick += dbl; }
            icon.Controls.AddRange(new Control[] { pic, lbl });
            desktop.Controls.Add(icon);

            y += 110;
            if (y > 620) { y = 25; x += 105; }
        }
    }

    private static void OdswiezStart(string filtr)
    {
        startGrid.Controls.Clear();
        int y = 5;
        foreach (string file in Directory.GetFiles(repoPath, "*.cs"))
        {
            string name = Path.GetFileName(file);
            if (!string.IsNullOrEmpty(filtr) && !name.ToLower().Contains(filtr.ToLower())) continue;

            Button item = new Button { Text = "  🚀  " + name, Location = new Point(0, y), Size = new Size(375, 40), BackColor = Color.FromArgb(42, 47, 62), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10), TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand };
            item.FlatAppearance.BorderSize = 0;
            item.Click += (s, e) => { startMenu.Visible = false; OdpalApke(file); };
            startGrid.Controls.Add(item);
            y += 46;
        }
    }

    private static async void OtworzHobbyStore()
    {
        Form store = new Form { Text = "🛍️ HobbyStore — GitHub Repo", Size = new Size(650, 500), BackColor = Color.FromArgb(24, 28, 38), StartPosition = FormStartPosition.CenterParent };
        Panel list = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(15) };
        Label status = new Label { Text = "Łączenie z repozytorium GitHub...", Dock = DockStyle.Top, Height = 30, ForeColor = Color.Gold, Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        store.Controls.AddRange(new Control[] { list, status });
        store.Show();

        try
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PhoenixOS-HobbyStore");
            string json = await client.GetStringAsync(githubApi);
            using JsonDocument doc = JsonDocument.Parse(json);

            int y = 10;
            foreach (JsonElement el in doc.RootElement.EnumerateArray())
            {
                string name = el.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".cs")) continue;
                string dlUrl = el.GetProperty("download_url").GetString() ?? "";

                Panel card = new Panel { Location = new Point(15, y), Size = new Size(580, 55), BackColor = Color.FromArgb(35, 40, 54) };
                Label lblName = new Label { Text = "📦 " + name, Location = new Point(15, 15), Size = new Size(360, 25), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
                Button btnGet = new Button { Text = "Pobierz", Location = new Point(450, 10), Size = new Size(110, 35), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
                btnGet.FlatAppearance.BorderSize = 0;

                btnGet.Click += async (s, e) =>
                {
                    btnGet.Enabled = false;
                    btnGet.Text = "Pobieranie...";
                    string code = await client.GetStringAsync(dlUrl);
                    File.WriteAllText(Path.Combine(repoPath, name), code);
                    btnGet.Text = "Zainstalowano";
                    btnGet.BackColor = Color.SeaGreen;
                    OdswiezPulpit();
                };

                card.Controls.AddRange(new Control[] { lblName, btnGet });
                list.Controls.Add(card);
                y += 65;
            }
            status.Text = "Dostępne aplikacje w repozytorium:";
        }
        catch (Exception ex) { status.Text = "Błąd połączenia: " + ex.Message; status.ForeColor = Color.Red; }
    }

    private static void OdpalApke(string csFile)
    {
        string appName = Path.GetFileNameWithoutExtension(csFile);
        string tempDir = Path.Combine(Path.GetTempPath(), "HobbyOSApps", appName);
        Directory.CreateDirectory(tempDir);
        File.Copy(csFile, Path.Combine(tempDir, "Program.cs"), true);
      
char lt = (char)60, gt = (char)62;
string csproj = $"{lt}Project Sdk='Microsoft.NET.Sdk'{gt}{lt}PropertyGroup{gt}{lt}OutputType{gt}WinExe{lt}/OutputType{gt}{lt}TargetFramework{gt}net10.0-windows{lt}/TargetFramework{gt}{lt}UseWindowsForms{gt}true{lt}/UseWindowsForms{gt}{lt}/PropertyGroup{gt}{lt}/Project{gt}";
Process.Start(new ProcessStartInfo { FileName = "dotnet", Arguments = "run", WorkingDirectory = tempDir, UseShellExecute = true });
}
}
