using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class SpyDesktopApplication
{
    private static Panel pnlPulpit = null!;
    private static Panel pnlMagazyn = null!;
    private static ContextMenuStrip menuPulpitu = null!;
    private static ContextMenuStrip menuIkony = null!;
    
    private static string configPath = "spy_desktop_positions.cfg";
    private static Dictionary<string, Point> positions = new Dictionary<string, Point>();
    private static Dictionary<string, string> iconPaths = new Dictionary<string, string>();

    private static bool isDragging = false;
    private static Point startMousePos;
    private static string selectedIcon = "";

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        
        Form f = new Form();
        f.Text = "Spy-D OS // Gnome Advanced Tactical Suite v15.0";
        f.Size = new Size(1150, 780);
        f.BackColor = Color.FromArgb(14, 16, 20);
        f.StartPosition = FormStartPosition.CenterScreen;
        f.FormBorderStyle = FormBorderStyle.FixedSingle;
        f.MaximizeBox = false;

        WczytajKonfiguracje();

        // 1. TOP BAR (Pasek Gnome)
        Panel topBar = new Panel { Size = new Size(1150, 40), Location = new Point(0, 0), BackColor = Color.FromArgb(24, 26, 32) };
        f.Controls.Add(topBar);

        Label lblLogo = new Label { Text = "🔒 SPY-D SHELL // WYKONALNY SILNIK PROCESOWY", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Lime, Location = new Point(15, 10), Size = new Size(450, 20) };
        topBar.Controls.Add(lblLogo);

        Button btnMagazyn = new Button { Text = "🏪 MAGAZYN APLIKACJI (.CS)", Location = new Point(850, 6), Size = new Size(250, 28), BackColor = Color.FromArgb(40, 45, 55), ForeColor = Color.Cyan, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        btnMagazyn.FlatAppearance.BorderColor = Color.Cyan;
        btnMagazyn.Click += (s, e) => pnlMagazyn.Visible = !pnlMagazyn.Visible;
        topBar.Controls.Add(btnMagazyn);

        // 2. GŁÓWNY OBSZAR PULPITU
        pnlPulpit = new Panel { Size = new Size(1150, 700), Location = new Point(0, 40), BackColor = Color.Transparent };
        f.Controls.Add(pnlPulpit);

        // 3. MENU POD PRAWYM PRZYCISKIEM (PULPIT)
        menuPulpitu = new ContextMenuStrip();
        menuPulpitu.Items.Add("Nowa aplikacja .cs", null, (s, e) => {
            string nowaApka = "apka_" + (positions.Count + 1) + ".cs";
            positions[nowaApka] = new Point(150, 150);
            ZapiszKonfiguracje();
            OdswiezGnomePulpit();
        });
        pnlPulpit.ContextMenuStrip = menuPulpitu;

        // 4. MENU POD PRAWYM PRZYCISKIEM (IKONA)
        menuIkony = new ContextMenuStrip();
        menuIkony.Items.Add("▶ Uruchom aplikację", null, (s, e) => UruchomZaznaczonaApke());
        menuIkony.Items.Add("🛠️ Właściwości i Ikona", null, (s, e) => OtworzWlasciwosciApki());
        menuIkony.Items.Add("❌ Usuń z pulpitu", null, (s, e) => UsunZaznaczonaApke());

        // 5. PANEL BOCZNY: MAGAZYN APLIKACJI
        pnlMagazyn = new Panel { Size = new Size(320, 600), Location = new Point(800, 50), BackColor = Color.FromArgb(25, 28, 35), Visible = false, BorderStyle = BorderStyle.FixedSingle };
        f.Controls.Add(pnlMagazyn);
        pnlMagazyn.BringToFront();

        Label lblMagazynTytul = new Label { Text = "DOSTĘPNE REPOZYTORIA KODU:", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White, Location = new Point(15, 15), Size = new Size(280, 20) };
        pnlMagazyn.Controls.Add(lblMagazynTytul);

        StworzElementMagazynu("pobieracz.cs", 60, Color.LightBlue);
        StworzElementMagazynu("skaner_ip.cs", 120, Color.Orange);

        if (positions.Count == 0)
        {
            positions["pobieracz.cs"] = new Point(60, 60);
        }

        OdswiezGnomePulpit();
        f.ShowDialog();
    }

    private static void StworzElementMagazynu(string nazwa, int y, Color k)
    {
        Button btn = new Button { Text = "Zainstaluj: " + nazwa, Location = new Point(15, y), Size = new Size(280, 40), BackColor = Color.FromArgb(45, 48, 55), ForeColor = k, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btn.FlatAppearance.BorderColor = k;
        btn.Click += (s, e) => {
            if (!positions.ContainsKey(nazwa)) {
                positions[nazwa] = new Point(100 + (positions.Count * 40), 150);
                ZapiszKonfiguracje();
                OdswiezGnomePulpit();
            }
        };
        pnlMagazyn.Controls.Add(btn);
    }

    private static void OdswiezGnomePulpit()
    {
        pnlPulpit.Controls.Clear();

        foreach (KeyValuePair<string, Point> element in positions)
        {
            string nazwaApki = element.Key;
            Point pozycja = element.Value;

            Panel pnlIkona = new Panel { Size = new Size(120, 110), Location = pozycja, BackColor = Color.FromArgb(30, 36, 45), BorderStyle = BorderStyle.FixedSingle, Tag = nazwaApki };
            pnlIkona.ContextMenuStrip = menuIkony;

            PictureBox pic = new PictureBox { Size = new Size(48, 48), Location = new Point(36, 15), BackColor = Color.FromArgb(45, 55, 70), SizeMode = PictureBoxSizeMode.StretchImage, Tag = nazwaApki };
            if (iconPaths.ContainsKey(nazwaApki) && File.Exists(iconPaths[nazwaApki]))
            {
                try { pic.Image = Image.FromFile(iconPaths[nazwaApki]); } catch { }
            }

            Label lbl = new Label { Text = nazwaApki, ForeColor = Color.LightGreen, Font = new Font("Consolas", 8, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter, Location = new Point(5, 70), Size = new Size(110, 35), Tag = nazwaApki };

            pnlIkona.Controls.Add(pic);
            pnlIkona.Controls.Add(lbl);

            // DETEKCJA MYSZY I PRZECIĄGANIE
            pic.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; startMousePos = e.Location; selectedIcon = nazwaApki; } };
            pic.MouseMove += (s, e) => {
                if (isDragging && selectedIcon == nazwaApki) {
                    pnlIkona.Left += e.X - startMousePos.X;
                    pnlIkona.Top += e.Y - startMousePos.Y;
                }
            };
            pic.MouseUp += (s, e) => {
                if (isDragging) {
                    isDragging = false;
                    positions[nazwaApki] = pnlIkona.Location;
                    ZapiszKonfiguracje();
                }
            };

            pic.MouseEnter += (s, e) => selectedIcon = nazwaApki;
            lbl.MouseEnter += (s, e) => selectedIcon = nazwaApki;

            // AKTYWACJA DWUKROTNEGO KLIKNIĘCIA
            pic.DoubleClick += (s, e) => KompilujAndRunRealApp(nazwaApki);
            lbl.DoubleClick += (s, e) => KompilujAndRunRealApp(nazwaApki);

            pnlPulpit.Controls.Add(pnlIkona);
        }
    }

    // ⚡ PRAWDZIWY, ROZBUDOWANY INTERFEJS URUCHAMIANIA PROCESÓW PRZEZ DOTNET RUN SDK
    private static void KompilujAndRunRealApp(string plik)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string sciezkaPlikuCs = Path.Combine(baseDir, "src_repo", plik);

        // Jeśli plik nie istnieje (np. dodany z poziomu menu), system generuje dla niego natywne, czyste okno wejściowe C#
        if (!File.Exists(sciezkaPlikuCs))
        {
            string szablonKodu = "using System; using System.Windows.Forms; class Program { [STAThread] static void Main() { Application.EnableVisualStyles(); Form f = new Form { Text = \"HobbyOS Program: " + plik + "\", Size = new System.Drawing.Size(400,250) }; f.ShowDialog(); } }";
            File.WriteAllText(sciezkaPlikuCs, szablonKodu);
        }

        try
        {
            // Tworzenie odizolowanego katalogu kompilacji dla aplikacji deweloperskiej
            string tempProjectDir = Path.Combine(baseDir, "HobbyOS_SubProcess_" + plik.Replace(".", "_"));
            if (Directory.Exists(tempProjectDir)) Directory.Delete(tempProjectDir, true);
            Directory.CreateDirectory(tempProjectDir);

            // Wygenerowanie rzeczywistego pliku struktury projektu .NET Core .csproj
            string csproj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms></PropertyGroup></Project>";
            File.WriteAllText(Path.Combine(tempProjectDir, "SubApp.csproj"), csproj);
            
            // Fizyczne sklonowanie kodu źródłowego do struktury kompilatora
            File.Copy(sciezkaPlikuCs, Path.Combine(tempProjectDir, "Program.cs"), true);

            // URUCHOMIENIE AUTENTYCZNEGO PROCESU SYSTEMOWEGO PRZEZ SDK KOMPUTERA
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project " + "\"" + tempProjectDir + "\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Krytyczny błąd jądra SDK podczas kompilacji procesu: " + ex.Message, "HobbyOS Engine Exception");
        }
    }

    private static void OtworzWlasciwosciApki()
    {
        if (string.IsNullOrEmpty(selectedIcon)) return;
        Form propForm = new Form { Text = "Właściwości: " + selectedIcon, Size = new Size(450, 300), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(30,30,35) };
        Button btnWgraj = new Button { Text = "WYBIERZ NOWĄ GRAFIKĘ IKONY (.PNG/.JPG)", Location = new Point(20, 60), Size = new Size(380, 40), ForeColor = Color.Cyan, FlatStyle = FlatStyle.Flat };
        
        btnWgraj.Click += (s, e) => {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
ofd.Filter = "Obrazy|.png;.jpg";
if (ofd.ShowDialog() == DialogResult.OK) {
iconPaths[selectedIcon] = ofd.FileName;
ZapiszKonfiguracje();
OdswiezGnomePulpit();
propForm.Close();
}
}
};
propForm.Controls.Add(btnWgraj);
propForm.ShowDialog();
}
private static void UruchomZaznaczonaApke() { if (!string.IsNullOrEmpty(selectedIcon)) KompilujAndRunRealApp(selectedIcon); }
private static void UsunZaznaczonaApke() { if (!string.IsNullOrEmpty(selectedIcon)) { positions.Remove(selectedIcon); ZapiszKonfiguracje(); OdswiezGnomePulpit(); } }
private static void ZapiszKonfiguracje()
{
try {
List ln = new List();
foreach (KeyValuePair<string, Point> pos in positions) {
string iPath = iconPaths.ContainsKey(pos.Key) ? iconPaths[pos.Key] : "none";
ln.Add(pos.Key + "," + pos.Value.X + "," + pos.Value.Y + "," + iPath);
}
File.WriteAllLines(configPath, ln.ToArray());
} catch { }
}
private static void WczytajKonfiguracje()
{
try {
if (File.Exists(configPath)) {
string[] ln = File.ReadAllLines(configPath);
positions.Clear(); iconPaths.Clear();
foreach (string l in ln) {
string[] tk = l.Split(',');
if (tk.Length == 4) {
positions[tk] = new Point(int.Parse(tk), int.Parse(tk));
if (tk != "none") iconPaths[tk] = tk;
}
}
}
} catch { }
}
}
