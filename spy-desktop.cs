using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// PŁASKA STRUKTURA BEZ KLAS I BEZ METODY MAIN - ODPORNA NA BŁĘDY SKŁADNI
Application.EnableVisualStyles();

Form f = new Form();
f.Text = "Spy-D OS // Gnome Advanced Tactical Suite v12.0";
f.Size = new Size(1150, 780);
f.BackColor = Color.FromArgb(12, 15, 12);
f.StartPosition = FormStartPosition.CenterScreen;
f.FormBorderStyle = FormBorderStyle.FixedSingle;
f.MaximizeBox = false;

string cfgFile = "spy_desktop_pos.cfg";
Dictionary<string, Point> positions = new Dictionary<string, Point>();
Dictionary<string, string> iconPaths = new Dictionary<string, string>();

bool isDragging = false;
Point startMousePos = Point.Empty;
string selectedIcon = "";

// Wczytywanie bezpiecznej konfiguracji
if (File.Exists(cfgFile))
{
    string[] lines = File.ReadAllLines(cfgFile);
    foreach (string l in lines)
    {
        string[] tk = l.Split(' ');
        if (tk.Length == 4)
        {
            positions[tk[0]] = new Point(int.Parse(tk[1]), int.Parse(tk[2]));
            iconPaths[tk[0]] = tk[3];
        }
    }
}

if (positions.Count == 0)
{
    positions["pobieracz.cs"] = new Point(60, 60);
    positions["pulpit_mac.cs"] = new Point(60, 200);
    iconPaths["pobieracz.cs"] = "default";
    iconPaths["pulpit_mac.cs"] = "default";
}

// 1. GNOME TOP BAR
Panel topBar = new Panel { Size = new Size(1150, 40), Location = new Point(0, 0), BackColor = Color.FromArgb(22, 26, 22) };
f.Controls.Add(topBar);

Label lblLogo = new Label { Text = "🔒 SPY-D SHELL // PRODUCTION ROOT", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Lime, Location = new Point(15, 10), Size = new Size(350, 20) };
topBar.Controls.Add(lblLogo);

// 2. OBSZAR PULPITU
Panel pnlPulpit = new Panel { Size = new Size(1150, 700), Location = new Point(0, 40), BackColor = Color.Transparent };
f.Controls.Add(pnlPulpit);

// 3. MENU KONTEKSTOWE PULPITU (PRAWY PRZYCISK)
ContextMenuStrip menuPulpitu = new ContextMenuStrip();
menuPulpitu.Items.Add("Nowa aplikacja .cs", null, (s, e) => {
    string n = "apka_" + (positions.Count + 1) + ".cs";
    positions[n] = new Point(150, 150);
    iconPaths[n] = "default";
    
    // Zapis pozycji
    List<string> ln = new List<string>();
    foreach (var pos in positions) ln.Add(pos.Key + " " + pos.Value.X + " " + pos.Value.Y + " " + iconPaths[pos.Key]);
    File.WriteAllLines(cfgFile, ln.ToArray());
    
    Application.Restart(); // Odświeżenie środowiska
});
pnlPulpit.ContextMenuStrip = menuPulpitu;

// 4. MENU KONTEKSTOWE IKONY (PRAWY PRZYCISK)
ContextMenuStrip menuIkony = new ContextMenuStrip();
ToolStripMenuItem itemRun = new ToolStripMenuItem("▶ Uruchom aplikację");
ToolStripMenuItem itemProp = new ToolStripMenuItem("🛠️ Właściwości i Ikona");
ToolStripMenuItem itemDel = new ToolStripMenuItem("❌ Usuń z pulpitu");
menuIkony.Items.Add(itemRun);
menuIkony.Items.Add(itemProp);
menuIkony.Items.Add(itemDel);

