using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

Application.EnableVisualStyles();

Form f = new Form();
f.Text = "Spy-D OS // Gnome Tactical Desktop Suite";
f.Size = new Size(1150, 780);
f.BackColor = Color.FromArgb(14, 16, 20);
f.StartPosition = FormStartPosition.CenterScreen;
f.FormBorderStyle = FormBorderStyle.FixedSingle;
f.MaximizeBox = false;

string configPath = "spy_desktop_positions.cfg";
Dictionary<string, Point> positions = new Dictionary<string, Point>();
Dictionary<string, string> iconPaths = new Dictionary<string, string>();

bool isDragging = false;
Point startMousePos = Point.Empty;
string selectedIcon = "";

if (File.Exists(configPath))
{
    try
    {
        string[] ln = File.ReadAllLines(configPath);
        foreach (string l in ln)
        {
            string[] tk = l.Split(',');
            if (tk.Length == 4)
            {
                positions[tk[0]] = new Point(int.Parse(tk[1]), int.Parse(tk[2]));
                if (tk[3] != "none") iconPaths[tk[0]] = tk[3];
            }
        }
    }
    catch { }
}

if (positions.Count == 0)
{
    positions["pobieracz.cs"] = new Point(60, 60);
}

Panel topBar = new Panel { Size = new Size(1150, 40), Location = new Point(0, 0), BackColor = Color.FromArgb(24, 26, 32) };
f.Controls.Add(topBar);

Label lblLogo = new Label { Text = "🔒 SPY-D SHELL // REDIRECTION GRAPHICS ENGINE", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Lime, Location = new Point(15, 10), Size = new Size(450, 20) };
topBar.Controls.Add(lblLogo);

Panel pnlMagazyn = new Panel { Size = new Size(320, 600), Location = new Point(800, 50), BackColor = Color.FromArgb(25, 28, 35), Visible = false, BorderStyle = BorderStyle.FixedSingle };
f.Controls.Add(pnlMagazyn);

Button btnMagazyn = new Button { Text = "APLIKACJE", Location = new Point(850, 6), Size = new Size(250, 28), BackColor = Color.FromArgb(40, 45, 55), ForeColor = Color.Cyan, FlatStyle = FlatStyle.Flat };
btnMagazyn.Click += (s, e) => pnlMagazyn.Visible = !pnlMagazyn.Visible;
topBar.Controls.Add(btnMagazyn);

Panel pnlPulpit = new Panel { Size = new Size(1150, 700), Location = new Point(0, 40), BackColor = Color.Transparent };
f.Controls.Add(pnlPulpit);

ContextMenuStrip menuPulpitu = new ContextMenuStrip();
menuPulpitu.Items.Add("Nowa aplikacja .cs", null, (s, e) => {
    string nowaApka = "apka_" + (positions.Count + 1) + ".cs";
    positions[nowaApka] = new Point(150, 150);
    
    try
    {
        List<string> ln = new List<string>();
        foreach (var pos in positions)
        {
            string iPath = iconPaths.ContainsKey(pos.Key) ? iconPaths[pos.Key] : "none";
            ln.Add(pos.Key + "," + pos.Value.X + "," + pos.Value.Y + "," + iPath);
        }
        File.WriteAllLines(configPath, ln.ToArray());
    }
    catch { }
    
    Application.Restart();
});
pnlPulpit.ContextMenuStrip = menuPulpitu;

ContextMenuStrip menuIkony = new ContextMenuStrip();
var itemRun = menuIkony.Items.Add("Uruchom");
var itemProp = menuIkony.Items.Add("Wlasciwosci");
var itemDel = menuIkony.Items.Add("Usun");

