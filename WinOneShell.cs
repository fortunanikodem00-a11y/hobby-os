using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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
        public int DesktopIndex;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public Color TileColor;
        public string CustomIconPath;

        public ShellIconItem(string filePath, string rootDir, int desktopIndex, float x, float y, float width, float height, Color tileColor)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            DisplayName = Path.GetFileNameWithoutExtension(filePath);
            RelativeFolder = GetRelativeFolder(filePath, rootDir);
            DesktopIndex = desktopIndex;
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
        private const int DesktopCount = 5;
        private const int MaxIconsPerDesktopAutoSplit = 12;
        private const int TaskbarHeight = 46;
        private const int StartButtonWidth = 104;
        private const float IconWidth = 106f;
        private const float IconHeight = 98f;
        private const float ColSpacing = 122f;
        private const float RowSpacing = 114f;
        private const float MarginLeft = 22f;
        private const float MarginTop = 22f;

        private readonly VirtualDesktop[] _desktops = new VirtualDesktop[DesktopCount];
        private readonly List<ShellIconItem> _allIcons = new List<ShellIconItem>();

        private readonly Dictionary<string, int> _savedDesktops = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PointF> _savedPositions = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> _savedTileColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _savedCustomIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Image> _imageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer _clockTimer = new System.Windows.Forms.Timer();

        private readonly string _hobbyOsRootDir;
        private readonly string _currentSourceFile;
        private readonly string _stateFilePath;
        private readonly bool _initialized;

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
            ("Slate Carbon", Color.FromArgb(22, 26, 34), Color.FromArgb(54, 64, 84)),
            ("Sunset Leonida", Color.FromArgb(72, 22, 54), Color.FromArgb(235, 118, 48))
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
            for (int i = 0; i < DesktopCount; i++)
            {
                var preset = WallpaperPresets[i % WallpaperPresets.Length];
                _desktops[i] = new VirtualDesktop(i, "Pulpit " + (i + 1), preset.A, preset.B);
            }

            _hobbyOsRootDir = ResolveHobbyOsRootDirectory(explicitRootDir);
            _currentSourceFile = Path.Combine(_hobbyOsRootDir, "WinOneShell.cs");
            _stateFilePath = Path.Combine(_hobbyOsRootDir, "winoneshell_state.cfg");

            Text = "HobbyOS — WinOneShell (D1-D5)";
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            DoubleBuffered = true;
            KeyPreview = true;

            _initialized = true;
            MinimumSize = new Size(680, 420);
            ClientSize = new Size(1240, 740);

            LoadPersistentState();
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
                }
            }

            return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        }

        private void OnClockTick(object sender, EventArgs e)
        {
            Invalidate();
        }

        private string NormalizeKey(string filePath)
        {
            return Path.GetFullPath(filePath);
        }

        private void LoadPersistentState()
        {
            _savedDesktops.Clear();
            _savedPositions.Clear();
            _savedTileColors.Clear();
            _savedCustomIcons.Clear();

            if (!File.Exists(_stateFilePath)) return;

            try
            {
                string[] lines = File.ReadAllLines(_stateFilePath);
                foreach (string rawLine in lines)
                {
                    if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("#")) continue;

                    if (rawLine.StartsWith("DESKTOP_WALLPAPER|", StringComparison.Ordinal))
                    {
                        string[] parts = rawLine.Split('|');
                        if (parts.Length >= 3 && int.TryParse(parts[1], out int dIdx) && dIdx >= 0 && dIdx < DesktopCount)
                        {
                            string wp = parts[2];
                            if (!string.IsNullOrWhiteSpace(wp) && File.Exists(wp))
                            {
                                _desktops[dIdx].WallpaperPath = wp;
                            }
                        }
                        continue;
                    }

                    if (rawLine.StartsWith("DESKTOP_COLORS|", StringComparison.Ordinal))
                    {
                        string[] parts = rawLine.Split('|');
                        if (parts.Length >= 4 && int.TryParse(parts[1], out int dIdx) && dIdx >= 0 && dIdx < DesktopCount)
                        {
                            if (int.TryParse(parts[2], out int argbA) && int.TryParse(parts[3], out int argbB))
                            {
                                _desktops[dIdx].ColorA = Color.FromArgb(argbA);
                                _desktops[dIdx].ColorB = Color.FromArgb(argbB);
                            }
                        }
                        continue;
                    }

                    if (rawLine.StartsWith("ICON|", StringComparison.Ordinal))
                    {
                        string[] parts = rawLine.Split('|');
                        if (parts.Length >= 6)
                        {
                            string path = NormalizeKey(parts[1]);
                            if (!int.TryParse(parts[2], out int dIdx)) dIdx = 0;
                            dIdx = Math.Clamp(dIdx, 0, DesktopCount - 1);

                            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) x = MarginLeft;
                            if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) y = MarginTop;
                            if (!int.TryParse(parts[5], out int argb)) argb = IconColorPresets[0].ToArgb();

                            string customIcon = parts.Length >= 7 ? parts[6] : "";

                            _savedDesktops[path] = dIdx;
                            _savedPositions[path] = new PointF(x, y);
                            _savedTileColors[path] = Color.FromArgb(argb);
                            if (!string.IsNullOrWhiteSpace(customIcon))
                            {
                                _savedCustomIcons[path] = customIcon;
                            }
                            else
                            {
                                _savedCustomIcons.Remove(path);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void SavePersistentState()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# WinOneShell state");

                for (int d = 0; d < DesktopCount; d++)
                {
                    var desk = _desktops[d];
                    lines.Add($"DESKTOP_COLORS|{d}|{desk.ColorA.ToArgb()}|{desk.ColorB.ToArgb()}");
                    if (!string.IsNullOrWhiteSpace(desk.WallpaperPath))
                    {
                        lines.Add($"DESKTOP_WALLPAPER|{d}|{desk.WallpaperPath}");
                    }
                }

                foreach (var icon in _allIcons)
                {
                    string key = NormalizeKey(icon.FilePath);
                    _savedDesktops[key] = icon.DesktopIndex;
                    _savedPositions[key] = new PointF(icon.X, icon.Y);
                    _savedTileColors[key] = icon.TileColor;
                    if (!string.IsNullOrWhiteSpace(icon.CustomIconPath))
                        _savedCustomIcons[key] = icon.CustomIconPath;
                    else
                        _savedCustomIcons.Remove(key);
                }

                foreach (var kv in _savedDesktops)
                {
                    string path = kv.Key;
                    int dIdx = Math.Clamp(kv.Value, 0, DesktopCount - 1);
                    PointF pos = _savedPositions.TryGetValue(path, out PointF p) ? p : new PointF(MarginLeft, MarginTop);
                    Color col = _savedTileColors.TryGetValue(path, out Color c) ? c : IconColorPresets[0];
                    string iconImg = _savedCustomIcons.TryGetValue(path, out string ic) ? ic : "";

                    lines.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "ICON|{0}|{1}|{2}|{3}|{4}|{5}",
                        path,
                        dIdx,
                        pos.X,
                        pos.Y,
                        col.ToArgb(),
                        iconImg));
                }

                string dir = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllLines(_stateFilePath, lines);
            }
            catch
            {
            }
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
            // Zaktualizuj pamięć z aktualnych ikon
            foreach (var old in _allIcons)
            {
                string key = NormalizeKey(old.FilePath);
                _savedDesktops[key] = old.DesktopIndex;
                _savedPositions[key] = new PointF(old.X, old.Y);
                _savedTileColors[key] = old.TileColor;
                if (!string.IsNullOrWhiteSpace(old.CustomIconPath))
                    _savedCustomIcons[key] = old.CustomIconPath;
            }

            _allIcons.Clear();

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

            int[] countPerDesktop = new int[DesktopCount];
            int cols = Math.Max(1, (ClientSize.Width - 60) / (int)ColSpacing);

            for (int i = 0; i < foundFiles.Count; i++)
            {
                string filePath = foundFiles[i];
                string key = NormalizeKey(filePath);

                int targetDesktop;
                if (_savedDesktops.TryGetValue(key, out int savedD))
                {
                    targetDesktop = Math.Clamp(savedD, 0, DesktopCount - 1);
                }
                else
                {
                    // Automatyczne rozdzielanie na kolejne pulpity D1..D5, gdy aplikacji jest dużo
                    targetDesktop = Math.Min(DesktopCount - 1, i / MaxIconsPerDesktopAutoSplit);
                }

                int slotIndex = countPerDesktop[targetDesktop]++;
                float x = MarginLeft + (slotIndex % cols) * ColSpacing;
                float y = MarginTop + (slotIndex / cols) * RowSpacing;

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

                var item = new ShellIconItem(filePath, _hobbyOsRootDir, targetDesktop, x, y, IconWidth, IconHeight, tileColor);
                if (_savedCustomIcons.TryGetValue(key, out string customIcon))
                {
                    item.CustomIconPath = customIcon;
                }

                _allIcons.Add(item);
            }

            _selectedIcon = null;
            _statusMessage = $"HobbyOS [{_hobbyOsRootDir}]: {foundFiles.Count} plik(ów) *.cs na pulpitach D1-D5";
            RecalculateAllScrollBounds();
            SavePersistentState();
            Invalidate();
        }

        private List<ShellIconItem> GetIconsOnDesktop(int desktopIdx)
        {
            var list = new List<ShellIconItem>();
            foreach (var icon in _allIcons)
            {
                if (icon.DesktopIndex == desktopIdx)
                {
                    list.Add(icon);
                }
            }
            return list;
        }

        private void MoveIconToDesktop(ShellIconItem icon, int targetDesktopIdx)
        {
            if (icon == null) return;
            targetDesktopIdx = Math.Clamp(targetDesktopIdx, 0, DesktopCount - 1);
            if (icon.DesktopIndex == targetDesktopIdx) return;

            icon.DesktopIndex = targetDesktopIdx;
            int countOnTarget = GetIconsOnDesktop(targetDesktopIdx).Count;
            int cols = Math.Max(1, (ClientSize.Width - 60) / (int)ColSpacing);
            int slot = Math.Max(0, countOnTarget - 1);

            icon.X = MarginLeft + (slot % cols) * ColSpacing;
            icon.Y = MarginTop + (slot / cols) * RowSpacing;

            _activeDesktopIndex = targetDesktopIdx;
            _selectedIcon = icon;
            _statusMessage = $"Przeniesiono '{icon.DisplayName}' na D{targetDesktopIdx + 1}";
            RecalculateAllScrollBounds();
            SavePersistentState();
            Invalidate();
        }

        private void RecalculateAllScrollBounds()
        {
            if (!_initialized || _desktops == null || _allIcons == null) return;
            int workHeight = Math.Max(100, ClientSize.Height - TaskbarHeight);

            for (int d = 0; d < DesktopCount; d++)
            {
                var desk = _desktops[d];
                if (desk == null) continue;

                float bottomMost = MarginTop + IconHeight + 40f;
                foreach (var icon in _allIcons)
                {
                    if (icon == null || icon.DesktopIndex != d) continue;
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
            SavePersistentState();
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
                    SavePersistentState();
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
            _statusMessage = $"Zmieniono kolor ikony: {_selectedIcon.DisplayName}";
            SavePersistentState();
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
                    _statusMessage = $"Ustawiono ikonę dla {_selectedIcon.DisplayName}";
                    SavePersistentState();
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
                SavePersistentState();

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

                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{csprojPath}\" -- \"{_hobbyOsRootDir}\"",
                    WorkingDirectory = _hobbyOsRootDir,
                    UseShellExecute = true
                };

                Process.Start(psi);
                _statusMessage = $"Odpalono: {icon.FileName}";
            }
            catch (Exception ex)
            {
                _statusMessage = "Błąd uruchamiania: " + ex.Message;
                MessageBox.Show(this, ex.ToString(), "Błąd startu aplikacji", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            int btnW = 40;
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
            if (!_initialized) return;
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
            else
            {
                int dKey = e.KeyCode switch
                {
                    Keys.D1 or Keys.NumPad1 => 0,
                    Keys.D2 or Keys.NumPad2 => 1,
                    Keys.D3 or Keys.NumPad3 => 2,
                    Keys.D4 or Keys.NumPad4 => 3,
                    Keys.D5 or Keys.NumPad5 => 4,
                    _ => -1
                };

                if (dKey >= 0 && dKey < DesktopCount)
                {
                    if (e.Shift && _selectedIcon != null)
                    {
                        MoveIconToDesktop(_selectedIcon, dKey);
                    }
                    else
                    {
                        _activeDesktopIndex = dKey;
                        _selectedIcon = null;
                        _statusMessage = $"Przełączono na {_desktops[dKey].Name}";
                        Invalidate();
                    }
                    e.Handled = true;
                }
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

            for (int d = 0; d < DesktopCount; d++)
            {
                if (GetDesktopSwitcherRect(d).Contains(e.Location))
                {
                    if (_selectedIcon != null && _selectedIcon.DesktopIndex != d)
                    {
                        MoveIconToDesktop(_selectedIcon, d);
                    }
                    else
                    {
                        _activeDesktopIndex = d;
                        _selectedIcon = null;
                        _startMenuOpen = false;
                        _statusMessage = $"Przełączono na {_desktops[d].Name}";
                        Invalidate();
                    }
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
                float scrollY = ActiveDesktop.ScrollY;
                ShellIconItem hit = null;

                for (int i = _allIcons.Count - 1; i >= 0; i--)
                {
                    var icon = _allIcons[i];
                    if (icon.DesktopIndex != _activeDesktopIndex) continue;
                    if (icon.GetScreenBounds(scrollY).Contains(e.Location))
                    {
                        hit = icon;
                        break;
                    }
                }

                _selectedIcon = hit;
                if (hit != null)
                {
                    _allIcons.Remove(hit);
                    _allIcons.Add(hit);

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
                SavePersistentState();
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

                RecalculateAllScrollBounds();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_isDragging && _draggingIcon != null)
                {
                    // Sprawdź, czy upuszczono ikonę na przycisk pulpitu D1..D5 na pasku zadań
                    for (int d = 0; d < DesktopCount; d++)
                    {
                        if (GetDesktopSwitcherRect(d).Contains(e.Location))
                        {
                            var targetIcon = _draggingIcon;
                            _isDragging = false;
                            _draggingIcon = null;
                            MoveIconToDesktop(targetIcon, d);
                            return;
                        }
                    }

                    SavePersistentState();
                }

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

            float scrollY = ActiveDesktop.ScrollY;

            for (int i = _allIcons.Count - 1; i >= 0; i--)
            {
                var icon = _allIcons[i];
                if (icon.DesktopIndex != _activeDesktopIndex) continue;
                if (icon.GetScreenBounds(scrollY).Contains(e.Location))
                {
                    _selectedIcon = icon;
                    LaunchIconScript(icon);
                    return;
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SavePersistentState();
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

            var visibleIcons = GetIconsOnDesktop(_activeDesktopIndex);
            if (visibleIcons.Count == 0)
            {
                using var infoFont = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var infoBrush = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
                g.DrawString(
                    desk.Name + " jest pusty.\nPrzeciągnij aplikację na przycisk D" + (_activeDesktopIndex + 1) + " lub wciśnij F5 aby przeskanować HobbyOS.",
                    infoFont, infoBrush, 24, 24);
            }

            using var labelFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var subFont = new Font("Consolas", 7.2f, FontStyle.Regular);
            using var shadowBrush = new SolidBrush(Color.FromArgb(195, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.White);
            using var subBrush = new SolidBrush(Color.FromArgb(220, 210, 235, 255));

            foreach (var icon in _allIcons)
            {
                if (icon.DesktopIndex != _activeDesktopIndex) continue;

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
            for (int d = 0; d < DesktopCount; d++)
            {
                Rectangle dr = GetDesktopSwitcherRect(d);
                bool active = d == _activeDesktopIndex;
                bool dropTarget = _isDragging && _draggingIcon != null && _draggingIcon.DesktopIndex != d;

                using var dBg = new SolidBrush(active
                    ? Color.FromArgb(42, 145, 225)
                    : (dropTarget ? Color.FromArgb(185, 85, 165, 60) : Color.FromArgb(85, 58, 72, 92)));
                using var dBorder = new Pen(active || dropTarget ? Color.White : Color.FromArgb(110, 150, 180, 210), 1.2f);
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

            int switcherEndX = StartButtonWidth + 10 + DesktopCount * 44 + 10;
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

        private static void CleanStaleWinOneShellTemp()
        {
            try
            {
                string staleDir = Path.Combine(Path.GetTempPath(), "HobbyOSApps", "WinOneShell");
                if (Directory.Exists(staleDir))
                {
                    Directory.Delete(staleDir, true);
                }
            }
            catch
            {
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            CleanStaleWinOneShellTemp();
            try
            {
                ApplicationConfiguration.Initialize();
                string rootArg = (args != null && args.Length > 0) ? args[0] : null;
                var form = new WinOneShellForm(rootArg);
                form.Show();
                form.Activate();
                Application.Run(form);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                try
                {
                    MessageBox.Show(ex.ToString(), "WinOneShell — błąd uruchomienia", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                }
            }
        }
    }
}