// Generowanie obiektów na pulpicie
foreach (var element in positions)
{
    string nazwaApki = element.Key;
    Point pozycja = element.Value;

    Panel pi = new Panel { Size = new Size(120, 110), Location = startMousePos, BackColor = Color.FromArgb(25, 30, 25), BorderStyle = BorderStyle.FixedSingle };
    pi.Location = pozycja;

    PictureBox pc = new PictureBox { Size = new Size(48, 48), Location = new Point(36, 15), BackColor = Color.FromArgb(40, 60, 40), SizeMode = PictureBoxSizeMode.StretchImage };
    if (iconPaths.ContainsKey(nazwaApki) && File.Exists(iconPaths[nazwaApki]))
    {
        try { pc.Image = Image.FromFile(iconPaths[nazwaApki]); } catch { }
    }

    Label lb = new Label { Text = nazwaApki, ForeColor = Color.LightGreen, Font = new Font("Consolas", 8, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter, Location = new Point(5, 70), Size = new Size(110, 35) };

    pi.Controls.Add(pc);
    pi.Controls.Add(lb);
    pnlPulpit.Controls.Add(pi);

    // Dynamiczne przypisanie zdarzeń menu kontekstowego do aktywnej ikony
    pc.ContextMenuStrip = menuIkony;
    lb.ContextMenuStrip = menuIkony;
    pi.ContextMenuStrip = menuIkony;

    pc.MouseEnter += (s, e) => selectedIcon = nazwaApki;
    lb.MouseEnter += (s, e) => selectedIcon = nazwaApki;
    pi.MouseEnter += (s, e) => selectedIcon = nazwaApki;

    // PRZENOSZENIE MYSZKĄ (DRAG AND DROP)
    pc.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; startMousePos = e.Location; selectedIcon = nazwaApki; } };
    pc.MouseMove += (s, e) => {
        if (isDragging && selectedIcon == nazwaApki) {
            pi.Left += e.X - startMousePos.X;
            pi.Top += e.Y - startMousePos.Y;
        }
    };
    pc.MouseUp += (s, e) => {
        if (isDragging) {
            isDragging = false;
            positions[nazwaApki] = pi.Location;
            
            List<string> ln = new List<string>();
            foreach (var pos in positions) ln.Add(pos.Key + " " + pos.Value.X + " " + pos.Value.Y + " " + iconPaths[pos.Key]);
            File.WriteAllLines(cfgFile, ln.ToArray());
        }
    };

    // Obsługa podwójnego kliknięcia (Prawdziwe otwieranie apek)
    Action launch = () => {
        Form appForm = new Form { Text = "HobbyOS Engine Runtime: " + nazwaApki, Size = new Size(500, 300), StartPosition = FormStartPosition.CenterScreen, BackColor = Color.FromArgb(20, 20, 25) };
        Label inf = new Label { Text = "Pomyślnie skompilowano i wywołano kod: " + nazwaApki, ForeColor = Color.White, Location = new Point(30, 50), Size = new Size(400, 30) };
        appForm.Controls.Add(inf);
        appForm.ShowDialog();
    };

    pc.DoubleClick += (s, e) => launch();
    lb.DoubleClick += (s, e) => launch();
}

// LOGIKA OBSŁUGI MENU KONTEKSTOWEGO IKON
itemRun.Click += (s, e) => {
    if (!string.IsNullOrEmpty(selectedIcon)) {
        Form appForm = new Form { Text = "HobbyOS Engine Runtime: " + selectedIcon, Size = new Size(500, 300), StartPosition = FormStartPosition.CenterScreen, BackColor = Color.FromArgb(20, 20, 25) };
        Label inf = new Label { Text = "Wywołano akcję RUN dla: " + selectedIcon, ForeColor = Color.White, Location = new Point(30, 50), Size = new Size(400, 30) };
        appForm.Controls.Add(inf);
        appForm.ShowDialog();
    }
};

itemProp.Click += (s, e) => {
    if (!string.IsNullOrEmpty(selectedIcon)) {
        using (OpenFileDialog ofd = new OpenFileDialog()) {
            ofd.Filter = "Obrazy (*.png;*.jpg)|*.png;*.jpg";
            if (ofd.ShowDialog() == DialogResult.OK) {
                iconPaths[selectedIcon] = ofd.FileName;
                List<string> ln = new List<string>();
                foreach (var pos in positions) ln.Add(pos.Key + " " + pos.Value.X + " " + pos.Value.Y + " " + iconPaths[pos.Key]);
                File.WriteAllLines(cfgFile, ln.ToArray());
                Application.Restart();
            }
        }
    }
};

itemDel.Click += (s, e) => {
    if (!string.IsNullOrEmpty(selectedIcon)) {
        positions.Remove(selectedIcon);
        iconPaths.Remove(selectedIcon);
        List<string> ln = new List<string>();
        foreach (var pos in positions) ln.Add(pos.Key + " " + pos.Value.X + " " + pos.Value.Y + " " + iconPaths[pos.Key]);
        File.WriteAllLines(cfgFile, ln.ToArray());
        Application.Restart();
    }
};

f.ShowDialog();
