using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class HobbyAdvancedExplorer : Form
{
    private ListView lvFiles = null!;
    private TextBox txtPath = null!;
    private Button btnUp = null!;
    private string currentDirectory = "";
    private static string repoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos\Hobby OS\Hobby OS\bin\Debug\net10.0-windows\src_repo");

    public HobbyAdvancedExplorer()
    {
        this.Text = "Menedżer Plików i Kontenerów — HobbyOS";
        this.Size = new Size(980, 580);
        this.BackColor = Color.FromArgb(20, 20, 26);
        this.StartPosition = FormStartPosition.CenterScreen;

        Panel topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Color.FromArgb(14, 14, 18),
            Padding = new Padding(8)
        };

        btnUp = new Button
        {
            Text = "↑ W górę",
            Dock = DockStyle.Left,
            Width = 85,
            BackColor = Color.FromArgb(38, 38, 46),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        btnUp.FlatAppearance.BorderSize = 0;
        btnUp.Click += (s, e) => GoUp();

        txtPath = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 28, 36),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10),
            BorderStyle = BorderStyle.FixedSingle
        };
        txtPath.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoadDirectory(txtPath.Text); };

        Panel pathWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
        pathWrapper.Controls.Add(txtPath);

        topPanel.Controls.Add(pathWrapper);
        topPanel.Controls.Add(btnUp);
        this.Controls.Add(topPanel);

        lvFiles = new ListView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 26),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10),
            View = View.Details,
            FullRowSelect = true,
            BorderStyle = BorderStyle.None
        };
        lvFiles.Columns.Add("Nazwa elementu", 380);
        lvFiles.Columns.Add("Kontener / Typ", 180);
        lvFiles.Columns.Add("Rozmiar", 120);
        lvFiles.Columns.Add("Modyfikacja", 160);
        lvFiles.DoubleClick += LvFiles_DoubleClick;

        this.Controls.Add(lvFiles);

        string startDir = Directory.Exists(repoPath) ? repoPath : AppDomain.CurrentDomain.BaseDirectory;
        LoadDirectory(startDir);
    }

    private void LoadDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        currentDirectory = path;
        txtPath.Text = path;
        lvFiles.Items.Clear();

        try
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                ListViewItem item = new ListViewItem("📁 " + di.Name);
                item.SubItems.Add("Folder systemowy");
                item.SubItems.Add("");
                item.SubItems.Add(di.LastWriteTime.ToString("dd.MM.yyyy HH:mm"));
                item.Tag = dir;
                lvFiles.Items.Add(item);
            }

            foreach (string file in Directory.GetFiles(path))
            {
                FileInfo fi = new FileInfo(file);
                string ext = fi.Extension.ToLower();
                string typeDesc = RozpoznajTypPliku(ext);

                ListViewItem item = new ListViewItem(DopasujIkone(ext) + " " + fi.Name);
                item.SubItems.Add(typeDesc);
                item.SubItems.Add(FormatSize(fi.Length));
                item.SubItems.Add(fi.LastWriteTime.ToString("dd.MM.yyyy HH:mm"));
                item.Tag = file;
                lvFiles.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Błąd odczytu repozytorium: " + ex.Message, "HobbyOS Kernel Safeguard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string RozpoznajTypPliku(string ext)
    {
        return ext switch
        {
            ".cs" => "Skrypt / Apka HobbyOS",
            ".txt" or ".json" or ".md" => "Dokument tekstowy",
            ".html" or ".htm" => "Kontener HTML Web",
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => "Plik graficzny / Obraz",
            ".mp3" or ".wav" or ".ogg" => "Kontener Audio / Muzyka",
            ".mp4" or ".avi" or ".mkv" or ".wmv" => "Kontener Wideo / Film",
            ".pdf" => "Dokument PDF",
            _ => "Nieznany kontener danych"
        };
    }

    private string DopasujIkone(string ext)
    {
        return ext switch
        {
            ".cs" => "⚙️",
            ".txt" or ".json" or ".md" => "📝",
            ".html" or ".htm" => "🌐",
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => "🖼️",
            ".mp3" or ".wav" or ".ogg" => "🎵",
            ".mp4" or ".avi" or ".mkv" or ".wmv" => "🎬",
            ".pdf" => "📕",
            _ => "📦"
        };
    }

    private void GoUp()
    {
        DirectoryInfo? parent = Directory.GetParent(currentDirectory);
        if (parent != null) LoadDirectory(parent.FullName);
    }

    private void LvFiles_DoubleClick(object? sender, EventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0) return;
        string fullPath = lvFiles.SelectedItems[0].Tag?.ToString() ?? "";

        if (Directory.Exists(fullPath))
        {
            LoadDirectory(fullPath);
        }
        else if (File.Exists(fullPath))
        {
            UruchomWlasnySkryptRozpoznawania(fullPath);
        }
    }

    private void UruchomWlasnySkryptRozpoznawania(string file)
    {
        string ext = Path.GetExtension(file).ToLower();

        try
        {
            if (ext == ".cs")
            {
                KompilujIValformujCs(file);
            }
            else if (ext == ".txt" || ext == ".json" || ext == ".md")
            {
                OtworzPodgladTekstowy(file);
            }
            else if (ext == ".html" || ext == ".htm")
            {
                OtworzPodgladHtml(file);
            }
            else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
            {
                OtworzPodgladObrazu(file);
            }
            else if (ext == ".mp4" || ext == ".avi" || ext == ".mkv" || ext == ".wmv" || ext == ".mp3" || ext == ".wav")
            {
                OtworzWeWlasnymOdtwarzaczu(file);
            }
            else if (ext == ".pdf")
            {
                OtworzPlikPdf(file);
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Skrypt uruchamiający zgłosił błąd dla {Path.GetFileName(file)}:\n{ex.Message}", "HobbyOS Script Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void KompilujIValformujCs(string csFile)
    {
        string appName = Path.GetFileNameWithoutExtension(csFile);
        string tempDir = Path.Combine(Path.GetTempPath(), "HobbyOSApps", appName);
        Directory.CreateDirectory(tempDir);
        File.Copy(csFile, Path.Combine(tempDir, "Program.cs"), true);

        char lt = (char)60, gt = (char)62;
        string csproj = $"{lt}Project Sdk='Microsoft.NET.Sdk'{gt}{lt}PropertyGroup{gt}{lt}OutputType{gt}WinExe{lt}/OutputType{gt}{lt}TargetFramework{gt}net10.0-windows{lt}/TargetFramework{gt}{lt}UseWindowsForms{gt}true{lt}/UseWindowsForms{gt}{lt}/PropertyGroup{gt}{lt}/Project{gt}";
        File.WriteAllText(Path.Combine(tempDir, appName + ".csproj"), csproj);

        Process.Start(new ProcessStartInfo { FileName = "dotnet", Arguments = "run", WorkingDirectory = tempDir, UseShellExecute = true });
    }

    private void OtworzPodgladTekstowy(string file)
    {
        Form viewer = new Form { Text = $"Podgląd pliku — {Path.GetFileName(file)}", Size = new Size(650, 450), BackColor = Color.FromArgb(30, 30, 36), StartPosition = FormStartPosition.CenterParent };
        TextBox txt = new TextBox { Multiline = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 36), ForeColor = Color.White, Font = new Font("Consolas", 11), BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Both, ReadOnly = true, Text = File.ReadAllText(file) };
        viewer.Controls.Add(txt);
        viewer.Show();
    }

    private void OtworzPodgladHtml(string file)
    {
        Form webViewer = new Form { Text = $"Web Kontener HTML — {Path.GetFileName(file)}", Size = new Size(800, 600), StartPosition = FormStartPosition.CenterParent };
        WebBrowser wb = new WebBrowser { Dock = DockStyle.Fill };
        wb.Url = new Uri(file);
        webViewer.Controls.Add(wb);
        webViewer.Show();
    }

    private void OtworzPodgladObrazu(string file)
    {
        Form imgViewer = new Form { Text = $"Podgląd obrazu — {Path.GetFileName(file)}", Size = new Size(600, 500), BackColor = Color.FromArgb(16, 16, 16), StartPosition = FormStartPosition.CenterParent };
        PictureBox pb = new PictureBox { Dock = DockStyle.Fill, Image = Image.FromFile(file), SizeMode = PictureBoxSizeMode.Zoom };
        imgViewer.Controls.Add(pb);
        imgViewer.Show();
    }

    private void OtworzWeWlasnymOdtwarzaczu(string file)
    {
        Form playerForm = new Form { Text = $"HobbyPlayer — {Path.GetFileName(file)}", Size = new Size(800, 560), BackColor = Color.FromArgb(10, 10, 14), StartPosition = FormStartPosition.CenterParent };
        var wmp = new MyWmpControl();
        wmp.Dock = DockStyle.Fill;
playerForm.Controls.Add(wmp);
playerForm.FormClosing += (s, e) => { wmp.Stop(); };
playerForm.Show();
wmp.Play(file);
}
// NOWA FUNKCJA: WBUDOWANY KONTENER PDF DLA HOBBYOS
private void OtworzPlikPdf(string file)
{
Form pdfForm = new Form
{
Text = $"HobbyPDF Reader — {Path.GetFileName(file)}",
Size = new Size(850, 700),
StartPosition = FormStartPosition.CenterParent
};
WebBrowser pdfBrowser = new WebBrowser
{
Dock = DockStyle.Fill
};
// Przekazanie ścieżki pliku bezpośrednio jako URI – silnik pod spodem wyrenderuje pełny widok PDF
pdfBrowser.Url = new Uri(file);
pdfForm.Controls.Add(pdfBrowser);
pdfForm.Show();
}
private string FormatSize(long bytes)
{
string[] suffix = { "B", "KB", "MB", "GB" };
double dblSiz = bytes;
int i;
for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024) { dblSiz = bytes / 1024.0; }
return $"{Math.Round(dblSiz, 1)} {suffix[i]}";
}
[STAThread]
public static void Main()
{
Application.EnableVisualStyles();
Application.Run(new HobbyAdvancedExplorer());
}
}
public class MyWmpControl : UserControl
{
private object wmpObject;
public MyWmpControl()
{
var t = Type.GetTypeFromProgID("WMPlayer.OCX.7");
if (t == null) throw new Exception("Brak kodeków Windows Media Player w systemie hosta.");
wmpObject = Activator.CreateInstance(t)!;
var wmpControl = (Control)wmpObject;
wmpControl.Dock = DockStyle.Fill;
this.Controls.Add(wmpControl);
}
public void Play(string url)
{
wmpObject.GetType().GetProperty("URL")?.SetValue(wmpObject, url, null);
}
public void Stop()
{
wmpObject.GetType().GetMethod("close")?.Invoke(wmpObject, null);
}
}