foreach (var element in positions)
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

    Label lbl = new Label { Text = nazwaApki, ForeColor = Color.LightGreen, Location = new Point(5, 70), Size = new Size(110, 35), Tag = nazwaApki, TextAlign = ContentAlignment.TopCenter };

    pnlIkona.Controls.Add(pic);
    pnlIkona.Controls.Add(lbl);

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
            try
            {
                List<string> ln = new List<string>();
                foreach (var pos in positions)
                {
                    string iPath = iconPaths.ContainsKey(pos.Key) ? iconPaths[pos.Key] : "none";
                    ln.Add(pos.Key + "," + pos.Value.X + "," + pos.Value.Y + "," + iPath);
                }
                File.WriteAllLines(configPath, ln.ToArray());
            }
            catch { }
        }
    };

    pic.MouseEnter += (s, e) => selectedIcon = nazwaApki;
    lbl.MouseEnter += (s, e) => selectedIcon = nazwaApki;

    // PRZEKIEROWANIE SYGNAŁU NA TWÓJ KOD Z ZAŁĄCZONEGO PLIKU (BEZ FUNKCJI REKURENCJI)
    Action launchAction = () => {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string tempProjectDir = Path.Combine(Path.GetTempPath(), "HobbyOSSub_" + nazwaApki.Replace(".", "_"));
        if (Directory.Exists(tempProjectDir)) Directory.Delete(tempProjectDir, true);
        Directory.CreateDirectory(tempProjectDir);

        string csproj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms></PropertyGroup></Project>";
        File.WriteAllText(Path.Combine(tempProjectDir, "SubApp.csproj"), csproj);
        File.Copy(Path.Combine(baseDir, "src_repo", nazwaApki), Path.Combine(tempProjectDir, "Program.cs"), true);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "dotnet", Arguments = "run --project " + "\"" + tempProjectDir + "\"", CreateNoWindow = true, UseShellExecute = false });
    };

    pic.DoubleClick += (s, e) => launchAction();
    lbl.DoubleClick += (s, e) => launchAction();

    pnlPulpit.Controls.Add(pnlIkona);
}

Button btnInstaluj = new Button { Text = "Instaluj: pobieracz.cs", Location = new Point(15, 60), Size = new Size(280, 40), BackColor = Color.FromArgb(45, 48, 55), ForeColor = Color.LightBlue, FlatStyle = FlatStyle.Flat };
btnInstaluj.Click += (s, e) => {
    if (!positions.ContainsKey("pobieracz.cs")) {
        positions["pobieracz.cs"] = new Point(100, 150);
        Application.Restart();
    }
};
pnlMagazyn.Controls.Add(btnInstaluj);

itemRun.Click += (s, e) => {
    if (!string.IsNullOrEmpty(selectedIcon)) {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string tempProjectDir = Path.Combine(Path.GetTempPath(), "HobbyOSSub_" + selectedIcon.Replace(".", "_"));
        if (Directory.Exists(tempProjectDir)) Directory.Delete(tempProjectDir, true);
        Directory.CreateDirectory(tempProjectDir);
        string csproj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net10.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms></PropertyGroup></Project>";
        File.WriteAllText(Path.Combine(tempProjectDir, "SubApp.csproj"), csproj);
        File.Copy(Path.Combine(baseDir, "src_repo", selectedIcon), Path.Combine(tempProjectDir, "Program.cs"), true);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "dotnet", Arguments = "run --project " + "\"" + tempProjectDir + "\"", CreateNoWindow = true, UseShellExecute = false });
    }
};

itemProp.Click += (s, e) => {
    if (!string.IsNullOrEmpty(selectedIcon)) {
        using (OpenFileDialog ofd = new OpenFileDialog()) {
            ofd.Filter = "Obrazy|*.png;*.jpg";
            if (ofd.ShowDialog() == DialogResult.OK) {
                iconPaths[selectedIcon] = ofd.FileName;
                Application.Restart();
            }
        }
    }
};

itemDel.Click += (s, e) => {
    if (!string.IsNullOrEmpty(selectedIcon)) {
        positions.Remove(selectedIcon);
        Application.Restart();
    }
};

f.ShowDialog();
