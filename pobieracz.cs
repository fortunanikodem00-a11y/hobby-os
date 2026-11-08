using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class Program
{
    private static Panel pnlPulpit = null!;
    private static ContextMenuStrip menuIkony = null!;
    
    private static string configPath = "spy_desktop_positions.cfg";
    private static Dictionary<string, Point> positions = new Dictionary<string, Point>();
    private static Dictionary<string, string> iconPaths = new Dictionary<string, string>();

    private static bool isDragging = false;
    private static Point startMousePos;
    private static string selectedIcon = "";

    // PARAMETRY PRZEWIJANIA PULPITU (SCROLL)
    private static int obecnyScrollY = 0;
    private static int krokScrolla = 80;

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        
        Form f = new Form();
        f.Text = "Spy-D OS // Gnome Tactical Desktop Suite";
        f.Size = new Size(1150, 780);
        f.BackColor = Color.FromArgb(14, 16, 20);
        f.StartPosition = FormStartPosition.CenterScreen;
        f.FormBorderStyle = FormBorderStyle.FixedSingle;
        f.MaximizeBox = false;
        f.KeyPreview = true; // Wymagane do przechwytywania strzałek klawiatury

        WczytajKonfiguracje();

        // Górny panel informacyjny
        Panel topBar = new Panel { Size = new Size(1150, 40), Location = new Point(0, 0), BackColor = Color.FromArgb(24, 26, 32) };
        f.Controls.Add(topBar);

        Label lblLogo = new Label { Text = "🔒 SPY-D SHELL // LIVE FILE-SYSTEM ENGINE ACTIVE (Klawisze GÓRA/DÓŁ przewijają pulpit)", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.Lime, Location = new Point(15, 12), Size = new Size(700, 20) };
        topBar.Controls.Add(lblLogo);

        // Kontener główny pulpitu
        pnlPulpit = new Panel { Size = new Size(1150, 700), Location = new Point(0, 40), BackColor = Color.Transparent };
        f.Controls.Add(pnlPulpit);

        menuIkony = new ContextMenuStrip();
        menuIkony.Items.Add("Uruchom", null, (s, e) => UruchomZaznaczonaApke());
        menuIkony.Items.Add("Wlasciwosci", null, (s, e) => OtworzWlasciwosciApki());

        // OBSŁUGA PRZEWIJANIA PULPITU ZA POMOCĄ KLAWISZY GÓRA / DÓŁ
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

        // Analiza dysku i generowanie ikon
        AnalizujDyskIBudujIkony();

        Application.Run(f);
    }

    private static void AnalizujDyskIBudujIkony()
    {
        pnlPulpit.Controls.Clear();

        // Fizyczne skanowanie folderu src_repo w poszukiwaniu prawdziwych aplikacji
        string folderRepo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src_repo");
        if (!Directory.Exists(folderRepo))
        {
            Directory.CreateDirectory(folderRepo);
        }

        string[] znalezionePliki = Directory.GetFiles(folderRepo, "*.cs");

        int startX = 50;
        int startY = 40;
        int odstepX = 150;
        int odstepY = 140;
        int kolumnyNaEkran = 7;

        for (int i = 0; i < znalezionePliki.Length; i++)
        {
            string pelnaSciezka = znalezionePliki[i];
            string nazwaApki = Path.GetFileName(pelnaSciezka);

            // Wyliczanie automatycznej pozycji siatki, jeśli brak zapisanej w pliku cfg
            if (!positions.ContainsKey(nazwaApki))
            {
                int kolumna = i % kolumnyNaEkran;
                int wiersz = i / kolumnyNaEkran;
                positions[nazwaApki] = new Point(startX + (kolumna * odstepX), startY + (wiersz * odstepY));
            }

            Point pozycja = positions[nazwaApki];

            // Budowanie wizualnego kontenera ikony
            Panel pnlIkona = new Panel { Size = new Size(120, 110), Location = pozycja, BackColor = Color.FromArgb(30, 36, 45), BorderStyle = BorderStyle.FixedSingle, Tag = nazwaApki };
            pnlIkona.ContextMenuStrip = menuIkony;

            PictureBox pic = new PictureBox { Size = new Size(48, 48), Location = new Point(36, 15), BackColor = Color.FromArgb(45, 55, 70), SizeMode = PictureBoxSizeMode.StretchImage, Tag = nazwaApki };
            if (iconPaths.ContainsKey(nazwaApki) && File.Exists(iconPaths[nazwaApki]))
            {
                try { pic.Image = Image.FromFile(iconPaths[nazwaApki]); } catch { }
            }

            Label lbl = new Label { Text = nazwaApki, ForeColor = Color.LightGreen, Location = new Point(5, 70), Size = new Size(110, 35), Tag = nazwaApki, TextAlign = ContentAlignment.TopCenter, Font = new Font("Consolas", 8, FontStyle.Bold) };

            pnlIkona.Controls.Add(pic);
            pnlIkona.Controls.Add(lbl);

            // Obsługa przeciągania myszką
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
                    positions[nazwaApki] = new Point(pnlIkona.Left, pnlIkona.Top - obecnyScrollY);
                    ZapiszKonfiguracje();
                }
            };

            pic.MouseEnter += (s, e) => selectedIcon = nazwaApki;
            lbl.MouseEnter += (s, e) => selectedIcon = nazwaApki;

            // Bezpośredni rozkaz wykonania komendy bezpśrednio w Twoim jądre
            pic.DoubleClick += (s, e) => EmulujKomendeWTerminalu(nazwaApki);
            lbl.DoubleClick += (s, e) => EmulujKomendeWTerminalu(nazwaApki);

            pnlPulpit.Controls.Add(pnlIkona);
        }

        PrzesunZawartoscPulpitu();
    }

    private static void PrzesunZawartoscPulpitu()
    {
        // Przesuwa ikony w pionie w zależności od wykonanego scrolla klawiaturą
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
                                        // Wywołuje stworzoną przez Ciebie komendę bezpośredniego rozruchu z parametrem nazwy
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

    private static void OtworzWlasciwosciApki()
    {
        if (string.IsNullOrEmpty(selectedIcon)) return;
        Form propForm = new Form { Text = "Wlasciwosci: " + selectedIcon, Size = new Size(450, 300), StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(30,30,35) };
        Button btnWgraj = new Button { Text = "ZMIEŃ IKONĘ (PNG/JPG)", Location = new Point(20, 60), Size = new Size(380, 40), ForeColor = Color.Cyan, FlatStyle = FlatStyle.Flat };
        
        btnWgraj.Click += (s, e) => {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Filter = "Obrazy|*.png;*.jpg";
                if (ofd.ShowDialog() == DialogResult.OK) {
                    iconPaths[selectedIcon] = ofd.FileName;
                    ZapiszKonfiguracje();
                    AnalizujDyskIBudujIkony();
                    propForm.Close();
                }
            }
        };
        propForm.Controls.Add(btnWgraj);
        propForm.ShowDialog();
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
string[] tk = l.Split(',');
if (tk.Length == 4) {
string key = tk.GetValue(0).ToString();
int x = int.Parse(tk.GetValue(1).ToString());
int y = int.Parse(tk.GetValue(2).ToString());
string iPath = tk.GetValue(3).ToString();
positions[key] = new Point(x, y);
if (iPath != "none") iconPaths[key] = iPath;
}
}
}
} catch { }
}
}
