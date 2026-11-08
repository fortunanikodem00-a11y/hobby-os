using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Form f = new Form { Text = "Spy-D OS // Gnome Tactical Desktop Suite", Size = new Size(1150, 780), BackColor = Color.FromArgb(11, 12, 15), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedSingle, MaximizeBox = false, KeyPreview = true };

        Panel topBar = new Panel { Size = new Size(1150, 35), Location = new Point(0, 0), BackColor = Color.FromArgb(18, 18, 22) };
        f.Controls.Add(topBar);
        topBar.Controls.Add(new Label { Text = "🔒 GNOME SHELL CORE // LIVE FILE-SYSTEM ACTIVE", Font = new Font("Ubuntu", 9, FontStyle.Bold), ForeColor = Color.FromArgb(240, 240, 240), Location = new Point(15, 10), Size = new Size(500, 20) });

        Panel dock = new Panel { Size = new Size(70, 745), Location = new Point(0, 35), BackColor = Color.FromArgb(16, 17, 20) };
        f.Controls.Add(dock);
        dock.Controls.Add(new Button { Size = new Size(50, 50), Location = new Point(10, 20), BackColor = Color.FromArgb(35, 38, 45), FlatStyle = FlatStyle.Flat, Text = "⣿", Font = new Font("Segoe UI", 16), ForeColor = Color.Orange });

        pnlPulpit = new Panel { Size = new Size(1080, 710), Location = new Point(70, 35), BackColor = Color.Transparent };
        f.Controls.Add(pnlPulpit);

        menuIkony = new ContextMenuStrip();
        menuIkony.Items.Add("Uruchom", null, (s, e) => OdpalProcesSystemowy(selectedIcon));

        f.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Down) obecnyScrollY -= krokScrolla;
            else if (e.KeyCode == Keys.Up && obecnyScrollY < 0) obecnyScrollY += krokScrolla;
            foreach (Control c in pnlPulpit.Controls) if (c.Tag != null && positions.ContainsKey(c.Tag.ToString())) c.Top = positions[c.Tag.ToString()].Y + obecnyScrollY;
        };

        // ⚡ AUTOMATYCZNE, BEZBŁĘDNE SZUKANIE FOLDERU SRC_REPO OD KATALOGU PROJEKTU UŻYTKOWNIKA
        string folderRepo = "";
        string uzytkownikPath = Path.Combine("C:\\Users", Environment.UserName, "source\\repos");
        
        if (Directory.Exists(uzytkownikPath))
        {
            string[] szukanie = Directory.GetDirectories(uzytkownikPath, "src_repo", SearchOption.AllDirectories);
            if (szukanie.Length > 0)
            {
                folderRepo = szukanie[0];
            }
        }

        // Zabezpieczenie ratunkowe, jeśli folder nie został znaleziony wyżej
        if (string.IsNullOrEmpty(folderRepo) || !Directory.Exists(folderRepo))
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            folderRepo = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\src_repo"));
            if (!Directory.Exists(folderRepo)) folderRepo = Path.Combine(baseDir, "src_repo");
        }

        if (!Directory.Exists(folderRepo)) Directory.CreateDirectory(folderRepo);
        string[] pliki = Directory.GetFiles(folderRepo, "*.cs");

        if (pliki.Length == 0) pnlPulpit.Controls.Add(new Label { Text = "📂 [ Repozytorium src_repo puste ]\nWgraj skrypty .cs, aby automatycznie wyrenderować ikony.", ForeColor = Color.FromArgb(100, 110, 120), Font = new Font("Consolas", 10, FontStyle.Italic), Location = new Point(50, 50), Size = new Size(600, 50) });

        for (int i = 0; i < pliki.Length; i++)
        {
            string nazwa = Path.GetFileName(pliki[i]);
            if (!positions.ContainsKey(nazwa)) positions[nazwa] = new Point(40 + ((i % 6) * 145), 40 + ((i / 6) * 135));

            Panel pnl = new Panel { Size = new Size(115, 105), Location = positions[nazwa], BackColor = Color.FromArgb(24, 25, 30), Tag = nazwa, ContextMenuStrip = menuIkony };
            Panel pic = new Panel { Size = new Size(46, 46), Location = new Point(34, 15), BackColor = Color.FromArgb(40, 42, 50), Tag = nazwa };
            Label lbl = new Label { Text = nazwa, ForeColor = Color.FromArgb(220, 220, 220), Location = new Point(2, 68), Size = new Size(111, 32), TextAlign = ContentAlignment.TopCenter, Font = new Font("Segoe UI", 8), Tag = nazwa };
            pnl.Controls.Add(pic); pnl.Controls.Add(lbl); pnlPulpit.Controls.Add(pnl);

            pic.MouseDown += (s, e) => { isDragging = true; startMousePos = e.Location; selectedIcon = nazwa; };
            pic.MouseMove += (s, e) => { if (isDragging && selectedIcon == nazwa) { pnl.Left += e.X - startMousePos.X; pnl.Top += e.Y - startMousePos.Y; } };
            pic.MouseUp += (s, e) => { isDragging = false; positions[nazwa] = new Point(pnl.Left, pnl.Top - obecnyScrollY); };
            pic.MouseEnter += (s, e) => { selectedIcon = nazwa; pnl.BackColor = Color.FromArgb(34, 36, 44); };
            pic.MouseLeave += (s, e) => pnl.BackColor = Color.FromArgb(24, 25, 30);

            pic.DoubleClick += (s, e) => OdpalProcesSystemowy(folderRepo, nazwa);
            lbl.DoubleClick += (s, e) => OdpalProcesSystemowy(folderRepo, nazwa);
        }

        Application.Run(f);
    }

    private static void OdpalProcesSystemowy(string sciezkaRepo, string nazwaSkryptu)
    {
        if (string.IsNullOrEmpty(nazwaSkryptu)) return;
        try
        {
            string pelnaSciezka = Path.Combine(sciezkaRepo, nazwaSkryptu);

            if (File.Exists(pelnaSciezka))
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "dotnet";
                psi.Arguments = "run -- " + "\"" + pelnaSciezka + "\"";
                psi.CreateNoWindow = false;
                psi.UseShellExecute = true;

                System.Diagnostics.Process.Start(psi);
            }
        }
        catch { }
    }
}
