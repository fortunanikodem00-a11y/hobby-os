using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

class Program
{
    private static Panel pnlPulpit = null!;
    private static Panel pnlMagazyn = null!;
    private static string plikKonfig = "spy_desktop_config.txt";
    private static string folderSrc = "src_repo";
    
    // Slownik przechowujacy pozycje ikon na pulpicie
    private static Dictionary<string, Point> pozycjeIkon = new Dictionary<string, Point>();
    
    // Dane do przenoszenia myszka (Drag & Drop)
    private static bool czyPrzeciagasz = false;
    private static Point punktStartowyMyszki;

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        
        Form f = new Form();
        f.Text = "Spy-D OS // Gnome Advanced Tactical Suite";
        f.Size = new Size(1150, 780);
        f.BackColor = Color.FromArgb(10, 12, 14); // Mroczny motyw szpiegowski
        f.StartPosition = FormStartPosition.CenterScreen;
        f.FormBorderStyle = FormBorderStyle.FixedSingle;
        f.MaximizeBox = false;

        // Wczytanie zapisanych pozycji z dysku przed budowaniem
        WczytajPozycjeZDysku();

        // 1. TOP BAR (Pasek Gnome)
        Panel topBar = new Panel { Size = new Size(1150, 40), Location = new Point(0, 0), BackColor = Color.FromArgb(20, 22, 26) };
        f.Controls.Add(topBar);

        Label lblLogo = new Label { Text = "🔒 SPY-D CORE v10.5 // GNOME RUNTIME", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.LightGreen, Location = new Point(15, 12), Size = new Size(300, 20) };
        topBar.Controls.Add(lblLogo);

        // Przycisk otwierania Magazynu Aplikacji
        Button btnSklep = new Button { Text = "🏪 MAGAZYN APLIKACJI (.CS)", Location = new Point(850, 6), Size = new Size(250, 28), BackColor = Color.FromArgb(40, 45, 55), ForeColor = Color.Cyan, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        btnSklep.FlatAppearance.BorderColor = Color.Cyan;
        btnSklep.Click += (s, e) => pnlMagazyn.Visible = !pnlMagazyn.Visible;
        topBar.Controls.Add(btnSklep);

        // 2. GŁÓWNY OBSZAR PULPITU
        pnlPulpit = new Panel { Size = new Size(1150, 700), Location = new Point(0, 40), BackColor = Color.Transparent };
        f.Controls.Add(pnlPulpit);

        // 3. BOCZNY PANEL: MAGAZYN APLIKACJI (Domyślnie ukryty)
        pnlMagazyn = new Panel { Size = new Size(320, 600), Location = new Point(800, 50), BackColor = Color.FromArgb(25, 28, 35), Visible = false, BorderStyle = BorderStyle.FixedSingle };
        f.Controls.Add(pnlMagazyn);
        pnlMagazyn.BringToFront();

        Label lblMagazynTytul = new Label { Text = "DOSTĘPNE REPOZYTORIA KODU:", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White, Location = new Point(15, 15), Size = new Size(280, 20) };
        pnlMagazyn.Controls.Add(lblMagazynTytul);

        // Dodawanie apek z Magazynu na Pulpit
        StworzElementMagazynu("pobieracz.cs", 50, Color.LightBlue);
        StworzElementMagazynu("notatnik.cs", 130, Color.LightGreen);
        StworzElementMagazynu("skaner_ip.cs", 210, Color.Orange);

        // Przycisk instalacji/wgrywania wlasnej ikony z dysku (.png/.jpg)
        Button btnWgrajIkone = new Button { Text = "📥 WGRAJ WŁASNĄ IKONĘ OBIEKTU", Location = new Point(15, 530), Size = new Size(280, 40), BackColor = Color.FromArgb(40, 50, 45), ForeColor = Color.Lime, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnWgrajIkone.FlatAppearance.BorderColor = Color.Lime;
        btnWgrajIkone.Click += (s, e) => {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Filter = "Obrazy (*.png;*.jpg)|*.png;*.jpg";
                if (ofd.ShowDialog() == DialogResult.OK) {
                    MessageBox.Show("Ikona została pomyślnie wgrana do jądra Spy-D OS i zabezpieczona!", "Magazyn");
                }
            }
        };
        pnlMagazyn.Controls.Add(btnWgrajIkone);

        // Renderowanie stałych ikon z bazy zapisu na pulpicie
        OdswiezPulpit();

        f.ShowDialog();
    }

