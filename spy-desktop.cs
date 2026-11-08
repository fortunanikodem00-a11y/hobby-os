using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class Program
{
    private static Panel pnlPulpit = null!;
    private static Panel pnlGnomeDock = null!;
    private static ContextMenuStrip menuIkony = null!;
    
    private static string configPath = "spy_desktop_positions.cfg";
    private static Dictionary<string, Point> positions = new Dictionary<string, Point>();
    private static Dictionary<string, string> iconPaths = new Dictionary<string, string>();

    private static bool isDragging = false;
    private static Point startMousePos;
    private static string selectedIcon = "";

    private static int obecnyScrollY = 0;
    private static int krokScrolla = 80;

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        
        Form f = new Form();
        f.Text = "Spy-D OS // Gnome Tactical Desktop Suite";
        f.Size = new Size(1150, 780);
        f.BackColor = Color.FromArgb(11, 12, 15);
        f.StartPosition = FormStartPosition.CenterScreen;
        f.FormBorderStyle = FormBorderStyle.FixedSingle;
        f.MaximizeBox = false;
        f.KeyPreview = true;

        WczytajKonfiguracje();

        Panel topBar = new Panel { Size = new Size(1150, 35), Location = new Point(0, 0), BackColor = Color.FromArgb(18, 18, 22) };
        f.Controls.Add(topBar);

        Label lblLogo = new Label { Text = "🔒 GNOME SHELL CORE // LIVE FILE-SYSTEM ACTIVE (Klawisze GÓRA/DÓŁ przewijają pulpit pionowo)", Font = new Font("Ubuntu", 9, FontStyle.Bold), ForeColor = Color.FromArgb(240, 240, 240), Location = new Point(15, 10), Size = new Size(800, 20) };
        topBar.Controls.Add(lblLogo);

        pnlGnomeDock = new Panel { Size = new Size(70, 745), Location = new Point(0, 35), BackColor = Color.FromArgb(16, 17, 20) };
        f.Controls.Add(pnlGnomeDock);

        Button btnApps = new Button { Size = new Size(50, 50), Location = new Point(10, 20), BackColor = Color.FromArgb(35, 38, 45), FlatStyle = FlatStyle.Flat, Text = "⣿", Font = new Font("Segoe UI", 16), ForeColor = Color.Orange };
        btnApps.FlatAppearance.BorderSize = 0;
        pnlGnomeDock.Controls.Add(btnApps);

        pnlPulpit = new Panel { Size = new Size(1080, 710), Location = new Point(70, 35), BackColor = Color.Transparent };
        f.Controls.Add(pnlPulpit);

        menuIkony = new ContextMenuStrip();
        menuIkony.Items.Add("Uruchom podprogram", null, (s, e) => UruchomZaznaczonaApke());

        f.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Down)
            {
                obecnyScrollY -= krokScrolla;
                PrzesunZawartoscPulpitu();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (obecnyScrollY < 0)
                {
                    obecnyScrollY += krokScrolla;
                    PrzesunZawartoscPulpitu();
                }
                e.Handled = true;
            }
        };

        AnalizujDyskIBudujIkony();

        Application.Run(f);
    }

    private static void AnalizujDyskIBudujIkony()
    {
        pnlPulpit.Controls.Clear();

        string folderRepo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src_repo");
        if (!Directory.Exists(folderRepo))
        {
            Directory.CreateDirectory(folderRepo);
        }

        string[] znalezionePliki = Directory.GetFiles(folderRepo, "*.cs");

        int startX = 40;
        int startY = 40;
        int odstepX = 145;
        int odstepY = 135;
        int kolumnyNaEkran = 6;

        if (znalezionePliki.Length == 0)
        {
            Label lblPuste = new Label { Text = "📂 [ Repozytorium src_repo puste ]\nWgraj skrypty .cs, aby automatycznie wyrenderować ikony.", ForeColor = Color.FromArgb(100, 110, 120), Font = new Font("Consolas", 10, FontStyle.Italic), Location = new Point(50, 50), Size = new Size(600, 50) };
            pnlPulpit.Controls.Add(lblPuste);
        }

        for (int i = 0; i < znalezionePliki.Length; i++)
        {
            string pelnaSciezka = znalezionePliki[i];
            string nazwaApki = Path.GetFileName(pelnaSciezka);

            if (!positions.ContainsKey(nazwaApki))
            {
                int kolumna = i % kolumnyNaEkran;
                int wiersz = i / kolumnyNaEkran;
                positions[nazwaApki] = new Point(startX + (kolumna * odstepX), startY + (wiersz * odstepY));
            }

            Point pozycja = positions[nazwaApki];

            Panel pnlIkona = new Panel { Size = new Size(115, 105), Location = pozycja, BackColor = Color.FromArgb(24, 25, 30), BorderStyle = BorderStyle.None, Tag = nazwaApki };
            pnlIkona.ContextMenuStrip = menuIkony;

            Panel pnlGrafika = new Panel { Size = new Size(46, 46), Location = new Point(34, 15), BackColor = Color.FromArgb(40, 42, 50) };
            pnlIkona.Controls.Add(pnlGrafika);

            Label lbl = new Label { Text = nazwaApki, ForeColor = Color.FromArgb(220, 220, 220), Location = new Point(2, 68), Size = new Size(111, 32), Tag = nazwaApki, TextAlign = ContentAlignment.TopCenter, Font = new Font("Segoe UI", 8, FontStyle.Regular) };
            pnlIkona.Controls.Add(lbl);

            pnlGrafika.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isDragging = true; startMousePos = e.Location; selectedIcon = nazwaApki; } };
            pnlGrafika.MouseMove += (s, e) => {
                if (isDragging && selectedIcon == nazwaApki) {
                    pnlIkona.Left += e.X - startMousePos.X;
                    pnlIkona.Top += e.Y - startMousePos.Y;
                }
            };
            pnlGrafika.MouseUp += (s, e) => {
                if (isDragging) {
                    isDragging = false;
                    positions[nazwaApki] = new Point(pnlIkona.Left, pnlIkona.Top - obecnyScrollY);
                    ZapiszKonfiguracje();
                }
            };

            pnlGrafika.MouseEnter += (s, e) => { selectedIcon = nazwaApki; pnlIkona.BackColor = Color.FromArgb(34, 36, 44); };
            pnlGrafika.MouseLeave += (s, e) => pnlIkona.BackColor = Color.FromArgb(24, 25, 30);
            lbl.MouseEnter += (s, e) => { selectedIcon = nazwaApki; pnlIkona.BackColor = Color.FromArgb(34, 36, 44); };

            pnlGrafika.DoubleClick += (s, e) => EmulujKomendeWTerminalu(nazwaApki);
            lbl.DoubleClick += (s, e) => EmulujKomendeWTerminalu(nazwaApki);

            pnlPulpit.Controls.Add(pnlIkona);
        }

        PrzesunZawartoscPulpitu();
    }

    private static void PrzesunZawartoscPulpitu()
    {
        foreach (Control c in pnlPulpit.Controls)
        {
            if (c is Panel && c.Tag != null)
            {
                string nazwa = c.Tag.ToString()!;
                if (positions.ContainsKey(nazwa))
                {
                    c.Top = positions[nazwa].Y + obecnyScrollY;
                }
            }
        }
    }

    private static void EmulujKomendeWTerminalu(string nazwaSkryptu)
    {
        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType("HobbyOSFullBootKernel");
                if (t != null)
                {
                    var fields = t.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    foreach (var field in fields)
                    {
                        if (field.FieldType == typeof(TextBox))
                        {
                            foreach (Form openForm in Application.OpenForms)
                            {
                                if (openForm.GetType() == t)
                                {
                                    TextBox txtInput = (TextBox)field.GetValue(openForm)!;
                                    txtInput.Invoke(new Action(() => {
                                        txtInput.Focus();
                                        txtInput.Text = "pulpit-start " + nazwaSkryptu; 
                                        SendKeys.Send("{ENTER}");
                                    }));
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }

    private static void UruchomZaznaczonaApke() { if (!string.IsNullOrEmpty(selectedIcon)) EmulujKomendeWTerminalu(selectedIcon); }

    private static void ZapiszKonfiguracje()
    {
        try {
            List<string> ln = new List<string>();
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
                    var elementy = new List<string>(l.Split(','));
                    if (elementy.Count == 4) {
                        string key = elementy[0];
                        int x = int.Parse(elementy[1]);
                        int y = int.Parse(elementy[2]);
                        string iPath = elementy[3];
                        positions[key] = new Point(x, y);
                        if (iPath != "none") iconPaths[key] = iPath;
                    }
                }
            }
        } catch { }
    }
}
