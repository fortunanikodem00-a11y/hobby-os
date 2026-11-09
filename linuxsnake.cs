using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class LinuxSnakeGame : Form
{
    private List<Point> snake = new List<Point>();
    private Point food;
    private int dx = 1, dy = 0;
    private int score = 0, highScore = 0;
    private bool gameOver = false;
    private System.Windows.Forms.Timer gameTimer;
    private Random rnd = new Random();
    private const int TileSize = 20;
    private const int GridW = 30;
    private const int GridH = 22;

    public LinuxSnakeGame()
    {
        this.Text = "admin@hobby-os: ~/games/snake-2d";
        this.ClientSize = new Size(GridW * TileSize, GridH * TileSize + 45);
        this.BackColor = Color.FromArgb(48, 10, 36);
        this.DoubleBuffered = true;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        gameTimer = new System.Windows.Forms.Timer();
        gameTimer.Tick += UpdateGame;

        this.KeyDown += HandleInput;
        StartNewGame();
    }

    private void StartNewGame()
    {
        snake.Clear();
        snake.Add(new Point(10, 10));
        snake.Add(new Point(9, 10));
        snake.Add(new Point(8, 10));
        dx = 1;
        dy = 0;
        score = 0;
        gameOver = false;
        gameTimer.Interval = 110;
        SpawnFood();
        gameTimer.Start();
        this.Invalidate();
    }

    private void SpawnFood()
    {
        while (true)
        {
            Point p = new Point(rnd.Next(0, GridW), rnd.Next(0, GridH));
            if (!snake.Contains(p))
            {
                food = p;
                break;
            }
        }
    }

    private void HandleInput(object? sender, KeyEventArgs e)
    {
        if (gameOver && e.KeyCode == Keys.Space)
        {
            StartNewGame();
            return;
        }

        if ((e.KeyCode == Keys.Up || e.KeyCode == Keys.W) && dy != 1) { dx = 0; dy = -1; }
        else if ((e.KeyCode == Keys.Down || e.KeyCode == Keys.S) && dy != -1) { dx = 0; dy = 1; }
        else if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.A) && dx != 1) { dx = -1; dy = 0; }
        else if ((e.KeyCode == Keys.Right || e.KeyCode == Keys.D) && dx != -1) { dx = 1; dy = 0; }
    }

    private void UpdateGame(object? sender, EventArgs e)
    {
        if (gameOver) return;

        Point head = snake[0];
        Point next = new Point(head.X + dx, head.Y + dy);

        if (next.X < 0 || next.X >= GridW || next.Y < 0 || next.Y >= GridH || snake.Contains(next))
        {
            gameOver = true;
            gameTimer.Stop();
            if (score > highScore) highScore = score;
            this.Invalidate();
            return;
        }

        snake.Insert(0, next);

        if (next == food)
        {
            score += 10;
            if (score > highScore) highScore = score;
            if (gameTimer.Interval > 45) gameTimer.Interval -= 3;
            SpawnFood();
        }
        else
        {
            snake.RemoveAt(snake.Count - 1);
        }

        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        using (SolidBrush topBar = new SolidBrush(Color.FromArgb(30, 6, 22)))
        {
            g.FillRectangle(topBar, 0, 0, this.ClientSize.Width, 45);
        }

        using (Font f = new Font("Consolas", 11, FontStyle.Bold))
        {
            g.DrawString($"admin@hobby-os:~$ ./snake  |  SCORE: {score}  |  BEST: {highScore}", f, Brushes.LimeGreen, 12, 13);
        }

        int offsetY = 45;

        using (Pen gridPen = new Pen(Color.FromArgb(62, 16, 48)))
        {
            for (int x = 0; x <= GridW; x++) g.DrawLine(gridPen, x * TileSize, offsetY, x * TileSize, offsetY + GridH * TileSize);
            for (int y = 0; y <= GridH; y++) g.DrawLine(gridPen, 0, offsetY + y * TileSize, GridW * TileSize, offsetY + y * TileSize);
        }

        using (SolidBrush foodBrush = new SolidBrush(Color.FromArgb(255, 85, 85)))
        {
            g.FillEllipse(foodBrush, food.X * TileSize + 2, offsetY + food.Y * TileSize + 2, TileSize - 4, TileSize - 4);
        }

        for (int i = 0; i < snake.Count; i++)
        {
            Brush b = (i == 0) ? Brushes.Lime : Brushes.MediumSpringGreen;
            g.FillRectangle(b, snake[i].X * TileSize + 1, offsetY + snake[i].Y * TileSize + 1, TileSize - 2, TileSize - 2);
        }

        if (gameOver)
        {
            using (SolidBrush overlay = new SolidBrush(Color.FromArgb(190, 15, 5, 12)))
            {
                g.FillRectangle(overlay, 0, offsetY, GridW * TileSize, GridH * TileSize);
            }
            using (Font big = new Font("Consolas", 20, FontStyle.Bold))
            using (Font sub = new Font("Consolas", 12, FontStyle.Regular))
            {
                g.DrawString("SEGMENTATION FAULT (GAME OVER)", big, Brushes.OrangeRed, 65, offsetY + 160);
                g.DrawString($"Twój wynik: {score}  |  Naciśnij SPACJĘ, aby zrestartować", sub, Brushes.White, 85, offsetY + 210);
            }
        }
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new LinuxSnakeGame());
    }
}
