using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

public class PsdnsStore : Form
{
    private FlowLayoutPanel grid = null!;
    private PictureBox logoBox = null!;
    private Label lblStatus = null!;
    private static readonly HttpClient http = new HttpClient();

    private const string ApiUrl =
        "https://github.com/fortunanikodem00-a11y/hobby-os";
    private const string LogoUrl =
        "https://github.com/fortunanikodem00-a11y/hobby-os/blob/main/psdnsdigitalstore.png?raw=true";

    private static readonly string RepoDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        @"source\repos\Hobby OS\Hobby OS\bin\Debug\net10.0-windows\src_repo");

    public PsdnsStore()
    {
        Text = "PSDNS Store — HobbyOS";
        Size = new Size(920, 600);
        BackColor = Color.FromArgb(14, 14, 18);
        StartPosition = FormStartPosition.CenterScreen;

        if (!http.DefaultRequestHeaders.Contains("User-Agent"))
            http.DefaultRequestHeaders.Add("User-Agent", "PSDNS-Store-HobbyOS");

        BuildUi();
        _ = LoadStoreAsync();
    }

    private void BuildUi()
    {
        Panel header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = Color.FromArgb(8, 8, 12),
            Padding = new Padding(14, 8, 14, 8)
        };

        logoBox = new PictureBox
        {
            Location = new Point(14, 8),
            Size = new Size(130, 70),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };

        Label title = new Label
        {
            Text = "PSDNS STORE",
            Location = new Point(158, 14),
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 145, 0),
            Font = new Font("Segoe UI", 18, FontStyle.Bold)
        };

        lblStatus = new Label
        {
            Text = "Łączenie z GitHub (fortunanikodem00-a11y/hobby-os)...",
            Location = new Point(160, 50),
            AutoSize = true,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 9.5f)
        };

        Button btnRefresh = new Button
        {
            Text = "↻ Odśwież",
            Size = new Size(110, 36),
            Location = new Point(780, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(30, 80, 220),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += async (s, e) => await LoadStoreAsync();

        header.Controls.AddRange(new Control[] { logoBox, title, lblStatus, btnRefresh });
        Controls.Add(header);

        grid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            BackColor = Color.FromArgb(14, 14, 18)
        };
        Controls.Add(grid);
        grid.BringToFront();
    }

    private async Task LoadStoreAsync()
    {
        grid.Controls.Clear();
        lblStatus.Text = "Pobieranie listy aplikacji z GitHuba...";

        _ = LoadLogoAsync();

        try
        {
            string json = await http.GetStringAsync(ApiUrl);
            using JsonDocument doc = JsonDocument.Parse(json);

            int count = 0;
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                string name = item.GetProperty("name").GetString() ?? "";
                string dlUrl = item.GetProperty("download_url").GetString() ?? "";
                long size = item.GetProperty("size").GetInt64();

                if (!name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                AddCard(name, dlUrl, size);
                count++;
            }

            lblStatus.Text = $"Połączono z GitHub • Dostępne aplikacje: {count}";
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Błąd połączenia z GitHub: " + ex.Message;
        }
    }

    private async Task LoadLogoAsync()
    {
        try
        {
            byte[] bytes = await http.GetByteArrayAsync(LogoUrl);
            using MemoryStream ms = new MemoryStream(bytes);
            logoBox.Image = Image.FromStream(ms);
        }
        catch { }
    }

    private void AddCard(string fileName, string downloadUrl, long size)
    {
        bool installed = File.Exists(Path.Combine(RepoDir, fileName));

        Panel card = new Panel
        {
            Size = new Size(270, 145),
            Margin = new Padding(10),
            BackColor = Color.FromArgb(24, 24, 32)
        };

        Label lblName = new Label
        {
            Text = Path.GetFileNameWithoutExtension(fileName),
            Location = new Point(14, 14),
            Size = new Size(242, 28),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };

        Label lblInfo = new Label
        {
            Text = $"Plik: {fileName}\nRozmiar: {Math.Round(size / 1024.0, 1)} KB",
            Location = new Point(14, 46),
            Size = new Size(242, 42),
            ForeColor = Color.Silver,
            Font = new Font("Segoe UI", 9)
        };

        Button btnAction = new Button
        {
            Text = installed ? "▶ Uruchom / Aktualizuj" : "⬇ Pobierz i uruchom",
            Location = new Point(14, 96),
            Size = new Size(242, 34),
            BackColor = installed ? Color.FromArgb(35, 150, 70) : Color.FromArgb(255, 140, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAction.FlatAppearance.BorderSize = 0;

        btnAction.Click += async (s, e) =>
        {
            btnAction.Enabled = false;
            btnAction.Text = "Pobieranie...";
            await DownloadAndRunAsync(fileName, downloadUrl);
            btnAction.Text = "▶ Uruchom / Aktualizuj";
            btnAction.BackColor = Color.FromArgb(35, 150, 70);
            btnAction.Enabled = true;
        };

        card.Controls.AddRange(new Control[] { lblName, lblInfo, btnAction });
        grid.Controls.Add(card);
    }

    private async Task DownloadAndRunAsync(string fileName, string url)
    {
        try
        {
            Directory.CreateDirectory(RepoDir);
            string targetFile = Path.Combine(RepoDir, fileName);
            string code = await http.GetStringAsync(url);
            await File.WriteAllTextAsync(targetFile, code);

            string appName = Path.GetFileNameWithoutExtension(fileName);
            string tempDir = Path.Combine(Path.GetTempPath(), "HobbyOSApps", appName);
            Directory.CreateDirectory(tempDir);
            File.Copy(targetFile, Path.Combine(tempDir, "Program.cs"), true);

            string csproj =
                "<Project Sdk=\"Microsoft.NET.Sdk\">" +
                "<PropertyGroup>" +
                "<OutputType>WinExe</OutputType>" +
                "<TargetFramework>net10.0-windows</TargetFramework>" +
                "<UseWindowsForms>true</UseWindowsForms>" +
                "</PropertyGroup>" +
                "</Project>";

            await File.WriteAllTextAsync(Path.Combine(tempDir, appName + ".csproj"), csproj);

            Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run",
                WorkingDirectory = tempDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Błąd instalacji: " + ex.Message, "PSDNS Store");
        }
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new PsdnsStore());
    }
}
