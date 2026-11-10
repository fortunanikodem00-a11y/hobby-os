using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

public class HobbyFastfetch : Form
{
    private RichTextBox consoleBox = null!;
    private TextBox inputBox = null!;
    private PictureBox logoBox = null!;

    private static string repoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos\Hobby OS\Hobby OS\bin\Debug\net10.0-windows\src_repo");
    private static string[] githubLogoUrls = new string[]
    {
"https://github.com/fortunanikodem00-a11y/hobby-os/blob/main/hobby%20os%20logo.png?raw=true",
    };

    public HobbyFastfetch()
    {
        this.Text = "admin@hobby-os: ~ (fastfetch)";
        this.Size = new Size(920, 480);
        this.BackColor = Color.FromArgb(18, 18, 24);
        this.StartPosition = FormStartPosition.CenterScreen;

        Panel bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = Color.FromArgb(12, 12, 16),
            Padding = new Padding(10, 6, 10, 6)
        };

        Label prompt = new Label
        {
            Text = "admin@hobby-os:~$",
            ForeColor = Color.LimeGreen,
            Font = new Font("Consolas", 10.5f, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left
        };

        inputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 12, 16),
            ForeColor = Color.White,
            Font = new Font("Consolas", 10.5f),
            BorderStyle = BorderStyle.None
        };
        inputBox.KeyDown += HandleCommand;

        bottomPanel.Controls.Add(inputBox);
        bottomPanel.Controls.Add(prompt);
        this.Controls.Add(bottomPanel);

        Panel topContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 18, 24),
            Padding = new Padding(20)
        };
        this.Controls.Add(topContainer);

        logoBox = new PictureBox
        {
            Location = new Point(25, 25),
            Size = new Size(270, 270),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        topContainer.Controls.Add(logoBox);

        consoleBox = new RichTextBox
        {
            Location = new Point(320, 20),
            Size = new Size(560, 360),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(18, 18, 24),
            ForeColor = Color.White,
            Font = new Font("Consolas", 10.5f),
            BorderStyle = BorderStyle.None,
            ReadOnly = true
        };
        topContainer.Controls.Add(consoleBox);

        WyswietlFastfetch();
        _ = ZaladujLogoZGitHubaAsync(false);
    }

    private async Task ZaladujLogoZGitHubaAsync(bool wymusPobranie)
    {
        if (!Directory.Exists(repoPath)) Directory.CreateDirectory(repoPath);
        string cacheFile = Path.Combine(repoPath, "logo.png");

        try
        {
            if (wymusPobranie || !File.Exists(cacheFile))
            {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "HobbyOS-Fastfetch");

                foreach (string url in githubLogoUrls)
                {
                    try
                    {
                        byte[] data = await client.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(cacheFile, data);
                        break;
                    }
                    catch { }
                }
            }

            if (File.Exists(cacheFile))
            {
                using FileStream fs = new FileStream(cacheFile, FileMode.Open, FileAccess.Read);
                using Bitmap raw = new Bitmap(fs);
                Bitmap transparent = new Bitmap(raw.Width, raw.Height);

                for (int y = 0; y < raw.Height; y++)
                {
                    for (int x = 0; x < raw.Width; x++)
                    {
                        Color c = raw.GetPixel(x, y);
                        if (c.R > 235 && c.G > 235 && c.B > 235)
                            transparent.SetPixel(x, y, Color.Transparent);
                        else
                            transparent.SetPixel(x, y, c);
                    }
                }

                logoBox.Image?.Dispose();
                logoBox.Image = transparent;
            }
        }
        catch { }
    }

    private async void HandleCommand(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;

        string cmd = inputBox.Text.Trim().ToLower();
        inputBox.Clear();

        AppendColor($"\nadmin@hobby-os:~$ {cmd}\n", Color.LimeGreen, true);

        if (cmd == "fastfetch" || cmd == "neofetch")
        {
            consoleBox.Clear();
            WyswietlFastfetch();
            await ZaladujLogoZGitHubaAsync(true);
        }
        else if (cmd == "clear")
        {
            consoleBox.Clear();
        }
        else if (cmd == "help")
        {
            AppendColor("Dostępne komendy: fastfetch, clear, help, exit\n", Color.LightGray, false);
        }
        else if (cmd == "exit")
        {
            this.Close();
        }
        else if (!string.IsNullOrEmpty(cmd))
        {
            AppendColor($"bash: {cmd}: nie znaleziono polecenia (wpisz 'help')\n", Color.IndianRed, false);
        }
    }

    private void WyswietlFastfetch()
    {
        string user = Environment.UserName;
        string host = Environment.MachineName;
        TimeSpan up = TimeSpan.FromMilliseconds(Environment.TickCount64);

        AppendColor($"{user}@{host}\n", Color.FromArgb(255, 85, 0), true);
        AppendColor("-------------------------------------\n", Color.Gray, false);

        DodajLinie("OS", "HobbyOS // Phoenix Edition");
        DodajLinie("Kernel", RuntimeInformation.OSDescription);
        DodajLinie("Uptime", $"{up.Days}d {up.Hours}h {up.Minutes}m");
        DodajLinie("Shell", "HobbyBash 1.0 (.NET " + Environment.Version + ")");
        DodajLinie("Resolution", $"{Screen.PrimaryScreen?.Bounds.Width}x{Screen.PrimaryScreen?.Bounds.Height}");
        DodajLinie("CPU", $"{PobierzProcesor()} ({Environment.ProcessorCount} threads)");
        DodajLinie("Disk (C:)", PobierzDysk());
        DodajLinie("Logo Source", "GitHub (fortunanikodem00-a11y/hobby-os)");
        DodajLinie("Theme", "Phoenix Dark [HobbyOS]");

        AppendColor("\n", Color.White, false);
        Color[] palette = { Color.Black, Color.IndianRed, Color.LimeGreen, Color.Gold, Color.DodgerBlue, Color.Magenta, Color.Cyan, Color.White };
        foreach (Color c in palette)
        {
            consoleBox.SelectionStart = consoleBox.TextLength;
            consoleBox.SelectionBackColor = c;
            consoleBox.SelectedText = "   ";
        }
        consoleBox.SelectionBackColor = consoleBox.BackColor;
        AppendColor("\n", Color.White, false);
    }

    private void DodajLinie(string klucz, string wartosc)
    {
        AppendColor(klucz + ": ", Color.FromArgb(255, 140, 0), true);
        AppendColor(wartosc + "\n", Color.WhiteSmoke, false);
    }

    private void AppendColor(string text, Color color, bool bold)
    {
        consoleBox.SelectionStart = consoleBox.TextLength;
        consoleBox.SelectionLength = 0;
        consoleBox.SelectionColor = color;
        consoleBox.SelectionFont = new Font(consoleBox.Font, bold ? FontStyle.Bold : FontStyle.Regular);
        consoleBox.AppendText(text);
        consoleBox.SelectionColor = consoleBox.ForeColor;
    }

    private string PobierzProcesor()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? RuntimeInformation.ProcessArchitecture.ToString();
        }
        catch { return RuntimeInformation.ProcessArchitecture.ToString(); }
    }

    private string PobierzDysk()
    {
        try
        {
            DriveInfo d = new DriveInfo("C");
            double freeGb = Math.Round(d.AvailableFreeSpace / 1073741824.0, 1);
            double totalGb = Math.Round(d.TotalSize / 1073741824.0, 1);
            double usedGb = Math.Round(totalGb - freeGb, 1);
            return $"{usedGb} GiB / {totalGb} GiB";
        }
        catch { return "Nieznany"; }
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new HobbyFastfetch());
    }
}
