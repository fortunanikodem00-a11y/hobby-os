using System;
using System.Drawing;
using System.Net.Http;
using System.Windows.Forms;

class Program
{
    static void Main()
    {
        Form f = new Form();
        f.Text = "HobbyOS - Real Downloader";
        f.Size = new Size(550, 260);
        f.BackColor = Color.FromArgb(25, 25, 30);
        f.StartPosition = FormStartPosition.CenterScreen;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;

        Label l = new Label { Text = "Wklej bezpośredni link (Raw URL) do pliku .cs:", ForeColor = Color.White, Location = new Point(25, 25), Size = new Size(400, 20) };
        f.Controls.Add(l);

        TextBox t = new TextBox { Location = new Point(25, 55), Size = new Size(480, 25), BackColor = Color.FromArgb(40, 40, 45), ForeColor = Color.Cyan, Font = new Font("Consolas", 10) };
        t.Text = "https://githubusercontent.com";
        f.Controls.Add(t);

        Button b = new Button { Text = "POBIERZ KOD ŹRÓDŁOWY", Location = new Point(25, 105), Size = new Size(480, 45), BackColor = Color.FromArgb(55, 55, 60), ForeColor = Color.Lime, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        b.Click += async (s, e) => {
            b.Text = "POBIERANIE...";
            try {
                using (HttpClient client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("User-Agent", "HobbyOS-App");
                    string pobranyKod = await client.GetStringAsync(t.Text);
                    MessageBox.Show("Sukces! Pobrany tekst ma: " + pobranyKod.Length + " znaków.", "HobbyOS Downloader Core");
                }
            } catch (Exception ex) { MessageBox.Show("Błąd sieci HTTP: " + ex.Message, "Error"); }
            b.Text = "POBIERZ KOD ŹRÓDŁOWY";
        };
        f.Controls.Add(b);

        f.ShowDialog();
    }
}
