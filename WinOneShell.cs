using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace HobbyOS.PullPity
{
    public sealed class DesktopIconItem
    {
        public string FilePath;
        public string FileName;
        public string DisplayName;
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public DesktopIconItem(string filePath, float x, float y, float width, float height)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            DisplayName = Path.GetFileNameWithoutExtension(filePath);
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public RectangleF GetScreenBounds(float scrollY)
        {
            return new RectangleF(X, Y - scrollY, Width, Height);
        }
    }

    public sealed class WindowsShellForm : Form
    {
        private const int TaskbarHeight = 44;
        private const int StartButtonWidth = 110;
        private const float IconWidth = 96f;
        private const float IconHeight = 92f;
        private const float ColSpacing = 114f;
        private const float RowSpacing = 108f;
        private const float MarginLeft = 24f;
        private const float MarginTop = 24f;

        private readonly List<DesktopIconItem> _icons = new List<DesktopIconItem>();
        private readonly Dictionary<string, PointF> _savedPositions = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer _clockTimer = new System.Windows.Forms.Timer();
        private readonly string _srcRepoPath;

        private float _scrollY = 0f;
        private float _maxScrollY = 0f;

        private DesktopIconItem _selectedIcon = null;
        private DesktopIconItem _draggingIcon = null;
        private PointF _dragOffset;
        private bool _isDragging = false;

        private bool _startMenuOpen = false;
        private string _statusMessage = "Gotowe — dwuklik uruchamia *.cs z src_repo przez dotnet run";

        public WindowsShellForm()
        {
            Text = "HobbyOS — Windows 1:1 Shell Pull-Pit";
            ClientSize = new Size(1200, 740);
            MinimumSize = new Size(640, 400);
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(14, 78, 152);

            _srcRepoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src_repo");
            ScanSrcRepo();

            _clockTimer.Interval = 1000;
            _clockTimer.Tick += OnClockTick;
            _clockTimer.Start();
        }

        private void OnClockTick(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void ScanSrcRepo()
        {
            // Zachowaj pozycje już istniejących ikon
            foreach (var old in _icons)
            {
                _savedPositions[old.FilePath] = new PointF(old.X, old.Y);
            }

            _icons.Clear();
            Directory.CreateDirectory(_srcRepoPath);

            string[] files;
            try
            {
                files = Directory.GetFiles(_srcRepoPath, "*.cs", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _statusMessage = "Błąd skanowania src_repo: " + ex.Message;
                RecalculateScrollBounds();
                Invalidate();
                return;
            }

            int cols = Math.Max(1, (ClientSize.Width - 60) / (int)ColSpacing);

            for (int i = 0; i < files.Length; i++)
            {
                string fullPath = files[i];
                float defaultX = MarginLeft + (i % cols) * ColSpacing;
                float defaultY = MarginTop + (i / cols) * RowSpacing;

                if (_savedPositions.TryGetValue(fullPath, out PointF pos))
                {
                    defaultX = pos.X;
                    defaultY = pos.Y;
                }

                _icons.Add(new DesktopIconItem(fullPath, defaultX, defaultY, IconWidth, IconHeight));
            }

            if (_selectedIcon != null)
            {
                DesktopIconItem match = null;
                foreach (var it in _icons)
                {
                    if (string.Equals(it.FilePath, _selectedIcon.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        match = it;
                        break;
                    }
                }
                _selectedIcon = match;
            }

            _statusMessage = $"src_repo: znaleziono {_icons.Count} plik(ów) *.cs";
            RecalculateScrollBounds();
            Invalidate();
        }

        private void RecalculateScrollBounds()
        {
            float bottomMost = MarginTop + IconHeight + 40f;
            foreach (var icon in _icons)
            {
                float b = icon.Y + icon.Height + 36f;
                if (b > bottomMost) bottomMost = b;
            }

            int workHeight = Math.Max(100, ClientSize.Height - TaskbarHeight);
            _maxScrollY = Math.Max(0f, bottomMost - workHeight);
            _scrollY = Math.Clamp(_scrollY, 0f, _maxScrollY);
        }

        private void ScrollBy(float deltaY)
        {
            RecalculateScrollBounds();
            _scrollY = Math.Clamp(_scrollY + deltaY, 0f, _maxScrollY);
            Invalidate();
        }

        private void LaunchIconScript(DesktopIconItem icon)
        {
            if (icon == null || !File.Exists(icon.FilePath))
            {
                _statusMessage = "Nie znaleziono pliku źródłowego.";
                Invalidate();
                return;
            }

            try
            {
                string safeName = SanitizeFolderName(Path.GetFileNameWithoutExtension(icon.FilePath));
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "App";

                string buildDir = Path.Combine(Path.GetTempPath(), "HobbyOSApps", safeName);
                Directory.CreateDirectory(buildDir);

                string csprojPath = Path.Combine(buildDir, safeName + ".csproj");
                string programCsPath = Path.Combine(buildDir, "Program.cs");

                string csprojContent =
                    "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                    "  <PropertyGroup>\n" +
                    "    <OutputType>WinExe</OutputType>\n" +
                    "    <TargetFramework>net10.0-windows</TargetFramework>\n" +
                    "    <UseWindowsForms>true</UseWindowsForms>\n" +
                    "    <ImplicitUsings>enable</ImplicitUsings>\n" +
                    "  </PropertyGroup>\n" +
                    "</Project>\n";

                File.WriteAllText(csprojPath, csprojContent);
                File.Copy(icon.FilePath, programCsPath, true);

                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{csprojPath}\"",
                    WorkingDirectory = buildDir,
                    UseShellExecute = true
                };

                Process.Start(psi);
                _statusMessage = $"Uruchomiono: {icon.FileName} (HobbyOSApps\\{safeName})";
            }
            catch (Exception ex)
            {
                _statusMessage = "Błąd uruchamiania: " + ex.Message;
            }

            Invalidate();
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(' ', '_');
        }

        private Rectangle GetStartButtonRect()
        {
            return new Rectangle(0, ClientSize.Height - TaskbarHeight, StartButtonWidth, TaskbarHeight);
        }

        private Rectangle GetStartMenuRect()
        {
            int menuW = 310;
            int menuH = 220;
            return new Rectangle(6, ClientSize.Height - TaskbarHeight - menuH - 6, menuW, menuH);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateScrollBounds();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBy(-e.Delta * 0.45f);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Up)
            {
                ScrollBy(-48f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                ScrollBy(48f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                ScrollBy(-240f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                ScrollBy(240f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                ScrollBy(-_scrollY);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                ScrollBy(_maxScrollY);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ScanSrcRepo();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && _selectedIcon != null)
            {
                LaunchIconScript(_selectedIcon);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _startMenuOpen = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left) return;

            Rectangle startRect = GetStartButtonRect();
            if (startRect.Contains(e.Location))
            {
                _startMenuOpen = !_startMenuOpen;
                Invalidate();
                return;
            }

            if (_startMenuOpen)
            {
                Rectangle menuRect = GetStartMenuRect();
                if (menuRect.Contains(e.Location))
                {
                    HandleStartMenuClick(e.Location, menuRect);
                    return;
                }
                else
                {
                    _startMenuOpen = false;
                }
            }

            int workBottom = ClientSize.Height - TaskbarHeight;
            if (e.Y < workBottom)
            {
                DesktopIconItem hit = null;
                for (int i = _icons.Count - 1; i >= 0; i--)
                {
                    if (_icons[i].GetScreenBounds(_scrollY).Contains(e.Location))
                    {
                        hit = _icons[i];
                        break;
                    }
                }

                _selectedIcon = hit;
                if (hit != null)
                {
                    // Przenieś na wierzch rysowania
                    _icons.Remove(hit);
                    _icons.Add(hit);

                    _draggingIcon = hit;
                    _isDragging = true;
                    _dragOffset = new PointF(e.X - hit.X, e.Y - (hit.Y - _scrollY));
                }
            }

            Invalidate();
        }

        private void HandleStartMenuClick(Point pt, Rectangle menuRect)
        {
            Rectangle refreshBtn = new Rectangle(menuRect.X + 14, menuRect.Y + 56, menuRect.Width - 28, 34);
            Rectangle openRepoBtn = new Rectangle(menuRect.X + 14, menuRect.Y + 98, menuRect.Width - 28, 34);
            Rectangle runSelectedBtn = new Rectangle(menuRect.X + 14, menuRect.Y + 140, menuRect.Width - 28, 34);
            Rectangle exitBtn = new Rectangle(menuRect.X + 14, menuRect.Y + 180, menuRect.Width - 28, 30);

            if (refreshBtn.Contains(pt))
            {
                _startMenuOpen = false;
                ScanSrcRepo();
            }
            else if (openRepoBtn.Contains(pt))
            {
                _startMenuOpen = false;
                try
                {
                    Directory.CreateDirectory(_srcRepoPath);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _srcRepoPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _statusMessage = "Nie udało się otworzyć src_repo: " + ex.Message;
                }
                Invalidate();
            }
            else if (runSelectedBtn.Contains(pt))
            {
                _startMenuOpen = false;
                if (_selectedIcon != null)
                    LaunchIconScript(_selectedIcon);
                else
                    _statusMessage = "Najpierw kliknij ikonę pliku *.cs na pulpicie.";
                Invalidate();
            }
            else if (exitBtn.Contains(pt))
            {
                Close();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && _draggingIcon != null && e.Button == MouseButtons.Left)
            {
                float newX = e.X - _dragOffset.X;
                float newY = e.Y + _scrollY - _dragOffset.Y;

                newX = Math.Clamp(newX, 6f, Math.Max(6f, ClientSize.Width - _draggingIcon.Width - 6f));
                newY = Math.Clamp(newY, 6f, 6000f);

                _draggingIcon.X = newX;
                _draggingIcon.Y = newY;
                _savedPositions[_draggingIcon.FilePath] = new PointF(newX, newY);

                RecalculateScrollBounds();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
                _draggingIcon = null;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;

            int workBottom = ClientSize.Height - TaskbarHeight;
            if (e.Y >= workBottom) return;

            for (int i = _icons.Count - 1; i >= 0; i--)
            {
                if (_icons[i].GetScreenBounds(_scrollY).Contains(e.Location))
                {
                    _selectedIcon = _icons[i];
                    LaunchIconScript(_icons[i]);
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int workH = Math.Max(1, ClientSize.Height - TaskbarHeight);

            // Tapeta Windows 1:1 (klasyczny błękitno-lazuroowy gradient i delikatny znak wodny)
            using (var bgBrush = new LinearGradientBrush(
                new Rectangle(0, 0, ClientSize.Width, workH),
                Color.FromArgb(14, 88, 168),
                Color.FromArgb(30, 148, 214),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(bgBrush, 0, 0, ClientSize.Width, workH);
            }

            // Delikatny motyw fali na pulpicie
            using (var wavePen = new Pen(Color.FromArgb(45, 255, 255, 255), 3f))
            {
                g.DrawBezier(
                    wavePen,
                    -40, workH * 0.72f,
                    ClientSize.Width * 0.32f, workH * 0.35f,
                    ClientSize.Width * 0.68f, workH * 0.92f,
                    ClientSize.Width + 40, workH * 0.45f);
            }

            // Obszar roboczy ikon z przycięciem powyżej paska zadań
            var prevClip = g.ClipBounds;
            g.SetClip(new Rectangle(0, 0, ClientSize.Width, workH));

            using var labelFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var shadowBrush = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.White);

            foreach (var icon in _icons)
            {
                RectangleF rect = icon.GetScreenBounds(_scrollY);
                if (rect.Bottom < -20 || rect.Top > workH + 20) continue;

                bool selected = _selectedIcon == icon;
                if (selected)
                {
                    using var selBg = new SolidBrush(Color.FromArgb(95, 130, 210, 255));
                    using var selBorder = new Pen(Color.FromArgb(220, 220, 245, 255), 1.2f);
                    g.FillRectangle(selBg, rect);
                    g.DrawRectangle(selBorder, rect.X, rect.Y, rect.Width, rect.Height);
                }

                // Ikona pliku C# w stylu kafelka okna
                float boxSize = 40f;
                float boxX = rect.X + (rect.Width - boxSize) * 0.5f;
                float boxY = rect.Y + 6f;

                using var tileBrush = new SolidBrush(Color.FromArgb(235, 32, 42, 62));
                using var accentPen = new Pen(Color.FromArgb(90, 210, 255), 2f);
                g.FillRectangle(tileBrush, boxX, boxY, boxSize, boxSize);
                g.DrawRectangle(accentPen, boxX, boxY, boxSize, boxSize);

                using var csFont = new Font("Consolas", 11f, FontStyle.Bold);
                using var csBrush = new SolidBrush(Color.FromArgb(110, 225, 255));
                g.DrawString("C#", csFont, csBrush, boxX + 7f, boxY + 10f);

                // Podpis pod ikoną
                RectangleF labelRect = new RectangleF(rect.X + 2f, boxY + boxSize + 4f, rect.Width - 4f, rect.Height - boxSize - 10f);
                var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                RectangleF shadowRect = new RectangleF(labelRect.X + 1f, labelRect.Y + 1f, labelRect.Width, labelRect.Height);
                g.DrawString(icon.DisplayName, labelFont, shadowBrush, shadowRect, fmt);
                g.DrawString(icon.DisplayName, labelFont, textBrush, labelRect, fmt);
            }

            // Pasek przewijania po prawej, jeśli ikon jest dużo
            if (_maxScrollY > 1f)
            {
                int trackW = 8;
                int trackX = ClientSize.Width - trackW - 4;
                int trackY = 6;
                int trackH = workH - 12;

                using var trackBrush = new SolidBrush(Color.FromArgb(75, 0, 0, 0));
                g.FillRectangle(trackBrush, trackX, trackY, trackW, trackH);

                float ratio = workH / (workH + _maxScrollY);
                float thumbH = Math.Max(28f, trackH * ratio);
                float scrollRatio = _maxScrollY > 0f ? (_scrollY / _maxScrollY) : 0f;
                float thumbY = trackY + scrollRatio * (trackH - thumbH);

                using var thumbBrush = new SolidBrush(Color.FromArgb(190, 240, 248, 255));
                g.FillRectangle(thumbBrush, trackX, thumbY, trackW, thumbH);
            }

            g.SetClip(prevClip);

            // Pasek zadań (Taskbar) na dole
            int tbY = ClientSize.Height - TaskbarHeight;
            using (var tbBrush = new LinearGradientBrush(
                new Rectangle(0, tbY, ClientSize.Width, TaskbarHeight),
                Color.FromArgb(34, 42, 56),
                Color.FromArgb(18, 23, 32),
                LinearGradientMode.Vertical))
            using (var tbTopLine = new Pen(Color.FromArgb(95, 165, 215), 1.5f))
            {
                g.FillRectangle(tbBrush, 0, tbY, ClientSize.Width, TaskbarHeight);
                g.DrawLine(tbTopLine, 0, tbY, ClientSize.Width, tbY);
            }

            // Przycisk Start
            Rectangle startRect = GetStartButtonRect();
            using (var startBrush = new SolidBrush(_startMenuOpen
                ? Color.FromArgb(42, 145, 225)
                : Color.FromArgb(24, 118, 198)))
            using (var startFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var startText = new SolidBrush(Color.White))
            {
                g.FillRectangle(startBrush, startRect);

                // Cztery kwadraty logo Windows
                int logoX = startRect.X + 12;
                int logoY = startRect.Y + 12;
                int sq = 8;
                g.FillRectangle(startText, logoX, logoY, sq, sq);
                g.FillRectangle(startText, logoX + sq + 2, logoY, sq, sq);
                g.FillRectangle(startText, logoX, logoY + sq + 2, sq, sq);
                g.FillRectangle(startText, logoX + sq + 2, logoY + sq + 2, sq, sq);

                g.DrawString("Start", startFont, startText, logoX + sq * 2 + 10, startRect.Y + 12);
            }

            // Zegar systemowy po prawej stronie paska zadań
            string clockText = DateTime.Now.ToString("HH:mm\ndd.MM.yyyy");
            using var clockFont = new Font("Segoe UI", 8f, FontStyle.Regular);
            using var clockBrush = new SolidBrush(Color.FromArgb(235, 240, 248));
            var clockFmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            g.DrawString(clockText, clockFont, clockBrush, new RectangleF(ClientSize.Width - 105f, tbY, 96f, TaskbarHeight), clockFmt);

            // Krótki status na środku paska zadań
            using var statusFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var statusBrush = new SolidBrush(Color.FromArgb(210, 225, 240));
            var statusFmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(_statusMessage, statusFont, statusBrush, new RectangleF(StartButtonWidth + 12f, tbY, Math.Max(40f, ClientSize.Width - StartButtonWidth - 125f), TaskbarHeight), statusFmt);

            // Rozwijane menu Start
            if (_startMenuOpen)
            {
                Rectangle menuRect = GetStartMenuRect();
                using var menuBg = new SolidBrush(Color.FromArgb(245, 22, 27, 38));
                using var menuBorder = new Pen(Color.FromArgb(80, 185, 245), 1.5f);
                using var headerBrush = new SolidBrush(Color.FromArgb(28, 124, 205));
                using var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var itemFont = new Font("Segoe UI", 9f, FontStyle.Regular);
                using var white = new SolidBrush(Color.White);

                g.FillRectangle(menuBg, menuRect);
                g.DrawRectangle(menuBorder, menuRect);

                g.FillRectangle(headerBrush, menuRect.X, menuRect.Y, menuRect.Width, 44);
                g.DrawString("HobbyOS Windows Shell", titleFont, white, menuRect.X + 14, menuRect.Y + 12);

                DrawMenuAction(g, new Rectangle(menuRect.X + 14, menuRect.Y + 56, menuRect.Width - 28, 34), "Odśwież skan src_repo (F5)", itemFont, white);
                DrawMenuAction(g, new Rectangle(menuRect.X + 14, menuRect.Y + 98, menuRect.Width - 28, 34), "Otwórz folder src_repo", itemFont, white);
                DrawMenuAction(g, new Rectangle(menuRect.X + 14, menuRect.Y + 140, menuRect.Width - 28, 34), "Uruchom zaznaczony *.cs", itemFont, white);
                DrawMenuAction(g, new Rectangle(menuRect.X + 14, menuRect.Y + 180, menuRect.Width - 28, 30), "Zamknij powłokę", itemFont, white);
            }
        }

        private static void DrawMenuAction(Graphics g, Rectangle r, string label, Font f, Brush textBrush)
        {
            using var btnBg = new SolidBrush(Color.FromArgb(90, 55, 70, 95));
            using var btnBorder = new Pen(Color.FromArgb(95, 140, 175, 215), 1f);
            g.FillRectangle(btnBg, r);
            g.DrawRectangle(btnBorder, r);

            var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            g.DrawString(label, f, textBrush, new RectangleF(r.X + 10, r.Y, r.Width - 20, r.Height), fmt);
        }

        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new WindowsShellForm());
        }
    }
}
