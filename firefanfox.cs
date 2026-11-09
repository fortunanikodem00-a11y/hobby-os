using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

public class FirefoxHobbyOS
{
    private static readonly Color FxBgTop = Color.FromArgb(28, 27, 34);
    private static readonly Color FxBgNav = Color.FromArgb(43, 42, 51);
    private static readonly Color FxUrlBg = Color.FromArgb(28, 27, 34);
    private static readonly Color FxUrlBorder = Color.FromArgb(82, 82, 94);
    private static readonly Color FxAccent = Color.FromArgb(255, 113, 57);
    private static readonly Color FxText = Color.FromArgb(251, 251, 254);
    private static readonly Color FxTextMuted = Color.FromArgb(177, 177, 179);

    private static readonly string HomePageUrl = "https://duckduckgo.com";

    private static Panel pnlTabBar = null!;
    private static Panel pnlBrowserContainer = null!;
    private static TextBox txtUrl = null!;
    private static Panel pnlUrlBox = null!;
    private static Label lblLock = null!;
    private static Button btnAddTab = null!;

    private static List<BrowserTab> tabs = new List<BrowserTab>();
    private static BrowserTab? activeTab = null;

    private class BrowserTab
    {
        public Panel TabHeader = null!;
        public Label lblTitle = null!;
        public WebBrowser Web = null!;
        public string CurrentUrl = "";
    }

    [STAThread]
    public static void Main()
    {
        UstawNowoczesnySilnik();
        Application.EnableVisualStyles();

        Form f = new Form
        {
            Text = "Mozilla Firefox — HobbyOS Edition",
            Size = new Size(1240, 820),
            BackColor = FxBgTop,
            StartPosition = FormStartPosition.CenterScreen
        };

        pnlTabBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = FxBgTop,
            Padding = new Padding(8, 6, 8, 0)
        };
        f.Controls.Add(pnlTabBar);

        btnAddTab = new Button
        {
            Text = "+",
            Size = new Size(30, 30),
            BackColor = Color.Transparent,
            ForeColor = FxText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btnAddTab.FlatAppearance.BorderSize = 0;
        btnAddTab.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 49, 60);
        btnAddTab.Click += (s, e) => DodajNowaKarte(HomePageUrl);
        pnlTabBar.Controls.Add(btnAddTab);

