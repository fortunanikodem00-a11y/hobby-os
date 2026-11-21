using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace HobbyOS.PullPity
{
    public sealed class ShellIconItem
    {
        public string FilePath;
        public string FileName;
        public string DisplayName;
        public string RelativeFolder;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public Color TileColor;
        public string CustomIconPath;

        public ShellIconItem(string filePath, string rootDir, float x, float y, float width, float height, Color tileColor)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            DisplayName = Path.GetFileNameWithoutExtension(filePath);
            RelativeFolder = GetRelativeFolder(filePath, rootDir);
            X = x;
            Y = y;
            Width = width;
            Height = height;
            TileColor = tileColor;
        }

        private static string GetRelativeFolder(string filePath, string rootDir)
        {
            try
            {
                string fullFile = Path.GetFullPath(filePath);
                string fullRoot = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dir = Path.GetDirectoryName(fullFile) ?? fullRoot;
                if (dir.Length <= fullRoot.Length) return ".";
                string rel = dir.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrWhiteSpace(rel) ? "." : rel;
            }
            catch
            {
                return ".";
            }
        }

        public RectangleF GetScreenBounds(float scrollY)
        {
            return new RectangleF(X, Y - scrollY, Width, Height);
        }
    }

    public sealed class VirtualDesktop
    {
        public int Index;
        public string Name;
        public Color ColorA;
        public Color ColorB;
        public string WallpaperPath;
        public float ScrollY;
        public float MaxScrollY;
        public readonly List<ShellIconItem> Icons = new List<ShellIconItem>();

        public VirtualDesktop(int index, string name, Color colorA, Color colorB)
        {
            Index = index;
            Name = name;
            ColorA = colorA;
            ColorB = colorB;
        }
    }

    public sealed class WinOneShellForm : Form
    {
        private const int TaskbarHeight = 46;
        private const int StartButtonWidth = 104;
        private const float IconWidth = 104f;
        private const float IconHeight = 98f;
        private const float ColSpacing = 120f;
        private const float RowSpacing = 114f;
        private const float MarginLeft = 22f;
        private const float MarginTop = 22f;

        private readonly VirtualDesktop[] _desktops = new VirtualDesktop[4];
        private readonly Dictionary<string, PointF> _savedPositions = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> _savedTileColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _savedCustomIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Image> _imageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer _clockTimer = new System.Windows.Forms.Timer();

        private readonly string _hobbyOsRootDir;
        private readonly string _currentSourceFile;

        private int _activeDesktopIndex = 0;
        private ShellIconItem _selectedIcon = null;
        private ShellIconItem _draggingIcon = null;
        private PointF _dragOffset;
        private bool _isDragging = false;

        private bool _startMenuOpen = false;
        private int _wallpaperPresetIndex = 0;
        private int _iconColorPresetIndex = 0;
        private string _statusMessage = "WinOneShell gotowy";

        private static readonly (string Name, Color A, Color B)[] WallpaperPresets =
        {
            ("Windows Azure", Color.FromArgb(14, 88, 168), Color.FromArgb(30, 148, 214)),
            ("Neon Vice", Color.FromArgb(36, 16, 68), Color.FromArgb(215, 55, 145)),
            ("Emerald Matrix", Color.FromArgb(10, 52, 46), Color.FromArgb(24, 138, 108)),
            ("Slate Carbon", Color.FromArgb(22, 26, 34), Color.FromArgb(54, 64, 84))
        };

        private static readonly Color[] IconColorPresets =
        {
            Color.FromArgb(32, 42, 62),
            Color.FromArgb(24, 118, 198),
            Color.FromArgb(185, 48, 125),
            Color.FromArgb(28, 142, 102),
            Color.FromArgb(185, 105, 28)
        };

        private VirtualDesktop ActiveDesktop => _desktops[_activeDesktopIndex];

        public WinOneShellForm(string explicitRootDir)
        {
            Text = "HobbyOS — WinOneShell";
            ClientSize = new Size(1240, 740);
            MinimumSize = new Size(680, 420);
            DoubleBuffered = true;
            KeyPreview = true;

            _hobbyOsRootDir = ResolveHobbyOsRootDirectory(explicitRootDir);
            _currentSourceFile = Path.Combine(_hobbyOsRootDir, "WinOneShell.cs");

            _desktops[0] = new VirtualDesktop(0, "Pulpit 1", WallpaperPresets[0].A, WallpaperPresets[0].B);
            _desktops[1] = new VirtualDesktop(1, "Pulpit 2", WallpaperPresets[1].A, WallpaperPresets[1].B);
            _desktops[2] = new VirtualDesktop(2, "Pulpit 3", WallpaperPresets[2].A, WallpaperPresets[2].B);
            _desktops[3] = new VirtualDesktop(3, "Pulpit 4", WallpaperPresets[3].A, WallpaperPresets[3].B);

            ScanHobbyOsTree();

            _clockTimer.Interval = 1000;
            _clockTimer.Tick += OnClockTick;
            _clockTimer.Start();
        }

        private static string ResolveHobbyOsRootDirectory(string explicitRootDir)
        {
            if (!string.IsNullOrWhiteSpace(explicitRootDir) && Directory.Exists(explicitRootDir))
            {
                return Path.GetFullPath(explicitRootDir);
            }

            string[] candidates =
            {
                Environment.CurrentDirectory,
                AppDomain.CurrentDomain.BaseDirectory
            };

            foreach (string start in candidates)
            {
                if (string.IsNullOrWhiteSpace(start)) continue;
                try
                {
                    string cur = Path.GetFullPath(start);
                    for (int depth = 0; depth < 8 && !string.IsNullOrEmpty(cur); depth++)
                    {
                        string upper = cur.ToUpperInvariant();
                        bool isTempHobbyOsApp = upper.Contains(Path.DirectorySeparatorChar + "TEMP" + Path.DirectorySeparatorChar + "HOBBYOSAPPS");

                        if (!isTempHobbyOsApp)
                        {
                            // Jeśli w tym katalogu jest src_repo lub wiele plików .cs, to najpewniej główny katalog HobbyOS
                            if (Directory.Exists(Path.Combine(cur, "src_repo")) || depth == 0)
                            {
                                return cur;
                            }
                        }

                        var parent = Directory.GetParent(cur);
                        if (parent == null) break;
                        cur = parent.FullName;
                    }
                }
                catch
                {
                    // Przejdź do kolejnego kandydata
                }
            }

            return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        }

        private void OnClockTick(object sender, EventArgs e)
        {
            Invalidate();
        }

        private string MakeDesktopKey(int desktopIdx, string filePath)
        {
            return desktopIdx + "::" + Path.GetFullPath(filePath);
        }

        private static bool IsIgnoredPath(string fullPath, string currentSourcePath)
        {
            string norm = Path.GetFullPath(fullPath);
            if (string.Equals(norm, Path.GetFullPath(currentSourcePath), StringComparison.OrdinalIgnoreCase))
                return true;

            string fileName = Path.GetFileName(norm);
            if (string.Equals(fileName, "Program.cs", StringComparison.OrdinalIgnoreCase))
                return true;

            string upper = norm.ToUpperInvariant();
            if (upper.Contains(Path.DirectorySeparatorChar + "BIN" + Path.DirectorySeparatorChar) ||
                upper.Contains(Path.DirectorySeparatorChar + "OBJ" + Path.DirectorySeparatorChar) ||
                upper.Contains(Path.DirectorySeparatorChar + "HOBBYOSAPPS" + Path.DirectorySeparatorChar))
            {
                return true;
            }

            return false;
        }

        private void ScanHobbyOsTree()
        {
            for (int d = 0; d < _desktops.Length; d++)
            {
                foreach (var old in _desktops[d].Icons)
                {
                    string key = MakeDesktopKey(d, old.FilePath);
                    _savedPositions[key] = new PointF(old.X, old.Y);
                    _savedTileColors[key] = old.TileColor;
                    if (!string.IsNullOrWhiteSpace(old.CustomIconPath))
                        _savedCustomIcons[key] = old.CustomIconPath;
                }
                _desktops[d].Icons.Clear();
            }

            var foundFiles = new List<string>();
            try
            {
                if (Directory.Exists(_hobbyOsRootDir))
                {
                    string[] allCs = Directory.GetFiles(_hobbyOsRootDir, "*.cs", SearchOption.AllDirectories);
                    Array.Sort(allCs, StringComparer.OrdinalIgnoreCase);

                    foreach (string f in allCs)
                    {
                        if (!IsIgnoredPath(f, _currentSourceFile))
                        {
                            foundFiles.Add(f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _statusMessage = "Błąd skanowania HobbyOS: " + ex.Message;
                RecalculateAllScrollBounds();
                Invalidate();
                return;
            }

            int cols = Math.Max(1, (ClientSize.Width - 60) / (int)ColSpacing);

            for (int d = 0; d < _desktops.Length; d++)
            {
                for (int i = 0; i < foundFiles.Count; i++)
                {
                    string filePath = foundFiles[i];
                    string key = MakeDesktopKey(d, filePath);

                    float x = MarginLeft + (i % cols) * ColSpacing;
                    float y = MarginTop + (i / cols) * RowSpacing;
                    if (_savedPositions.TryGetValue(key, out PointF savedPos))
                    {
                        x = savedPos.X;
                        y = savedPos.Y;
                    }

                    Color tileColor = IconColorPresets[i % IconColorPresets.Length];
                    if (_savedTileColors.TryGetValue(key, out Color savedCol))
                    {
                        tileColor = savedCol;
                    }

                    var item = new ShellIconItem(filePath, _hobbyOsRootDir, x, y, IconWidth, IconHeight, tileColor);
                    if (_savedCustomIcons.TryGetValue(key, out string customIcon))
                    {
                        item.CustomIconPath = customIcon;
                    }

                    _desktops[d].Icons.Add(item);
                }
            }

            _selectedIcon = null;
            _statusMessage = $"HobbyOS [{_hobbyOsRootDir}]: {foundFiles.Count} plik(ów) *.cs";
            RecalculateAllScrollBounds();
            Invalidate();
        }

        private void RecalculateAllScrollBounds()
        {
            int workHeight = Math.Max(100, ClientSize.Height - TaskbarHeight);
            for (int d = 0; d < _desktops.Length; d++)
            {
                var desk = _desktops[d];
                float bottomMost = MarginTop + IconHeight + 40f;
                foreach (var icon in desk.Icons)
                {
                    float b = icon.Y + icon.Height + 36f;
                    if (b > bottomMost) bottomMost = b;
                }
                desk.MaxScrollY = Math.Max(0f, bottomMost - workHeight);
                desk.ScrollY = Math.Clamp(desk.ScrollY, 0f, desk.MaxScrollY);
            }
        }

        private void ScrollActiveDesktopBy(float deltaY)
        {
            RecalculateAllScrollBounds();
            var desk = ActiveDesktop;
            desk.ScrollY = Math.Clamp(desk.ScrollY + deltaY, 0f, desk.MaxScrollY);
            Invalidate();
        }

        private void CycleWallpaperPreset()
        {
            _wallpaperPresetIndex = (_wallpaperPresetIndex + 1) % WallpaperPresets.Length;
            var preset = WallpaperPresets[_wallpaperPresetIndex];
            ActiveDesktop.WallpaperPath = null;
            ActiveDesktop.ColorA = preset.A;
            ActiveDesktop.ColorB = preset.B;
            _statusMessage = $"{ActiveDesktop.Name}: tapeta {preset.Name}";
            Invalidate();
        }

        private void PickCustomWallpaperImage()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Wybierz tapetę pulpitu",
                Filter = "Obrazy (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Wszystkie pliki (*.*)|*.*"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                if (GetOrLoadImage(dlg.FileName) != null)
                {
                    ActiveDesktop.WallpaperPath = dlg.FileName;
                    _statusMessage = $"{ActiveDesktop.Name}: tapeta {Path.GetFileName(dlg.FileName)}";
                }
                else
                {
                    _statusMessage = "Nie udało się wczytać obrazu tapety.";
                }
                Invalidate();
            }
        }

        private void CycleSelectedIconColor()
        {
            if (_selectedIcon == null)
            {
                _statusMessage = "Najpierw zaznacz ikonę pliku *.cs na pulpicie.";
                Invalidate();
                return;
            }

            _iconColorPresetIndex = (_iconColorPresetIndex + 1) % IconColorPresets.Length;
            Color nextCol = IconColorPresets[_iconColorPresetIndex];
            _selectedIcon.TileColor = nextCol;
            _selectedIcon.CustomIconPath = null;
            _savedTileColors[MakeDesktopKey(_activeDesktopIndex, _selectedIcon.FilePath)] = nextCol;
            _savedCustomIcons.Remove(MakeDesktopKey(_activeDesktopIndex, _selectedIcon.FilePath));
            _statusMessage = $"Zmieniono kolor ikony: {_selectedIcon.DisplayName}";
            Invalidate();
        }

        private void PickCustomIconImage()
        {
            if (_selectedIcon == null)
            {
                _statusMessage = "Najpierw zaznacz ikonę pliku *.cs, aby zmienić jej obrazek.";
                Invalidate();
                return;
            }

            using var dlg = new OpenFileDialog
            {
                Title = "Wybierz obrazek ikony",
                Filter = "Ikony i obrazy (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|Wszystkie pliki (*.*)|*.*"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                if (GetOrLoadImage(dlg.FileName) != null)
                {
                    _selectedIcon.CustomIconPath = dlg.FileName;
                    _savedCustomIcons[MakeDesktopKey(_activeDesktopIndex, _selectedIcon.FilePath)] = dlg.FileName;
                    _statusMessage = $"Ustawiono ikonę dla {_selectedIcon.DisplayName}";
                }
                else
                {
                    _statusMessage = "Nie udało się wczytać pliku ikony.";
                }
                Invalidate();
            }
        }

        private Image GetOrLoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            if (_imageCache.TryGetValue(path, out Image cached)) return cached;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                using var tempImg = Image.FromStream(ms);
                var copy = new Bitmap(tempImg);
                _imageCache[path] = copy;
                return copy;
            }
            catch
            {
                return null;
            }
        }

        private void LaunchIconScript(ShellIconItem icon)
        {
            if (icon == null || !File.Exists(icon.FilePath))
            {
                _statusMessage = "Brak pliku źródłowego na dysku.";
                Invalidate();
                return;
            }

            try
            {
                string safeName = SanitizeFolderName(Path.GetFileNameWithoutExtension(icon.FilePath));
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "HobbyApp";

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

                // Przekazujemy _hobbyOsRootDir, żeby każda kolejna apka potomna i potomna potomnych znała główny katalog HobbyOS
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{csprojPath}\" -- \"{_hobbyOsRootDir}\"",
                    WorkingDirectory = _hobbyOsRootDir,
                    UseShellExecute = true
                };

                Process.Start(psi);
                _statusMessage = $"Odpalono: {icon.FileName} (root: {_hobbyOsRootDir})";
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

        private Rectangle GetDesktopSwitcherRect(int idx)
        {
            int btnW = 42;
            int gap = 4;
            int startX = StartButtonWidth + 10 + idx * (btnW + gap);
            return new Rectangle(startX, ClientSize.Height - TaskbarHeight + 8, btnW, TaskbarHeight - 16);
        }

        private Rectangle GetStartMenuRect()
        {
            int menuW = 330;
            int menuH = 264;
            return new Rectangle(6, ClientSize.Height - TaskbarHeight - menuH - 6, menuW, menuH);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateAllScrollBounds();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollActiveDesktopBy(-e.Delta * 0.45f);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Up)
            {
                ScrollActiveDesktopBy(-48f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                ScrollActiveDesktopBy(48f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                ScrollActiveDesktopBy(-240f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                ScrollActiveDesktopBy(240f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                ScrollActiveDesktopBy(-ActiveDesktop.ScrollY);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                ScrollActiveDesktopBy(ActiveDesktop.MaxScrollY);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ScanHobbyOsTree();
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

            for (int d = 0; d < _desktops.Length; d++)
            {
                if (GetDesktopSwitcherRect(d).Contains(e.Location))
                {
                    _activeDesktopIndex = d;
                    _selectedIcon = null;
                    _startMenuOpen = false;
                    _statusMessage = $"Przełączono na {_desktops[d].Name}";
                    Invalidate();
                    return;
                }
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
                var icons = ActiveDesktop.Icons;
                float scrollY = ActiveDesktop.ScrollY;

                ShellIconItem hit = null;
                for (int i = icons.Count - 1; i >= 0; i--)
                {
                    if (icons[i].GetScreenBounds(scrollY).Contains(e.Location))
                    {
                        hit = icons[i];
                        break;
                    }
                }

                _selectedIcon = hit;
                if (hit != null)
                {
                    icons.Remove(hit);
                    icons.Add(hit);

                    _draggingIcon = hit;
                    _isDragging = true;
                    _dragOffset = new PointF(e.X - hit.X, e.Y - (hit.Y - scrollY));
                }
            }

            Invalidate();
        }

        private void HandleStartMenuClick(Point pt, Rectangle menuRect)
        {
            int x = menuRect.X + 14;
            int w = menuRect.Width - 28;
            int y0 = menuRect.Y + 52;
            int step = 34;

            if (new Rectangle(x, y0 + step * 0, w, 28).Contains(pt))
            {
                _startMenuOpen = false;
                ScanHobbyOsTree();
            }
            else if (new Rectangle(x, y0 + step * 1, w, 28).Contains(pt))
            {
                _startMenuOpen = false;
                CycleWallpaperPreset();
            }
            else if (new Rectangle(x, y0 + step * 2, w, 28).Contains(pt))
            {
                _startMenuOpen = false;
                PickCustomWallpaperImage();
            }
            else if (new Rectangle(x, y0 + step * 3, w, 28).Contains(pt))
            {
                _startMenuOpen = false;
                CycleSelectedIconColor();
            }
            else if (new Rectangle(x, y0 + step * 4, w, 28).Contains(pt))
            {
                _startMenuOpen = false;
                PickCustomIconImage();
            }
            else if (new Rectangle(x, y0 + step * 5, w, 28).Contains(pt))
            {
                Close();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && _draggingIcon != null && e.Button == MouseButtons.Left)
            {
                float scrollY = ActiveDesktop.ScrollY;
                float newX = e.X - _dragOffset.X;
                float newY = e.Y + scrollY - _dragOffset.Y;

                newX = Math.Clamp(newX, 6f, Math.Max(6f, ClientSize.Width - _draggingIcon.Width - 6f));
                newY = Math.Clamp(newY, 6f, 6000f);

                _draggingIcon.X = newX;
                _draggingIcon.Y = newY;
                _savedPositions[MakeDesktopKey(_activeDesktopIndex, _draggingIcon.FilePath)] = new PointF(newX, newY);

                RecalculateAllScrollBounds();
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

            var icons = ActiveDesktop.Icons;
            float scrollY = ActiveDesktop.ScrollY;

            for (int i = icons.Count - 1; i >= 0; i--)
            {
                if (icons[i].GetScreenBounds(scrollY).Contains(e.Location))
                {
                    _selectedIcon = icons[i];
                    LaunchIconScript(icons[i]);
                    return;
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _clockTimer.Stop();
            _clockTimer.Dispose();
            foreach (var img in _imageCache.Values)
            {
                img.Dispose();
            }
            _imageCache.Clear();
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int workH = Math.Max(1, ClientSize.Height - TaskbarHeight);
            var desk = ActiveDesktop;

            Image wallImg = GetOrLoadImage(desk.WallpaperPath);
            if (wallImg != null)
            {
                g.DrawImage(wallImg, 0, 0, ClientSize.Width, workH);
            }
            else
            {
                using var bgBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, ClientSize.Width, workH),
                    desk.ColorA,
                    desk.ColorB,
                    LinearGradientMode.ForwardDiagonal);
                g.FillRectangle(bgBrush, 0, 0, ClientSize.Width, workH);

                using var wavePen = new Pen(Color.FromArgb(40, 255, 255, 255), 3f);
                g.DrawBezier(
                    wavePen,
                    -40, workH * 0.72f,
                    ClientSize.Width * 0.32f, workH * 0.35f,
                    ClientSize.Width * 0.68f, workH * 0.92f,
                    ClientSize.Width + 40, workH * 0.45f);
            }

            var prevClip = g.ClipBounds;
            g.SetClip(new Rectangle(0, 0, ClientSize.Width, workH));

            using var labelFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var subFont = new Font("Consolas", 7.2f, FontStyle.Regular);
            using var shadowBrush = new SolidBrush(Color.FromArgb(195, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.White);
            using var subBrush = new SolidBrush(Color.FromArgb(220, 210, 235, 255));

            foreach (var icon in desk.Icons)
            {
                RectangleF rect = icon.GetScreenBounds(desk.ScrollY);
                if (rect.Bottom < -20 || rect.Top > workH + 20) continue;

                bool selected = _selectedIcon == icon;
                if (selected)
                {
                    using var selBg = new SolidBrush(Color.FromArgb(95, 130, 210, 255));
                    using var selBorder = new Pen(Color.FromArgb(225, 225, 245, 255), 1.3f);
                    g.FillRectangle(selBg, rect);
                    g.DrawRectangle(selBorder, rect.X, rect.Y, rect.Width, rect.Height);
                }

                float boxSize = 42f;
                float boxX = rect.X + (rect.Width - boxSize) * 0.5f;
                float boxY = rect.Y + 5f;

                Image customIconImg = GetOrLoadImage(icon.CustomIconPath);
                if (customIconImg != null)
                {
                    g.DrawImage(customIconImg, boxX, boxY, boxSize, boxSize);
                }
                else
                {
                    using var tileBrush = new SolidBrush(icon.TileColor);
                    using var accentPen = new Pen(Color.FromArgb(140, 225, 255), 2f);
                    g.FillRectangle(tileBrush, boxX, boxY, boxSize, boxSize);
                    g.DrawRectangle(accentPen, boxX, boxY, boxSize, boxSize);

                    using var csFont = new Font("Consolas", 11f, FontStyle.Bold);
                    using var csBrush = new SolidBrush(Color.White);
                    g.DrawString("C#", csFont, csBrush, boxX + 8f, boxY + 11f);
                }

                var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                RectangleF labelRect = new RectangleF(rect.X + 2f, boxY + boxSize + 3f, rect.Width - 4f, 18f);
                g.DrawString(icon.DisplayName, labelFont, shadowBrush, new RectangleF(labelRect.X + 1f, labelRect.Y + 1f, labelRect.Width, labelRect.Height), fmt);
                g.DrawString(icon.DisplayName, labelFont, textBrush, labelRect, fmt);

                if (icon.RelativeFolder != ".")
                {
                    RectangleF subRect = new RectangleF(rect.X + 2f, boxY + boxSize + 20f, rect.Width - 4f, 15f);
                    g.DrawString(icon.RelativeFolder, subFont, shadowBrush, new RectangleF(subRect.X + 1f, subRect.Y + 1f, subRect.Width, subRect.Height), fmt);
                    g.DrawString(icon.RelativeFolder, subFont, subBrush, subRect, fmt);
                }
            }

            if (desk.MaxScrollY > 1f)
            {
                int trackW = 8;
                int trackX = ClientSize.Width - trackW - 4;
                int trackY = 6;
                int trackH = workH - 12;

                using var trackBrush = new SolidBrush(Color.FromArgb(75, 0, 0, 0));
                g.FillRectangle(trackBrush, trackX, trackY, trackW, trackH);

                float ratio = workH / (workH + desk.MaxScrollY);
                float thumbH = Math.Max(28f, trackH * ratio);
                float scrollRatio = desk.MaxScrollY > 0f ? (desk.ScrollY / desk.MaxScrollY) : 0f;
                float thumbY = trackY + scrollRatio * (trackH - thumbH);

                using var thumbBrush = new SolidBrush(Color.FromArgb(195, 240, 248, 255));
                g.FillRectangle(thumbBrush, trackX, thumbY, trackW, thumbH);
            }

            g.SetClip(prevClip);

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

            Rectangle startRect = GetStartButtonRect();
            using (var startBrush = new SolidBrush(_startMenuOpen ? Color.FromArgb(42, 145, 225) : Color.FromArgb(24, 118, 198)))
            using (var startFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var startText = new SolidBrush(Color.White))
            {
                g.FillRectangle(startBrush, startRect);
                int logoX = startRect.X + 10;
                int logoY = startRect.Y + 13;
                int sq = 8;
                g.FillRectangle(startText, logoX, logoY, sq, sq);
                g.FillRectangle(startText, logoX + sq + 2, logoY, sq, sq);
                g.FillRectangle(startText, logoX, logoY + sq + 2, sq, sq);
                g.FillRectangle(startText, logoX + sq + 2, logoY + sq + 2, sq, sq);
                g.DrawString("Start", startFont, startText, logoX + sq * 2 + 9, startRect.Y + 13);
            }

            using var dFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            for (int d = 0; d < _desktops.Length; d++)
            {
                Rectangle dr = GetDesktopSwitcherRect(d);
                bool active = d == _activeDesktopIndex;
                using var dBg = new SolidBrush(active ? Color.FromArgb(42, 145, 225) : Color.FromArgb(85, 58, 72, 92));
                using var dBorder = new Pen(active ? Color.White : Color.FromArgb(110, 150, 180, 210), 1f);
                using var dText = new SolidBrush(Color.White);

                g.FillRectangle(dBg, dr);
                g.DrawRectangle(dBorder, dr);
                var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("D" + (d + 1), dFont, dText, dr, fmt);
            }

            string clockText = DateTime.Now.ToString("HH:mm\ndd.MM.yyyy");
            using var clockFont = new Font("Segoe UI", 8f, FontStyle.Regular);
            using var clockBrush = new SolidBrush(Color.FromArgb(235, 240, 248));
            var clockFmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            g.DrawString(clockText, clockFont, clockBrush, new RectangleF(ClientSize.Width - 105f, tbY, 96f, TaskbarHeight), clockFmt);

            int switcherEndX = StartButtonWidth + 10 + _desktops.Length * 46 + 10;
            using var statusFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var statusBrush = new SolidBrush(Color.FromArgb(215, 228, 242));
            var statusFmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(_statusMessage, statusFont, statusBrush, new RectangleF(switcherEndX, tbY, Math.Max(40f, ClientSize.Width - switcherEndX - 115f), TaskbarHeight), statusFmt);

            if (_startMenuOpen)
            {
                Rectangle menuRect = GetStartMenuRect();
                using var menuBg = new SolidBrush(Color.FromArgb(248, 22, 27, 38));
                using var menuBorder = new Pen(Color.FromArgb(80, 185, 245), 1.5f);
                using var headerBrush = new SolidBrush(Color.FromArgb(28, 124, 205));
                using var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var itemFont = new Font("Segoe UI", 8.8f, FontStyle.Regular);
                using var white = new SolidBrush(Color.White);

                g.FillRectangle(menuBg, menuRect);
                g.DrawRectangle(menuBorder, menuRect);

                g.FillRectangle(headerBrush, menuRect.X, menuRect.Y, menuRect.Width, 42);
                g.DrawString("WinOneShell — " + desk.Name, titleFont, white, menuRect.X + 14, menuRect.Y + 11);

                int x = menuRect.X + 14;
                int w = menuRect.Width - 28;
                int y0 = menuRect.Y + 52;
                int step = 34;

                DrawStartRow(g, new Rectangle(x, y0 + step * 0, w, 28), "Skanuj drzewo HobbyOS (F5)", itemFont, white);
                DrawStartRow(g, new Rectangle(x, y0 + step * 1, w, 28), "Zmień motyw tapety pulpitu", itemFont, white);
                DrawStartRow(g, new Rectangle(x, y0 + step * 2, w, 28), "Wybierz własny obraz tapety...", itemFont, white);
                DrawStartRow(g, new Rectangle(x, y0 + step * 3, w, 28), "Zmień kolor zaznaczonej ikony", itemFont, white);
                DrawStartRow(g, new Rectangle(x, y0 + step * 4, w, 28), "Ustaw obrazek zaznaczonej ikony...", itemFont, white);
                DrawStartRow(g, new Rectangle(x, y0 + step * 5, w, 28), "Zamknij WinOneShell", itemFont, white);
            }
        }

        private static void DrawStartRow(Graphics g, Rectangle r, string label, Font f, Brush textBrush)
        {
            using var btnBg = new SolidBrush(Color.FromArgb(95, 55, 70, 95));
            using var btnBorder = new Pen(Color.FromArgb(95, 140, 175, 215), 1f);
            g.FillRectangle(btnBg, r);
            g.DrawRectangle(btnBorder, r);

            var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            g.DrawString(label, f, textBrush, new RectangleF(r.X + 10, r.Y, r.Width - 20, r.Height), fmt);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            string rootArg = (args != null && args.Length > 0) ? args[0] : null;
            Application.Run(new WinOneShellForm(rootArg));
        }
    }
}
