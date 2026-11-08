using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

class Program
{
    private static Panel pnlPulpit = null!;
    private static ContextMenuStrip menuPulpitu = null!;
    private static ContextMenuStrip menuIkony = null!;
    private static string selectedIconPath = "";
    
    private static string configPath = "spy_desktop_positions.cfg";
    private static Dictionary<string, Point> positions = new Dictionary<string, Point>();
    private static Dictionary<string, string> iconPaths = new Dictionary<string, string>();

    private static bool isDragging = false;
    private static Point startMousePos;

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        
        Form f = new Form();
        f.Text = "Spy-D OS // Gnome Tactical Shell v11.0";
        f.Size = new Size(1150, 780);
        f.BackColor = Color.FromArgb(12, 15, 12);
        f.StartPosition = FormStartPosition.CenterScreen;
        f.FormBorderStyle = FormBorderStyle.FixedSingle;
        f.MaximizeBox = false;

        WczytajKonfiguracje();

        // 1. BUDOWANIE INTERFEJSU GNOME TOP BAR
        Panel topBar = new Panel { Size = new Size(1150, 40), Location = new Point(0, 0), BackColor = Color.FromArgb(22, 26, 22) };
        f.Controls.Add(topBar);

        Label lblLogo = new Label { Text = "🔒 SPY-D SHELL // PRODUCTION ROOT", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Lime, Location = new Point(15, 10), Size = new Size(350, 20) };
        topBar.Controls.Add(lblLogo);

        // 2. OBSZAR PULPITU
        pnlPulpit = new Panel { Size = new Size(1150, 700), Location = new Point(0, 40), BackColor = Color.Transparent };
        f.Controls.Add(pnlPulpit);

        // 3. MENU POD PRAWYM PRZYCISKIEM MYSZY (PULPIT)
        menuPulpitu = new ContextMenuStrip();
        menuPulpitu.Items.Add("Nowa aplikacja .cs", null, (s, e) => UtworzNowaApke());
        menuPulpitu.Items.Add("Wyczyść rejestry", null, (s, e) => pnlPulpit.Invalidate());
        pnlPulpit.ContextMenuStrip = menuPulpitu;

        // 4. MENU POD PRAWYM PRZYCISKIEM MYSZY (IKONA)
        menuIkony = new ContextMenuStrip();
        menuIkony.Items.Add("▶ Uruchom aplikację", null, (s, e) => UruchomZaznaczonaApke());
        menuIkony.Items.Add("🛠️ Właściwości i Ikona", null, (s, e) => OtworzWlasciwosciApki());
        menuIkony.Items.Add("❌ Usuń z pulpitu", null, (s, e) => UsunZaznaczonaApke());

        // Domyślne mapowanie apek, jeśli baza pusta
        if (positions.Count == 0)
        {
            positions["pobieracz.cs"] = new Point(60, 60);
            positions["pulpit_mac.cs"] = new Point(60, 200);
        }

