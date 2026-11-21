using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HobbyOS.PullPity
{
    public enum BlockType : byte
    {
        Air = 0,
        Grass = 1,
        Dirt = 2,
        Stone = 3,
        Wood = 4,
        Leaves = 5,
        Sand = 6,
        Water = 7
    }

    public sealed class Minecraft2DForm : Form
    {
        private const int WorldWidth = 140;
        private const int WorldHeight = 64;
        private const int TileSize = 32;
        private const float Gravity = 28f;
        private const float JumpVelocity = -11.5f;
        private const float MoveSpeed = 6.2f;
        private const float ReachTiles = 5.5f;

        private readonly BlockType[,] _world = new BlockType[WorldWidth, WorldHeight];
        private readonly Timer _gameTimer = new Timer();
        private readonly string _worldSavePath;

        private float _playerX = (WorldWidth / 2f) * TileSize;
        private float _playerY = 10f * TileSize;
        private const float PlayerW = TileSize * 0.65f;
        private const float PlayerH = TileSize * 1.75f;

        private float _vx;
        private float _vy;
        private bool _onGround;

        private bool _moveLeft;
        private bool _moveRight;
        private bool _jumpPressed;

        private BlockType _selectedBlock = BlockType.Grass;
        private int _seed = 2026;
        private float _camX;
        private float _camY;
        private Point _mousePos;

        private static readonly BlockType[] HotbarBlocks =
        {
            BlockType.Grass,
            BlockType.Dirt,
            BlockType.Stone,
            BlockType.Wood,
            BlockType.Leaves,
            BlockType.Sand,
            BlockType.Water
        };

        public Minecraft2DForm()
        {
            Text = "HobbyOS Pull-Pit — 2D Minecraft";
            ClientSize = new Size(1120, 700);
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(120, 185, 245);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _worldSavePath = Path.Combine(baseDir, "hobbyos_mc2d_world.dat");

            if (!TryLoadWorld())
            {
                GenerateWorld(_seed);
            }

            _gameTimer.Interval = 16; // ~60 FPS
            _gameTimer.Tick += OnGameTick;
            _gameTimer.Start();
        }

        private void GenerateWorld(int seed)
        {
            _seed = seed;
            var rng = new Random(seed);

            int[] heights = new int[WorldWidth];
            double h = 26;
            for (int x = 0; x < WorldWidth; x++)
            {
                h += (rng.NextDouble() - 0.5) * 2.4;
                h = Math.Clamp(h, 16, 38);
                heights[x] = (int)Math.Round(h);
            }

            int waterLevel = 29;

            for (int x = 0; x < WorldWidth; x++)
            {
                int surface = heights[x];
                for (int y = 0; y < WorldHeight; y++)
                {
                    if (y > surface + 4)
                    {
                        _world[x, y] = BlockType.Stone;
                    }
                    else if (y > surface)
                    {
                        _world[x, y] = (surface >= waterLevel - 1) ? BlockType.Sand : BlockType.Dirt;
                    }
                    else if (y == surface)
                    {
                        _world[x, y] = (surface >= waterLevel - 1) ? BlockType.Sand : BlockType.Grass;
                    }
                    else if (y >= waterLevel)
                    {
                        _world[x, y] = BlockType.Water;
                    }
                    else
                    {
                        _world[x, y] = BlockType.Air;
                    }
                }

                // Drzewa na trawie
                if (_world[x, surface] == BlockType.Grass && x > 4 && x < WorldWidth - 5 && rng.Next(0, 9) == 0)
                {
                    int trunkHeight = rng.Next(4, 6);
                    for (int t = 1; t <= trunkHeight; t++)
                    {
                        if (surface - t >= 2)
                            _world[x, surface - t] = BlockType.Wood;
                    }

                    int crownY = surface - trunkHeight;
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        for (int dy = -2; dy <= 1; dy++)
                        {
                            if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2) continue;
                            int lx = x + dx;
                            int ly = crownY + dy;
                            if (lx >= 0 && lx < WorldWidth && ly >= 0 && ly < WorldHeight)
                            {
                                if (_world[lx, ly] == BlockType.Air)
                                    _world[lx, ly] = BlockType.Leaves;
                            }
                        }
                    }
                    x += 2; // odstęp między drzewami
                }
            }

            // Ustaw gracza bezpiecznie nad środkiem
            int spawnX = WorldWidth / 2;
            int spawnSurface = heights[spawnX];
            _playerX = spawnX * TileSize + (TileSize - PlayerW) / 2f;
            _playerY = (spawnSurface - 3) * TileSize;
            _vx = 0;
            _vy = 0;
        }

        private bool TryLoadWorld()
        {
            try
            {
                if (!File.Exists(_worldSavePath)) return false;
                byte[] data = File.ReadAllBytes(_worldSavePath);
                if (data.Length != WorldWidth * WorldHeight) return false;

                int idx = 0;
                for (int y = 0; y < WorldHeight; y++)
                {
                    for (int x = 0; x < WorldWidth; x++)
                    {
                        byte b = data[idx++];
                        _world[x, y] = Enum.IsDefined(typeof(BlockType), b) ? (BlockType)b : BlockType.Air;
                    }
                }

                int midX = WorldWidth / 2;
                int topY = 4;
                while (topY < WorldHeight - 2 && _world[midX, topY] == BlockType.Air) topY++;
                _playerX = midX * TileSize;
                _playerY = Math.Max(0, (topY - 3) * TileSize);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SaveWorld()
        {
            try
            {
                byte[] data = new byte[WorldWidth * WorldHeight];
                int idx = 0;
                for (int y = 0; y < WorldHeight; y++)
                {
                    for (int x = 0; x < WorldWidth; x++)
                    {
                        data[idx++] = (byte)_world[x, y];
                    }
                }
                File.WriteAllBytes(_worldSavePath, data);
            }
            catch
            {
                // Ignoruj błędy zapisu w kontenerze
            }
        }

        private void OnGameTick(object? sender, EventArgs e)
        {
            float dt = _gameTimer.Interval / 1000f;

            float dir = 0f;
            if (_moveLeft) dir -= 1f;
            if (_moveRight) dir += 1f;
            _vx = dir * MoveSpeed;

            if (_jumpPressed && _onGround)
            {
                _vy = JumpVelocity;
                _onGround = false;
            }
            _jumpPressed = false;

            _vy += Gravity * dt;
            if (_vy > 22f) _vy = 22f;

            // Ruch poziomy + kolizja
            float nextX = _playerX + _vx * TileSize * dt;
            nextX = Math.Clamp(nextX, 0, (WorldWidth * TileSize) - PlayerW);
            if (!CollidesWithSolid(nextX, _playerY, PlayerW, PlayerH))
            {
                _playerX = nextX;
            }

            // Ruch pionowy + kolizja
            float nextY = _playerY + _vy * TileSize * dt;
            _onGround = false;

            if (!CollidesWithSolid(_playerX, nextY, PlayerW, PlayerH))
            {
                _playerY = nextY;
            }
            else
            {
                if (_vy > 0)
                {
                    _onGround = true;
                    int bottomTile = (int)((nextY + PlayerH) / TileSize);
                    _playerY = bottomTile * TileSize - PlayerH - 0.001f;
                }
                else if (_vy < 0)
                {
                    int topTile = (int)(nextY / TileSize);
                    _playerY = (topTile + 1) * TileSize + 0.001f;
                }
                _vy = 0;
            }

            // Kamera wyśrodkowana na graczu
            float targetCamX = _playerX + PlayerW / 2f - ClientSize.Width / 2f;
            float targetCamY = _playerY + PlayerH / 2f - ClientSize.Height / 2f;

            float maxCamX = Math.Max(0, WorldWidth * TileSize - ClientSize.Width);
            float maxCamY = Math.Max(0, WorldHeight * TileSize - ClientSize.Height);

            _camX = Math.Clamp(targetCamX, 0, maxCamX);
            _camY = Math.Clamp(targetCamY, 0, maxCamY);

            Invalidate();
        }

        private bool CollidesWithSolid(float x, float y, float w, float h)
        {
            int left = Math.Max(0, (int)(x / TileSize));
            int right = Math.Min(WorldWidth - 1, (int)((x + w - 0.01f) / TileSize));
            int top = Math.Max(0, (int)(y / TileSize));
            int bottom = Math.Min(WorldHeight - 1, (int)((y + h - 0.01f) / TileSize));

            for (int ty = top; ty <= bottom; ty++)
            {
                for (int tx = left; tx <= right; tx++)
                {
                    BlockType b = _world[tx, ty];
                    if (b != BlockType.Air && b != BlockType.Water)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.Location;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _mousePos = e.Location;

            int tx = (int)((_mousePos.X + _camX) / TileSize);
            int ty = (int)((_mousePos.Y + _camY) / TileSize);
            if (tx < 0 || tx >= WorldWidth || ty < 0 || ty >= WorldHeight) return;

            float playerCenterX = (_playerX + PlayerW / 2f) / TileSize;
            float playerCenterY = (_playerY + PlayerH / 2f) / TileSize;
            double dist = Math.Sqrt(Math.Pow(tx + 0.5 - playerCenterX, 2) + Math.Pow(ty + 0.5 - playerCenterY, 2));
            if (dist > ReachTiles) return;

            if (e.Button == MouseButtons.Left)
            {
                _world[tx, ty] = BlockType.Air;
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (_world[tx, ty] == BlockType.Air || _world[tx, ty] == BlockType.Water)
                {
                    // Nie stawiaj bloku dokładnie na ciele gracza
                    float bx = tx * TileSize;
                    float by = ty * TileSize;
                    bool overlapsPlayer =
                        _playerX < bx + TileSize &&
                        _playerX + PlayerW > bx &&
                        _playerY < by + TileSize &&
                        _playerY + PlayerH > by;

                    if (!overlapsPlayer || _selectedBlock == BlockType.Water)
                    {
                        _world[tx, ty] = _selectedBlock;
                    }
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int idx = Array.IndexOf(HotbarBlocks, _selectedBlock);
            if (idx < 0) idx = 0;

            if (e.Delta > 0)
                idx = (idx - 1 + HotbarBlocks.Length) % HotbarBlocks.Length;
            else if (e.Delta < 0)
                idx = (idx + 1) % HotbarBlocks.Length;

            _selectedBlock = HotbarBlocks[idx];
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.A || e.KeyCode == Keys.Left) _moveLeft = true;
            if (e.KeyCode == Keys.D || e.KeyCode == Keys.Right) _moveRight = true;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.W || e.KeyCode == Keys.Up) _jumpPressed = true;

            if (e.KeyCode == Keys.R)
            {
                GenerateWorld(_seed + 1337);
            }
            else if (e.KeyCode == Keys.S)
            {
                SaveWorld();
            }

            int slot = e.KeyCode switch
            {
                Keys.D1 or Keys.NumPad1 => 0,
                Keys.D2 or Keys.NumPad2 => 1,
                Keys.D3 or Keys.NumPad3 => 2,
                Keys.D4 or Keys.NumPad4 => 3,
                Keys.D5 or Keys.NumPad5 => 4,
                Keys.D6 or Keys.NumPad6 => 5,
                Keys.D7 or Keys.NumPad7 => 6,
                _ => -1
            };

            if (slot >= 0 && slot < HotbarBlocks.Length)
            {
                _selectedBlock = HotbarBlocks[slot];
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.A || e.KeyCode == Keys.Left) _moveLeft = false;
            if (e.KeyCode == Keys.D || e.KeyCode == Keys.Right) _moveRight = false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveWorld();
            _gameTimer.Stop();
            _gameTimer.Dispose();
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            int startX = Math.Max(0, (int)(_camX / TileSize));
            int endX = Math.Min(WorldWidth - 1, (int)((_camX + ClientSize.Width) / TileSize) + 1);
            int startY = Math.Max(0, (int)(_camY / TileSize));
            int endY = Math.Min(WorldHeight - 1, (int)((_camY + ClientSize.Height) / TileSize) + 1);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    BlockType block = _world[x, y];
                    if (block == BlockType.Air) continue;

                    int sx = (int)(x * TileSize - _camX);
                    int sy = (int)(y * TileSize - _camY);
                    DrawBlock(g, block, sx, sy, TileSize);
                }
            }

            // Podświetlenie kafelka pod kursorem
            int hoverTx = (int)((_mousePos.X + _camX) / TileSize);
            int hoverTy = (int)((_mousePos.Y + _camY) / TileSize);
            if (hoverTx >= 0 && hoverTx < WorldWidth && hoverTy >= 0 && hoverTy < WorldHeight)
            {
                int hx = (int)(hoverTx * TileSize - _camX);
                int hy = (int)(hoverTy * TileSize - _camY);
                using var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 2f);
                g.DrawRectangle(pen, hx, hy, TileSize, TileSize);
            }

            // Gracz (Steve-style prosty model 2D)
            int px = (int)(_playerX - _camX);
            int py = (int)(_playerY - _camY);
            int headH = (int)(PlayerH * 0.30f);
            int bodyH = (int)(PlayerH * 0.36f);
            int legsH = (int)PlayerH - headH - bodyH;

            using (var headBrush = new SolidBrush(Color.FromArgb(232, 184, 148)))
            using (var shirtBrush = new SolidBrush(Color.FromArgb(35, 155, 175)))
            using (var pantsBrush = new SolidBrush(Color.FromArgb(45, 58, 128)))
            using (var eyeBrush = new SolidBrush(Color.FromArgb(25, 25, 30)))
            {
                g.FillRectangle(headBrush, px, py, (int)PlayerW, headH);
                g.FillRectangle(shirtBrush, px, py + headH, (int)PlayerW, bodyH);
                g.FillRectangle(pantsBrush, px, py + headH + bodyH, (int)PlayerW, legsH);

                int eyeX = _vx >= 0 ? px + (int)(PlayerW * 0.62f) : px + (int)(PlayerW * 0.18f);
                g.FillRectangle(eyeBrush, eyeX, py + (int)(headH * 0.38f), 4, 4);
            }

            // Górny pasek informacyjny
            using (var hudBg = new SolidBrush(Color.FromArgb(150, 15, 20, 28)))
            using (var hudFont = new Font("Consolas", 10f, FontStyle.Bold))
            using (var hudText = new SolidBrush(Color.White))
            {
                g.FillRectangle(hudBg, 10, 10, 740, 28);
                g.DrawString(
                    $"HobbyOS 2D MC | Blok: {_selectedBlock} | LPM: Zniszcz | PPM: Postaw | R: Nowy świat | S: Zapisz",
                    hudFont, hudText, 18, 16);
            }

            // Dolny hotbar (1-7)
            int slotSize = 44;
            int gap = 8;
            int totalW = HotbarBlocks.Length * slotSize + (HotbarBlocks.Length - 1) * gap;
            int barX = (ClientSize.Width - totalW) / 2;
            int barY = ClientSize.Height - slotSize - 16;

            using var barBg = new SolidBrush(Color.FromArgb(175, 20, 24, 32));
            g.FillRectangle(barBg, barX - 8, barY - 8, totalW + 16, slotSize + 16);

            using var numFont = new Font("Consolas", 8.5f, FontStyle.Bold);
            using var numBrush = new SolidBrush(Color.White);

            for (int i = 0; i < HotbarBlocks.Length; i++)
            {
                int sx = barX + i * (slotSize + gap);
                bool active = HotbarBlocks[i] == _selectedBlock;

                using var slotBg = new SolidBrush(active ? Color.FromArgb(230, 80, 140, 220) : Color.FromArgb(180, 45, 50, 62));
                g.FillRectangle(slotBg, sx, barY, slotSize, slotSize);

                DrawBlock(g, HotbarBlocks[i], sx + 8, barY + 8, slotSize - 16);

                if (active)
                {
                    using var selPen = new Pen(Color.Gold, 2.5f);
                    g.DrawRectangle(selPen, sx, barY, slotSize, slotSize);
                }

                g.DrawString((i + 1).ToString(), numFont, numBrush, sx + 2, barY + 1);
            }
        }

        private static void DrawBlock(Graphics g, BlockType block, int x, int y, int size)
        {
            Color baseColor = block switch
            {
                BlockType.Grass => Color.FromArgb(92, 168, 56),
                BlockType.Dirt => Color.FromArgb(134, 92, 58),
                BlockType.Stone => Color.FromArgb(125, 130, 138),
                BlockType.Wood => Color.FromArgb(110, 74, 42),
                BlockType.Leaves => Color.FromArgb(56, 132, 48),
                BlockType.Sand => Color.FromArgb(224, 204, 132),
                BlockType.Water => Color.FromArgb(52, 132, 224, 185),
                _ => Color.Magenta
            };

            using var brush = new SolidBrush(baseColor);
            g.FillRectangle(brush, x, y, size, size);

            if (block == BlockType.Grass)
            {
                using var topBrush = new SolidBrush(Color.FromArgb(118, 202, 68));
                using var dirtBrush = new SolidBrush(Color.FromArgb(134, 92, 58));
                int topH = Math.Max(4, size / 4);
                g.FillRectangle(dirtBrush, x, y + topH, size, size - topH);
                g.FillRectangle(topBrush, x, y, size, topH);
            }
            else if (block == BlockType.Wood)
            {
                using var ringPen = new Pen(Color.FromArgb(78, 50, 26), 1.5f);
                g.DrawLine(ringPen, x + size / 3, y, x + size / 3, y + size);
                g.DrawLine(ringPen, x + (2 * size) / 3, y, x + (2 * size) / 3, y + size);
            }

            using var borderPen = new Pen(Color.FromArgb(55, 0, 0, 0), 1f);
            g.DrawRectangle(borderPen, x, y, size, size);
        }

        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Minecraft2DForm());
        }
    }
}
