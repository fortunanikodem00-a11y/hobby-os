using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class NotepadWin11
{
    private static TextBox editor = null!;
    private static Label lblStatus = null!;
    private static string currentFile = "";
    private static Form mainForm = null!;
    private static string defaultRepo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos\Hobby OS\Hobby OS\bin\Debug\net10.0-windows\src_repo");

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();

        Color bgDark = Color.FromArgb(30, 30, 30);
        Color bgMenu = Color.FromArgb(42, 42, 46);
        Color fgText = Color.FromArgb(240, 240, 240);

        mainForm = new Form
        {
            Text = "Bez tytułu — Notatnik (HobbyOS)",
            Size = new Size(960, 640),
            BackColor = bgDark,
            StartPosition = FormStartPosition.CenterScreen
        };

        MenuStrip menu = new MenuStrip
        {
            BackColor = bgMenu,
            ForeColor = fgText,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(6, 2, 0, 2)
        };

        ToolStripMenuItem mFile = new ToolStripMenuItem("Plik") { ForeColor = fgText };
        ToolStripMenuItem mNew = new ToolStripMenuItem("Nowy", null, (s, e) => NowyPlik()) { ShortcutKeys = Keys.Control | Keys.N };
        ToolStripMenuItem mOpen = new ToolStripMenuItem("Otwórz...", null, (s, e) => OtworzPlik()) { ShortcutKeys = Keys.Control | Keys.O };
        ToolStripMenuItem mSave = new ToolStripMenuItem("Zapisz", null, (s, e) => ZapiszPlik(false)) { ShortcutKeys = Keys.Control | Keys.S };
        ToolStripMenuItem mSaveAs = new ToolStripMenuItem("Zapisz jako...", null, (s, e) => ZapiszPlik(true));
        ToolStripMenuItem mExit = new ToolStripMenuItem("Zakończ", null, (s, e) => mainForm.Close());
        mFile.DropDownItems.AddRange(new ToolStripItem[] { mNew, mOpen, mSave, mSaveAs, new ToolStripSeparator(), mExit });

        ToolStripMenuItem mEdit = new ToolStripMenuItem("Edycja") { ForeColor = fgText };
        ToolStripMenuItem mCopy = new ToolStripMenuItem("Kopiuj", null, (s, e) => editor.Copy()) { ShortcutKeys = Keys.Control | Keys.C };
        ToolStripMenuItem mPaste = new ToolStripMenuItem("Wklej", null, (s, e) => editor.Paste()) { ShortcutKeys = Keys.Control | Keys.V };
        ToolStripMenuItem mSelectAll = new ToolStripMenuItem("Zaznacz wszystko", null, (s, e) => editor.SelectAll()) { ShortcutKeys = Keys.Control | Keys.A };
        ToolStripMenuItem mDate = new ToolStripMenuItem("Godzina/Data", null, (s, e) => editor.SelectedText = DateTime.Now.ToString("HH:mm dd.MM.yyyy")) { ShortcutKeys = Keys.F5 };
        mEdit.DropDownItems.AddRange(new ToolStripItem[] { mCopy, mPaste, new ToolStripSeparator(), mSelectAll, mDate });

        ToolStripMenuItem mView = new ToolStripMenuItem("Widok") { ForeColor = fgText };
        ToolStripMenuItem mWrap = new ToolStripMenuItem("Zawijanie wierszy") { Checked = true, CheckOnClick = true };
        mWrap.CheckedChanged += (s, e) => editor.WordWrap = mWrap.Checked;
        ToolStripMenuItem mZoomIn = new ToolStripMenuItem("Powiększ czcionkę", null, (s, e) => ZmienCzcionke(2)) { ShortcutKeys = Keys.Control | Keys.Oemplus };
        ToolStripMenuItem mZoomOut = new ToolStripMenuItem("Pomniejsz czcionkę", null, (s, e) => ZmienCzcionke(-2)) { ShortcutKeys = Keys.Control | Keys.OemMinus };
        mView.DropDownItems.AddRange(new ToolStripItem[] { mWrap, new ToolStripSeparator(), mZoomIn, mZoomOut });

        menu.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mView });
        mainForm.MainMenuStrip = menu;
        mainForm.Controls.Add(menu);

        Panel statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            BackColor = bgMenu
        };
        lblStatus = new Label
        {
            Text = "Linia 1, Kolumna 1   |   Znaki: 0   |   UTF-8",
            Dock = DockStyle.Fill,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 15, 0)
        };
        statusBar.Controls.Add(lblStatus);
        mainForm.Controls.Add(statusBar);

        editor = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            BackColor = bgDark,
            ForeColor = fgText,
            Font = new Font("Consolas", 11.5f),
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Both,
            AcceptsTab = true,
            WordWrap = true
        };
        editor.TextChanged += (s, e) => AktualizujStatus();
        editor.KeyUp += (s, e) => AktualizujStatus();
        editor.MouseUp += (s, e) => AktualizujStatus();
        mainForm.Controls.Add(editor);
        editor.BringToFront();

        Application.Run(mainForm);
    }

    private static void NowyPlik()
    {
        editor.Clear();
        currentFile = "";
        mainForm.Text = "Bez tytułu — Notatnik (HobbyOS)";
        AktualizujStatus();
    }

    private static void OtworzPlik()
    {
        using OpenFileDialog ofd = new OpenFileDialog
        {
            Filter = "Pliki kodu C# (*.cs)|*.cs|Pliki tekstowe (*.txt)|*.txt|Wszystkie pliki (*.*)|*.*",
            InitialDirectory = Directory.Exists(defaultRepo) ? defaultRepo : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            currentFile = ofd.FileName;
            editor.Text = File.ReadAllText(currentFile);
            mainForm.Text = Path.GetFileName(currentFile) + " — Notatnik (HobbyOS)";
            AktualizujStatus();
        }
    }

    private static void ZapiszPlik(bool saveAs)
    {
        if (saveAs || string.IsNullOrEmpty(currentFile))
        {
            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Plik kodu C# (*.cs)|*.cs|Plik tekstowy (*.txt)|*.txt|Wszystkie pliki (*.*)|*.*",
                InitialDirectory = Directory.Exists(defaultRepo) ? defaultRepo : Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = "nowa-aplikacja.cs"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            currentFile = sfd.FileName;
        }
        File.WriteAllText(currentFile, editor.Text);
        mainForm.Text = Path.GetFileName(currentFile) + " — Notatnik (HobbyOS)";
    }

    private static void ZmienCzcionke(float delta)
    {
        float newSize = Math.Max(8f, Math.Min(36f, editor.Font.Size + delta));
        editor.Font = new Font("Consolas", newSize);
    }

    private static void AktualizujStatus()
    {
        int pos = editor.SelectionStart;
        int line = editor.GetLineFromCharIndex(pos) + 1;
        int col = pos - editor.GetFirstCharIndexOfCurrentLine() + 1;
        lblStatus.Text = $"Linia {line}, Kolumna {col}   |   Znaki: {editor.Text.Length}   |   UTF-8";
    }
}