        Panel pnlNavBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = FxBgNav
        };
        f.Controls.Add(pnlNavBar);
        pnlNavBar.BringToFront();

        Button btnBack = StworzPrzyciskNav("←", 10);
        Button btnForward = StworzPrzyciskNav("→", 46);
        Button btnRefresh = StworzPrzyciskNav("↻", 82);
        Button btnHome = StworzPrzyciskNav("⌂", 118);

        pnlNavBar.Controls.AddRange(new Control[] { btnBack, btnForward, btnRefresh, btnHome });

        pnlUrlBox = new Panel
        {
            Location = new Point(162, 6),
            Size = new Size(980, 32),
            BackColor = FxUrlBg,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlUrlBox.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color borderColor = txtUrl.Focused ? FxAccent : FxUrlBorder;
            using (Pen p = new Pen(borderColor, txtUrl.Focused ? 2 : 1))
            {
                e.Graphics.DrawRectangle(p, 0, 0, pnlUrlBox.Width - 1, pnlUrlBox.Height - 1);
            }
        };
        pnlNavBar.Controls.Add(pnlUrlBox);

        lblLock = new Label
        {
            Text = "🔒",
            Location = new Point(8, 6),
            Size = new Size(24, 20),
            ForeColor = FxAccent,
            Font = new Font("Segoe UI Emoji", 9)
        };
        pnlUrlBox.Controls.Add(lblLock);

        txtUrl = new TextBox
        {
            Location = new Point(36, 6),
            Size = new Size(930, 22),
            BackColor = FxUrlBg,
            ForeColor = FxText,
            Font = new Font("Segoe UI", 10),
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        txtUrl.GotFocus += (s, e) => pnlUrlBox.Invalidate();
        txtUrl.LostFocus += (s, e) => pnlUrlBox.Invalidate();
        pnlUrlBox.Controls.Add(txtUrl);

        Button btnMenu = StworzPrzyciskNav("≡", 1152);
        btnMenu.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlNavBar.Controls.Add(btnMenu);

        Panel pnlBookmarks = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = FxBgNav,
            Padding = new Padding(12, 2, 12, 2)
        };
        pnlBookmarks.Paint += (s, e) =>
        {
            using (Pen p = new Pen(Color.FromArgb(20, 20, 25), 1))
            {
                e.Graphics.DrawLine(p, 0, pnlBookmarks.Height - 1, pnlBookmarks.Width, pnlBookmarks.Height - 1);
            }
        };
        f.Controls.Add(pnlBookmarks);
        pnlBookmarks.BringToFront();

        DodajZakladke(pnlBookmarks, "🦆 DuckDuckGo", "https://duckduckgo.com", 12);
        DodajZakladke(pnlBookmarks, "🌐 Wikipedia", "https://wikipedia.org", 130);
        DodajZakladke(pnlBookmarks, "💻 GitHub", "https://github.com", 240);
        DodajZakladke(pnlBookmarks, "📰 Wykop", "https://wykop.pl", 330);

        pnlBrowserContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FxBgNav
        };
        f.Controls.Add(pnlBrowserContainer);
        pnlBrowserContainer.BringToFront();

        btnBack.Click += (s, e) => { if (activeTab != null && activeTab.Web.CanGoBack) activeTab.Web.GoBack(); };
        btnForward.Click += (s, e) => { if (activeTab != null && activeTab.Web.CanGoForward) activeTab.Web.GoForward(); };
        btnRefresh.Click += (s, e) => { if (activeTab != null) activeTab.Web.Refresh(); };
        btnHome.Click += (s, e) => Nawiguj(HomePageUrl);

        txtUrl.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Nawiguj(txtUrl.Text);
                e.SuppressKeyPress = true;
            }
        };

        DodajNowaKarte(HomePageUrl);

        Application.Run(f);
    }

    private static void DodajNowaKarte(string url)
    {
        BrowserTab tab = new BrowserTab();

        Panel header = new Panel
        {
            Size = new Size(210, 34),
            BackColor = FxBgNav,
            Margin = new Padding(0, 0, 4, 0),
            Cursor = Cursors.Hand
        };

        Panel topAccent = new Panel
        {
            Dock = DockStyle.Top,
            Height = 2,
            BackColor = FxAccent,
            Tag = "accent"
        };
        header.Controls.Add(topAccent);

        Label lblIcon = new Label
        {
            Text = "🦊",
            Location = new Point(8, 8),
            Size = new Size(20, 20),
            ForeColor = FxAccent,
            Font = new Font("Segoe UI Emoji", 8)
        };
        header.Controls.Add(lblIcon);

        Label lblTitle = new Label
        {
            Text = "Ładowanie...",
            Location = new Point(30, 8),
            Size = new Size(148, 20),
            ForeColor = FxText,
            Font = new Font("Segoe UI", 8.5f),
            AutoEllipsis = true
        };
        header.Controls.Add(lblTitle);

        Button btnClose = new Button
        {
            Text = "×",
            Location = new Point(182, 6),
            Size = new Size(22, 22),
            BackColor = Color.Transparent,
            ForeColor = FxTextMuted,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 69, 82);
        header.Controls.Add(btnClose);

        WebBrowser web = new WebBrowser
        {
            Dock = DockStyle.Fill,
            ScriptErrorsSuppressed = true,
            Visible = false
        };

        tab.TabHeader = header;
        tab.lblTitle = lblTitle;
        tab.Web = web;

        EventHandler aktywuj = (s, e) => AktywujKarte(tab);
        header.Click += aktywuj;
        lblTitle.Click += aktywuj;
        lblIcon.Click += aktywuj;

        btnClose.Click += (s, e) => ZamknijKarte(tab);

        web.Navigated += (s, e) =>
        {
            if (web.Url != null)
            {
                string u = web.Url.ToString();
                tab.CurrentUrl = u;
                lblTitle.Text = string.IsNullOrEmpty(web.DocumentTitle) ? web.Url.Host : web.DocumentTitle;

                if (activeTab == tab)
                {
                    txtUrl.Text = tab.CurrentUrl;
                    lblLock.Text = u.StartsWith("https://") ? "🔒" : "🦊";
                }
            }
        };

        web.DocumentCompleted += (s, e) =>
        {
            if (web.Url != null && !string.IsNullOrEmpty(web.DocumentTitle))
            {
                lblTitle.Text = web.DocumentTitle;
            }
        };

        tabs.Add(tab);
        pnlTabBar.Controls.Add(header);
        pnlBrowserContainer.Controls.Add(web);

        PrzeliczPozycjeKart();
        AktywujKarte(tab);
        Nawiguj(url);
    }

    private static void AktywujKarte(BrowserTab tab)
    {
        activeTab = tab;
        foreach (var t in tabs)
        {
            bool isCurrent = (t == tab);
            t.Web.Visible = isCurrent;
            t.TabHeader.BackColor = isCurrent ? FxBgNav : FxBgTop;
            t.lblTitle.ForeColor = isCurrent ? FxText : FxTextMuted;
            foreach (Control c in t.TabHeader.Controls)
            {
if (c.Tag != null && c.Tag.ToString() == "accent") c.Visible = isCurrent;
}
}
txtUrl.Text = tab.CurrentUrl;
lblLock.Text = tab.CurrentUrl.StartsWith("https://") ? "🔒" : "🦊";
}
private static void ZamknijKarte(BrowserTab tab)
{
if (tabs.Count == 1)
{
Nawiguj(HomePageUrl);
return;
}
int idx = tabs.IndexOf(tab);
tabs.Remove(tab);
pnlTabBar.Controls.Remove(tab.TabHeader);
pnlBrowserContainer.Controls.Remove(tab.Web);
tab.Web.Dispose();
PrzeliczPozycjeKart();
if (activeTab == tab)
{
AktywujKarte(tabs[Math.Max(0, idx - 1)]);
}
}
private static void PrzeliczPozycjeKart()
{
int x = 8;
foreach (var t in tabs)
{
t.TabHeader.Location = new Point(x, 6);
x += t.TabHeader.Width + 4;
}
btnAddTab.Location = new Point(x + 2, 6);
}
private static void Nawiguj(string adres)
{
if (activeTab == null) return;
adres = adres.Trim();
if (string.IsNullOrEmpty(adres)) return;
if (!adres.StartsWith("http://") && !adres.StartsWith("https://"))
{
if (adres.Contains(".") && !adres.Contains(" "))
{
adres = "https://" + adres;
}
else
{
adres = "duckduckgo.com" + Uri.EscapeDataString(adres);
}
}
activeTab.Web.Navigate(adres);
}
private static Button StworzPrzyciskNav(string tekst, int x)
{
Button btn = new Button
{
Text = tekst,
Location = new Point(x, 6),
Size = new Size(32, 32),
BackColor = Color.Transparent,
ForeColor = FxText,
FlatStyle = FlatStyle.Flat,
Font = new Font("Segoe UI", 11, FontStyle.Regular),
Cursor = Cursors.Hand
};
btn.FlatAppearance.BorderSize = 0;
btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(66, 65, 77);
return btn;
}
private static void DodajZakladke(Panel parent, string nazwa, string url, int x)
{
Button btn = new Button
{
Text = nazwa,
Location = new Point(x, 2),
AutoSize = true,
Height = 22,
BackColor = Color.Transparent,
ForeColor = FxTextMuted,
FlatStyle = FlatStyle.Flat,
Font = new Font("Segoe UI", 8.5f),
Cursor = Cursors.Hand
};
btn.FlatAppearance.BorderSize = 0;
btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(66, 65, 77);
btn.Click += (s, e) => Nawiguj(url);
parent.Controls.Add(btn);
}
private static void UstawNowoczesnySilnik()
{
try
{
string appName = System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName);
using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
{
key?.SetValue(appName, 11001, RegistryValueKind.DWord);
}
}
catch { }
}
}