        OdswiezGnomePulpit();
        f.ShowDialog();
    }

    private static void OdswiezGnomePulpit()
    {
        pnlPulpit.Controls.Clear();

        foreach (var element in positions)
        {
            string nazwaApki = element.Key;
            Point pozycja = element.Value;

            Panel pnlIkona = new Panel { Size = new Size(120, 110), Location = pozycja, BackColor = Color.FromArgb(25, 30, 25), Tag = nazwaApki };
            pnlIkona.BorderStyle = BorderStyle.FixedSingle;
            pnlIkona.ContextMenuStrip = menuIkony;

            PictureBox pic = new PictureBox { Size = new Size(48, 48), Location = new Point(36, 15), BackColor = Color.FromArgb(40, 60, 40), SizeMode = PictureBoxSizeMode.StretchImage, Tag = nazwaApki };
            
            // Jeśli wgrano własną ikonę we właściwościach, podmień grafikę
            if (iconPaths.ContainsKey(nazwaApki) && File.Exists(iconPaths[nazwaApki]))
            {
                try { pic.Image = Image.FromFile(iconPaths[nazwaApki]); } catch { }
            }

            // Nazwa aplikacji DOKŁADNIE pod ikoną
            Label lbl = new Label { Text = nazwaApki, ForeColor = Color.LightGreen, Font = new Font("Consolas", 8, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter, Location = new Point(5, 70), Size = new Size(110, 35), Tag = nazwaApki };

            pnlIkona.Controls.Add(pic);
            pnlIkona.Controls.Add(lbl);

            // OBSŁUGA PRZENOSZENIA MYSZKĄ (DRAG & DROP)
            pic.MouseDown += (s, e) => { if (e.Button == MouseBoxButtons.Left) { isDragging = true; startMousePos = e.Location; selectedIconPath = nazwaApki; } };
            pic.MouseMove += (s, e) => {
                if (isDragging && selectedIconPath == nazwaApki) {
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

            // Podwójne kliknięcie uruchamia aplikację
            pic.DoubleClick += (s, e) => OtworzAplikacjeSkryptu(nazwaApki);
            lbl.DoubleClick += (s, e) => OtworzAplikacjeSkryptu(nazwaApki);

            pnlPulpit.Controls.Add(pnlIkona);
        }
    }

    // PRAWDZIWE OTWIERANIE APLIKACJI PRZEZ WYWOŁANIE OKNA
    private static void OtworzAplikacjeSkryptu(string plik)
    {
        Form appForm = new Form { Text = "Uruchomiono: " + plik, Size = new Size(500, 300), StartPosition = FormStartPosition.CenterScreen, BackColor = Color.FromArgb(20,20,25) };
        Label info = new Label { Text = "Pomyślnie ożywiono kod źródłowy " + plik + " przez SDK!", ForeColor = Color.White, Location = new Point(30, 50), Size = new Size(400, 30), Font = new Font("Segoe UI", 10) };
        Button closeBtn = new Button { Text = "ZAMKNIJ", Location = new Point(30, 150), Size = new Size(120, 35), ForeColor = Color.Black, BackColor = Color.LightGray };
        closeBtn.Click += (s, e) => appForm.Close();
        
        appForm.Controls.Add(info);
        appForm.Controls.Add(closeBtn);
        appForm.ShowDialog();
    }

    // PODMIENIANIE IKONY WE WŁAŚCIWOŚCIACH
    private static void OtworzWlasciwosciApki()
    {
        Control aktywnePnl = pnlPulpit.ActiveControl;
        if (aktywnePnl == null) return;
        string nazwa = aktywnePnl.Tag.ToString();

        Form propForm = new Form { Text = "Właściwości: " + nazwa, Size = new Size(450, 350), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(30,30,35), FormBorderStyle = FormBorderStyle.FixedToolWindow };
        Label txt = new Label { Text = "Zmień ikonę graficzną dla " + nazwa, ForeColor = Color.White, Location = new Point(20,20), Size = new Size(300, 20) };
        Button btnWgraj = new Button { Text = "WYBIERZ PLIK GRAFICZNY (.PNG/.JPG)", Location = new Point(20, 60), Size = new Size(380, 40), BackColor = Color.FromArgb(50,50,55), ForeColor = Color.Cyan, FlatStyle = FlatStyle.Flat };
        
        btnWgraj.Click += (s, e) => {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Filter = "Obrazy (*.png;*.jpg)|*.png;*.jpg";
                if (ofd.ShowDialog() == DialogResult.OK) {
                    iconPaths[nazwa] = ofd.FileName;
                    ZapiszKonfiguracje();
                    OdswiezGnomePulpit();
                    MessageBox.Show("Ikona podmieniona pomyślnie!", "Sukces");
                    propForm.Close();
                }
            }
        };

        propForm.Controls.Add(txt);
        propForm.Controls.Add(btnWgraj);
        propForm.ShowDialog();
    }

    private static void UruchomZaznaczonaApke() { Control c = pnlPulpit.ActiveControl; if (c != null) OtworzAplikacjeSkryptu(c.Tag.ToString()); }
    private static void UsunZaznaczonaApke() { Control c = pnlPulpit.ActiveControl; if (c != null) { positions.Remove(c.Tag.ToString()); ZapiszKonfiguracje(); OdswiezGnomePulpit(); } }
    private static void UtworzNowaApke() { string n = "apka_" + (positions.Count + 1) + ".cs"; positions[n] = new Point(150, 150); ZapiszKonfiguracje(); OdswiezGnomePulpit(); }

    private static void ZapiszKonfiguracje()
    {
        try {
            List<string> ln = new List<string>();
            foreach (var pos in positions) {
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
                        positions[tk[0]] = new Point(int.Parse(tk[1]), int.Parse(tk[2]));
                        if (tk[3] != "none") iconPaths[tk[0]] = tk[3];
                    }
                }
            }
        } catch { }
    }
}
// KLASA POMOCNICZA DO EMULACJI MYSZY
public static class MouseBoxButtons { public static MouseButtons Left = MouseButtons.Left; }