    // TWORZENIE PRZYCISKU INSTALACYJNEGO W MAGAZYNIE
    private static void StworzElementMagazynu(string nazwaApki, int y, Color kolor)
    {
        Button btnApka = new Button { Text = $"Zainstaluj: {nazwaApki}", Location = new Point(15, y), Size = new Size(280, 45), BackColor = Color.FromArgb(45, 48, 55), ForeColor = kolor, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnApka.FlatAppearance.BorderColor = kolor;
        
        btnApka.Click += (s, e) => {
            if (!pozycjeIkon.ContainsKey(nazwaApki)) {
                // Przypisanie domyślnej pozycji na środku pulpitu
                pozycjeIkon[nazwaApki] = new Point(100 + (pozycjeIkon.Count * 50), 150);
                ZapiszPozycjeNaDysk();
                OdswiezPulpit();
                MessageBox.Show($"Aplikacja {nazwaApki} została umieszczona na pulpicie. Możesz ją teraz swobodnie przesuwać myszką!", "HobbyOS Magazyn");
            } else {
                MessageBox.Show("Ta aplikacja jest już zainstalowana na Twoim pulpicie.", "Info");
            }
        };
        pnlMagazyn.Controls.Add(btnApka);
    }

    // ODŚWIEŻANIE I RENDEROWANIE IKON NA PULPICIE MYSZKĄ
    private static void OdswiezPulpit()
    {
        // Czyszczenie starych kontrolek (oprócz panelu magazynu)
        List<Control> doUsuniecia = new List<Control>();
        foreach (Control c in pnlPulpit.Controls) doUsuniecia.Add(c);
        foreach (Control c in doUsuniecia) pnlPulpit.Controls.Remove(c);

        foreach (var ikona in pozycjeIkon)
        {
            string nazwaPlikuCs = ikona.Key;
            Point pozycja = ikona.Value;

            // Kontener całej ikony aplikacji
            Panel pnlIkona = new Panel { Size = new Size(110, 100), Location = pozycja, BackColor = Color.FromArgb(30, 35, 45), Cursor = Cursors.Hand };
            pnlIkona.BorderStyle = BorderStyle.FixedSingle;

            // Graficzny element kwadratu (stała ikona)
            PictureBox picGrafika = new PictureBox { Size = new Size(40, 40), Location = new Point(35, 15), BackColor = Color.FromArgb(100, 200, 255), SizeMode = PictureBoxSizeMode.StretchImage };
            
            // Napis NAZWY APLIKACJI DOKŁADNIE POD IKONĄ (Wymóg!)
            Label lblNazwa = new Label { Text = nazwaPlikuCs, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter, Location = new Point(5, 65), Size = new Size(100, 30) };

            pnlIkona.Controls.Add(picGrafika);
            pnlIkona.Controls.Add(lblNazwa);

            // PRZENOSZENIE IKON MYSZKĄ (DRAG & DROP MECHANIZM)
            picGrafika.MouseDown += (s, e) => { czyPrzeciagasz = true; punktStartowyMyszki = e.Location; };
            picGrafika.MouseMove += (s, e) => {
                if (czyPrzeciagasz) {
                    pnlIkona.Left += e.X - punktStartowyMyszki.X;
                    pnlIkona.Top += e.Y - punktStartowyMyszki.Y;
                }
            };
            picGrafika.MouseUp += (s, e) => {
                if (czyPrzeciagasz) {
                    czyPrzeciagasz = false;
                    // Zapis nowej pozycji do słownika i na dysk twardy
                    pozycjeIkon[nazwaPlikuCs] = pnlIkona.Location;
                    ZapiszPozycjeNaDysk();
                }
            };

            // Kliknięcie w ikonę uruchamia kod (Symulacja akcji aplikacji)
            lblNazwa.Click += (s, e) => MessageBox.Show($"Uruchamianie kodu źródłowego aplikacji: {nazwaPlikuCs} przez jądro .NET Roslyn!", "Spy-D OS Kernel");
            picGrafika.Click += (s, e) => MessageBox.Show($"Uruchamianie kodu źródłowego aplikacji: {nazwaPlikuCs} przez jądro .NET Roslyn!", "Spy-D OS Kernel");

            pnlPulpit.Controls.Add(pnlIkona);
        }
    }

    // AUTENTYCZNY ZAPIS WSPÓŁRZĘDNYCH DO PLIKU TEKSTOWEGO
    private static void ZapiszPozycjeNaDysk()
    {
        try {
            List<string> linie = new List<string>();
            foreach (var ikona in pozycjeIkon) {
                linie.Add($"{ikona.Key};{ikona.Value.X};{ikona.Value.Y}");
            }
            File.WriteAllLines(plikKonfig, linie);
        } catch { /* Zabezpieczenie zapisu */ }
    }

    // AUTENTYCZNY ODCZYT WSPÓŁRZĘDNYCH Z DYSKU (PAMIĘĆ STAŁA)
    private static void WczytajPozycjeZDysku()
    {
        try {
            if (File.Exists(plikKonfig)) {
                string[] linie = File.ReadAllLines(plikKonfig);
                pozycjeIkon.Clear();
                foreach (string linia in linie) {
                    string[] tokeny = linia.Split(';');
                    if (tokeny.Length == 3) {
                        string nazwa = tokeny[0];
                        int x = int.Parse(tokeny[1]);
                        int y = int.Parse(tokeny[2]);
                        pozycjeIkon[nazwa] = new Point(x, y);
                    }
                }
            } else {
                // Domyślne ikony startowe systemu, jeśli plik konfiguracyjny jeszcze nie istnieje
                pozycjeIkon["spy-d.cs"] = new Point(50, 50);
                pozycjeIkon["pulpit_mac.cs"] = new Point(50, 180);
            }
        } catch { /* Zabezpieczenie odczytu bazy */ }
    }
}
