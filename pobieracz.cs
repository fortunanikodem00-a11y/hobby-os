using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

Application.EnableVisualStyles();

// UTWORZENIE OKNA FILTRU SIECIOWEGO
Form f = new Form();
f.Text = "🛰️ NET-FETCH // HOBBYOS GLOBAL DOWNLOAD MANAGER [v3.0]";
f.Size = new Size(700, 320);
f.BackColor = Color.FromArgb(16, 16, 22);
f.StartPosition = FormStartPosition.CenterScreen;
f.FormBorderStyle = FormBorderStyle.FixedSingle;
f.MaximizeBox = false;

// ETYKIETA ADRESU URL
Label lblUrl = new Label { Text = "WPROWADŹ LINK RAW GITHUBA LUB URL PLIKU .CS:", Location = new Point(25, 20), Size = new Size(630, 20), ForeColor = Color.Cyan, Font = new Font("Consolas", 9, FontStyle.Bold) };
f.Controls.Add(lblUrl);

// POLE TEKSTOWE NA LINK
TextBox txtUrl = new TextBox { Location = new Point(25, 45), Size = new Size(635, 25), BackColor = Color.FromArgb(28, 28, 36), ForeColor = Color.Lime, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.FixedSingle };
txtUrl.Text = "https://githubusercontent.com"; // Domyślny ułatwiający prefiks
f.Controls.Add(txtUrl);

// ETYKIETA STATUSU OPERACJI
Label lblStatus = new Label { Text = "STATUS POWŁOKI: OCZEKIWANIE NA LINK SIECIOWY...", Location = new Point(25, 90), Size = new Size(630, 20), ForeColor = Color.DarkGray, Font = new Font("Consolas", 9) };
f.Controls.Add(lblStatus);

// PASEK POSTĘPU POBIERANIA
ProgressBar pasekPostepu = new ProgressBar { Location = new Point(25, 120), Size = new Size(635, 25), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0, Visible = false };
f.Controls.Add(pasekPostepu);

// PRZYCISK INICJACJI TRANSFERU
Button btnPobierz = new Button { Text = "ROZPOCZNIJ PRZEKIEROWANIE I POBIERANIE pliku", Location = new Point(25, 170), Size = new Size(635, 45), BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
btnPobierz.FlatAppearance.BorderColor = Color.Lime;
f.Controls.Add(btnPobierz);

// REJESTRACJA AKCJI KLIKNIĘCIA — ASYNCHRONICZNY TRANSFER BEZ ZAMRAŻANIA OKNA
btnPobierz.Click += async (s, e) => {
    string url = txtUrl.Text.Trim();
    
    if (string.IsNullOrEmpty(url) || url.StartsWith("https://githubusercontent.com"))
    {
        lblStatus.Text = " [BŁĄD]: Wklej prawidłowy, pełny link sieciowy pliku!";
        lblStatus.ForeColor = Color.Red;
        return;
    }

    btnPobierz.Enabled = false;
    txtUrl.ReadOnly = true;
    pasekPostepu.Visible = true;
    pasekPostepu.MarqueeAnimationSpeed = 30; // Włączenie animacji paska
    lblStatus.Text = " [TRANSFER]: Nawiązywanie bezpiecznego gniazda SSL...";
    lblStatus.ForeColor = Color.Yellow;

    try
    {
        using (HttpClient klient = new HttpClient())
        {
            klient.DefaultRequestHeaders.Add("User-Agent", "HobbyOS-FetchEngine");
            
            lblStatus.Text = " [TRANSFER]: Pobieranie surowych bajtów kodu z serwera...";
            string czystyKodCs = await klient.GetStringAsync(url);

            // Automatyczne wyciąganie nazwy pliku z adresu URL
            string nazwaPliku = url.Substring(url.LastIndexOf('/') + 1);
            if (string.IsNullOrEmpty(nazwaPliku) || !nazwaPliku.EndsWith(".cs"))
            {
                nazwaPliku = "pobrana_aplikacja_" + DateTime.Now.Ticks + ".cs";
            }

            // Ustalenie ścieżki zapisu wewnątrz folderu /src_repo jądra
            string folderRepo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src_repo");
            if (!Directory.Exists(folderRepo))
            {
                Directory.CreateDirectory(folderRepo);
            }

            string ostatecznaSciezka = Path.Combine(folderRepo, nazwaPliku);
            
            // Zapis surowego skryptu C# na dysk twardy
            File.WriteAllText(ostatecznaSciezka, czystyKodCs);

            pasekPostepu.MarqueeAnimationSpeed = 0;
            pasekPostepu.Style = ProgressBarStyle.Blocks;
            pasekPostepu.Value = 100;
            
            lblStatus.Text = " [SUKCES]: Zapisano jako '" + nazwaPliku + "' w /src_repo! Odśwież pulpit.";
            lblStatus.ForeColor = Color.Lime;
            MessageBox.Show("Pobieranie zakończone sukcesem!\nSkrypt '" + nazwaPliku + "' jest gotowy do rozruchu.", "HobbyOS Security", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    catch (Exception ex)
    {
        pasekPostepu.Visible = false;
        lblStatus.Text = " [BŁĄD SIECI]: " + ex.Message;
        lblStatus.ForeColor = Color.Red;
    }
    finally
    {
        btnPobierz.Enabled = true;
        txtUrl.ReadOnly = false;
    }
};

f.ShowDialog();
