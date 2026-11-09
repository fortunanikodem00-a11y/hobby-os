using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;

public class PobieraczApp
{
    private static string folderRepo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        @"source\repos\Hobby OS\Hobby OS\bin\Debug\net10.0-windows\src_repo"
    );

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();

        Form f = new Form
        {
            Text = "🛰️ NET-FETCH // HOBBYOS DOWNLOAD MANAGER",
            Size = new Size(680, 290),
            BackColor = Color.FromArgb(16, 16, 22),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox = false
        };

        Label lblUrl = new Label
        {
            Text = "WKLEJ LINK RAW DO PLIKU .CS (NP. Z GITHUBA):",
            Location = new Point(25, 20),
            Size = new Size(610, 20),
            ForeColor = Color.Cyan,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        f.Controls.Add(lblUrl);

        TextBox txtUrl = new TextBox
        {
            Location = new Point(25, 45),
            Size = new Size(615, 25),
            BackColor = Color.FromArgb(28, 28, 36),
            ForeColor = Color.Lime,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle
        };
        f.Controls.Add(txtUrl);

        Label lblStatus = new Label
        {
            Text = "STATUS: GOTOWY DO POBIERANIA...",
            Location = new Point(25, 85),
            Size = new Size(615, 20),
            ForeColor = Color.DarkGray,
            Font = new Font("Consolas", 9)
        };
        f.Controls.Add(lblStatus);

        ProgressBar pasek = new ProgressBar
        {
            Location = new Point(25, 115),
            Size = new Size(615, 22),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 0,
            Visible = false
        };
        f.Controls.Add(pasek);

        Button btnPobierz = new Button
        {
            Text = "POBIERZ I ZAINSTALUJ W SRC_REPO",
            Location = new Point(25, 160),
            Size = new Size(615, 45),
            BackColor = Color.FromArgb(35, 38, 45),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        btnPobierz.FlatAppearance.BorderColor = Color.Lime;
        f.Controls.Add(btnPobierz);

        btnPobierz.Click += async (s, e) =>
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
            {
                lblStatus.Text = "[BŁĄD]: Wklej poprawny link zaczynający się od http/https!";
                lblStatus.ForeColor = Color.Red;
                return;
            }

            btnPobierz.Enabled = false;
            txtUrl.ReadOnly = true;
            pasek.Visible = true;
            pasek.MarqueeAnimationSpeed = 30;
            lblStatus.Text = "[TRANSFER]: Łączenie z serwerem...";
            lblStatus.ForeColor = Color.Yellow;

            try
            {
                using (HttpClient klient = new HttpClient())
                {
                    klient.DefaultRequestHeaders.Add("User-Agent", "HobbyOS-FetchEngine");
                    string kodCs = await klient.GetStringAsync(url);

                    string nazwaPliku = Path.GetFileName(new Uri(url).LocalPath);
                    if (string.IsNullOrEmpty(nazwaPliku) || !nazwaPliku.EndsWith(".cs"))
                    {
                        nazwaPliku = "app_" + DateTime.Now.Ticks + ".cs";
                    }

                    if (!Directory.Exists(folderRepo)) Directory.CreateDirectory(folderRepo);
                    string pelnaSciezka = Path.Combine(folderRepo, nazwaPliku);

                    File.WriteAllText(pelnaSciezka, kodCs);

                    pasek.MarqueeAnimationSpeed = 0;
                    pasek.Style = ProgressBarStyle.Blocks;
                    pasek.Value = 100;

                    lblStatus.Text = "[SUKCES]: Zainstalowano '" + nazwaPliku + "' w src_repo!";
                    lblStatus.ForeColor = Color.Lime;
                    MessageBox.Show("Pobrano pomyślnie: " + nazwaPliku + "\nUruchom ponownie pulpit, aby zobaczyć nową ikonę.", "HobbyOS Net-Fetch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                pasek.Visible = false;
                lblStatus.Text = "[BŁĄD]: " + ex.Message;
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnPobierz.Enabled = true;
                txtUrl.ReadOnly = false;
            }
        };

        Application.Run(f);
    }
}
