using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public class Program
{
    private static Panel pnlPulpit = null!;
    private static ContextMenuStrip menuIkony = null!;
    private static Dictionary<string, Point> positions = new Dictionary<string, Point>();
    private static bool isDragging = false;
    private static Point startMousePos;
    private static string selectedIcon = "";
    private static int obecnyScrollY = 0;
    private static int krokScrolla = 80;
    private static string folderRepo = "";

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Form f = new Form 
        { 
            Text = "Spy-D OS // Gnome Tactical Desktop Suite", 
            Size = new Size(1150, 780), 
            BackColor = Color.FromArgb(11, 12, 15), 
            StartPosition = FormStartPosition.CenterScreen, 
            FormBorderStyle = FormBorderStyle.FixedSingle, 
            MaximizeBox = false, 
            KeyPreview = true 
        };

        // Górny pasek Gnome
        Panel topBar = new Panel { Size = new Size(1150, 35), Location = new Point(0, 0), BackColor = Color.FromArgb(18, 18, 22) };
        f.Controls.Add(topBar);
        topBar.Controls.Add(new Label { Text = "🔒 GNOME SHELL CORE // LIVE FILE-SYSTEM ACTIVE (Strzałki GÓRA/DÓŁ przewijają)", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(240, 240, 240), Location = new Point(15, 9), Size = new Size(650, 20) });

        // Boczny Ubuntu Dock
        Panel dock = new Panel { Size = new Size(70, 745), Location = new Point(0, 35), BackColor = Color.FromArgb(16, 17, 20) };
        f.Controls.Add(dock);
        dock.Controls.Add(new Button { Size = new Size(50, 50), Location = new Point(10, 20), BackColor = Color.FromArgb(35, 38, 45), FlatStyle = FlatStyle.Flat, Text = "⣿", Font = new Font("Segoe UI", 16), ForeColor = Color.Orange });

        // Obszar roboczy pulpitu
        pnlPulpit = new Panel { Size = new Size(1080, 710), Location = new Point(70, 35), BackColor = Color.Transparent };
        f.Controls.Add(pnlPulpit);

        menuIkony = new ContextMenuStrip();
        menuIkony.Items.Add("Uruchom", null, (s, e) => OdpalProcesSystemowy(selectedIcon));

        // Przewijanie strzałkami góra/dół
        f.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Down) obecnyScrollY -= krokScrolla;
            else if (e.KeyCode == Keys.Up && obecnyScrollY < 0) obecnyScrollY += krokScrolla;
            
            foreach (Control c in pnlPulpit.Controls)
            {
                if (c.Tag != null && positions.ContainsKey(c.Tag.ToString()!))
                {
                    c.Top = positions[c.Tag.ToString()!].Y + obecnyScrollY;
                }
            }
        };

        // Bezpieczne wyszukiwanie folderu src_repo
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        folderRepo = Path.Combine(baseDir, "src_repo");

        if (!Directory.Exists(folderRepo))
        {
            string uzytkownikPath = Path.Combine("C:\\Users", Environment.UserName, "source\\repos");
            if (Directory.Exists(uzytkownikPath))
            {
                string? znaleziony = Directory.GetDirectories(uzytkownikPath, "src_repo", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(znaleziony)) folderRepo = znaleziony;
            }
        }

        if (!Directory.Exists(folderRepo)) Directory.CreateDirectory(folderRepo);

        // Skanowanie plików .cs
        List<string> pliki = Directory.GetFiles(folderRepo, "*.cs").ToList();

        if (pliki.Count == 0)
        {
            pnlPulpit.Controls.Add(new Label { Text = "📂 [ Repozytorium src_repo puste ]\nWgraj skrypty .cs, aby automatycznie wyrenderować ikony.", ForeColor = Color.FromArgb(100, 110, 120), Font = new Font("Consolas", 10, FontStyle.Italic), Location = new Point(50, 50), Size = new Size(600, 50) });
        }

        int i = 0;
        foreach (string sciezka in pliki)
        {
            string nazwa = Path.GetFileName(sciezka);
            if (!positions.ContainsKey(nazwa))
            {
                positions[nazwa] = new Point(40 + ((i % 6) * 145), 40 + ((i / 6) * 135));
            }

            Panel pnl = new Panel { Size = new Size(115, 105), Location = positions[nazwa], BackColor = Color.FromArgb(24, 25, 30), Tag = nazwa, ContextMenuStrip = menuIkony };
            Panel pic = new Panel { Size = new Size(46, 46), Location = new Point(34, 15), BackColor = Color.FromArgb(40, 42, 50), Tag = nazwa };
            Label lbl = new Label { Text = nazwa, ForeColor = Color.FromArgb(220, 220, 220), Location = new Point(2, 68), Size = new Size(111, 32), TextAlign = ContentAlignment.TopCenter, Font = new Font("Segoe UI", 8), Tag = nazwa };
            
            pnl.Controls.Add(pic);
            pnl.Controls.Add(lbl);
            pnlPulpit.Controls.Add(pnl);

            // Przeciąganie ikon
            pic.MouseDown += (s, e) => { isDragging = true; startMousePos = e.Location; selectedIcon = nazwa; };
            pic.MouseMove += (s, e) => { if (isDragging && selectedIcon == nazwa) { pnl.Left += e.X - startMousePos.X; pnl.Top += e.Y - startMousePos.Y; } };
            pic.MouseUp += (s, e) => { isDragging = false; positions[nazwa] = new Point(pnl.Left, pnl.Top - obecnyScrollY); };
            pic.MouseEnter += (s, e) => { selectedIcon = nazwa; pnl.BackColor = Color.FromArgb(34, 36, 44); };
            pic.MouseLeave += (s, e) => pnl.BackColor = Color.FromArgb(24, 25, 30);

            // Bezpośrednie otwieranie po dwukliku
            pic.DoubleClick += (s, e) => OdpalProcesSystemowy(nazwa);
            lbl.DoubleClick += (s, e) => OdpalProcesSystemowy(nazwa);

            i++;
        }

        Application.Run(f);
    }

    private static void OdpalProcesSystemowy(string nazwaSkryptu)
    {
        if (string.IsNullOrEmpty(nazwaSkryptu)) return;
        try
        {
            string pelnaSciezka = Path.Combine(folderRepo, nazwaSkryptu);
            if (!File.Exists(pelnaSciezka)) return;

            string nazwaBezRozszerzenia = Path.GetFileNameWithoutExtension(nazwaSkryptu);
            string tempAppDir = Path.Combine(Path.GetTempPath(), "HobbyOSApps", "SubApp_" + nazwaBezRozszerzenia);

            if (Directory.Exists(tempAppDir))
            {
                try { Directory.Delete(tempAppDir, true); } catch { }
            }
            Directory.CreateDirectory(tempAppDir);

            string csprojContent = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>";
            File.WriteAllText(Path.Combine(tempAppDir, "SubApp.csproj"), csprojContent);
            File.Copy(pelnaSciezka, Path.Combine(tempAppDir, "Program.cs"), true);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{tempAppDir}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Błąd uruchamiania: " + ex.Message, "Spy-D OS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
