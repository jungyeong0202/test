// 화면 하단(작업 표시줄) 위를 포켓몬이 돌아다니는 데스크톱 펫 - 파이썬 없이 도는 C# 판.
//
// 윈도우에 기본 탑재된 .NET Framework 컴파일러로 빌드한다. run.bat 참고.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PokemonTaskbar
{
    /// <summary>명령줄로 받은 설정.</summary>
    public class Options
    {
        public List<string> Species = new List<string>();
        public int Count = 1;
        public int Scale = 3;
        public double Speed = 55.0;
        public int Offset = 0;
        public bool OnTaskbar = false;
        public bool ShowList = false;
        public bool ShowHelp = false;
        public string Error = null;
    }

    /// <summary>도트 문자열을 화면에 그릴 비트맵으로 바꾼다.</summary>
    public static class SpriteFactory
    {
        private static Color ParseColor(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }

        /// <summary>걷기 프레임들을 색 배열로 만든다. 빈 칸은 null.</summary>
        public static List<Color?[][]> Frames(PokemonSprite sprite)
        {
            int width = 0;
            foreach (string[] frame in sprite.Frames)
            {
                foreach (string row in frame)
                {
                    if (row.Length > width)
                    {
                        width = row.Length;
                    }
                }
            }

            List<Color?[][]> frames = new List<Color?[][]>();
            foreach (string[] rows in sprite.Frames)
            {
                Color?[][] grid = new Color?[rows.Length][];
                for (int y = 0; y < rows.Length; y++)
                {
                    grid[y] = new Color?[width];
                    string row = rows[y].PadRight(width, '.');
                    for (int x = 0; x < width; x++)
                    {
                        char key = row[x];
                        grid[y][x] = key == '.' ? (Color?)null : ParseColor(sprite.Palette[key]);
                    }
                }
                frames.Add(grid);
            }
            return frames;
        }

        /// <summary>색 배열을 확대해 비트맵으로 그린다. flip 이면 좌우로 뒤집는다.</summary>
        public static Bitmap Render(Color?[][] grid, int scale, bool flip)
        {
            int height = grid.Length;
            int width = grid[0].Length;
            Bitmap bitmap = new Bitmap(width * scale, height * scale, PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                Dictionary<int, SolidBrush> brushes = new Dictionary<int, SolidBrush>();

                for (int y = 0; y < height; y++)
                {
                    int x = 0;
                    while (x < width)
                    {
                        Color? color = grid[y][flip ? width - 1 - x : x];
                        if (color == null)
                        {
                            x++;
                            continue;
                        }

                        int end = x;
                        while (end < width)
                        {
                            Color? next = grid[y][flip ? width - 1 - end : end];
                            if (next == null || next.Value.ToArgb() != color.Value.ToArgb())
                            {
                                break;
                            }
                            end++;
                        }

                        int argb = color.Value.ToArgb();
                        if (!brushes.ContainsKey(argb))
                        {
                            brushes[argb] = new SolidBrush(color.Value);
                        }
                        graphics.FillRectangle(
                            brushes[argb], x * scale, y * scale, (end - x) * scale, scale);
                        x = end;
                    }
                }

                foreach (SolidBrush brush in brushes.Values)
                {
                    brush.Dispose();
                }
            }
            return bitmap;
        }
    }

    /// <summary>포켓몬 한 마리. 테두리 없는 항상-맨-앞 창이다.</summary>
    public class PetForm : Form
    {
        private const int TickMs = 40;
        private const double StepSeconds = 0.16;
        private const double JumpSeconds = 0.45;
        private const int TopmostTicks = 5;   // 5틱 = 0.2초마다 맨 앞을 다시 주장

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;

        private readonly Bitmap[][] images;   // [0] 오른쪽, [1] 왼쪽
        private readonly Timer timer;
        private readonly Random random;
        private readonly int spriteWidth;
        private readonly int spriteHeight;
        private readonly int hop;
        private readonly int frameCount;
        private readonly int scale;
        private readonly int maxX;
        private readonly int baseY;
        private readonly double speed;

        private double x;
        private int direction;
        private bool walking = true;
        private double idleLeft;
        private double animTime;
        private double jumpTime = -1.0;
        private int ticks;

        public PetForm(PetWorld world, PokemonSprite sprite)
        {
            this.random = world.Random;

            List<Color?[][]> frames = SpriteFactory.Frames(sprite);
            this.frameCount = frames.Count;
            int scale = Math.Max(1, (int)Math.Round(world.Options.Scale * sprite.ScaleFactor));
            this.scale = scale;
            this.images = new Bitmap[2][];
            this.images[0] = new Bitmap[frames.Count];
            this.images[1] = new Bitmap[frames.Count];
            // images[0] 은 오른쪽으로 갈 때, images[1] 은 왼쪽으로 갈 때 쓴다.
            // 원본이 보고 있는 방향과 가려는 방향이 다를 때만 뒤집는다.
            for (int i = 0; i < frames.Count; i++)
            {
                this.images[0][i] = SpriteFactory.Render(frames[i], scale, !sprite.FacesRight);
                this.images[1][i] = SpriteFactory.Render(frames[i], scale, sprite.FacesRight);
            }

            this.spriteWidth = this.images[0][0].Width;
            this.spriteHeight = this.images[0][0].Height;
            this.hop = scale;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.ClientSize = new Size(this.spriteWidth, this.spriteHeight + this.hop);
            this.Text = sprite.NameKo;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            Rectangle screen = Screen.PrimaryScreen.Bounds;
            // 기본값은 작업 표시줄 "위"에 올라서기. 작업 영역의 아래쪽 선이 곧 표시줄의 윗변이다.
            int ground = world.Options.OnTaskbar
                ? screen.Bottom
                : Screen.PrimaryScreen.WorkingArea.Bottom;
            this.maxX = Math.Max(0, screen.Width - this.spriteWidth);
            this.baseY = ground - (this.spriteHeight + this.hop) - world.Options.Offset;
            this.x = this.random.NextDouble() * this.maxX;
            this.direction = this.random.Next(2) == 0 ? -1 : 1;
            this.speed = world.Options.Speed * (0.85 + this.random.NextDouble() * 0.3);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("포켓몬 추가", null, delegate { world.AddRandom(); });
            menu.Items.Add("이 포켓몬 보내주기", null, delegate { world.Remove(this); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("전부 종료", null, delegate { world.QuitAll(); });
            this.ContextMenuStrip = menu;

            this.MoveToPlace();

            this.timer = new Timer();
            this.timer.Interval = TickMs;
            this.timer.Tick += this.OnTick;
            this.timer.Start();
        }

        /// <summary>알트탭 목록에 넣지 않고, 클릭해도 다른 창의 포커스를 빼앗지 않는다.</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= 0x00000080;  // WS_EX_TOOLWINDOW
                createParams.ExStyle |= 0x08000000;  // WS_EX_NOACTIVATE
                return createParams;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int frame = this.walking ? (int)(this.animTime / StepSeconds) % this.frameCount : 0;
            // 홀수 프레임에서 살짝 튀어올라 걷는 느낌을 준다.
            int bounce = (this.walking && frame % 2 == 1) ? this.hop : 0;
            Bitmap image = this.images[this.direction > 0 ? 0 : 1][frame];
            e.Graphics.DrawImageUnscaled(image, 0, this.hop - bounce);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && this.jumpTime < 0)
            {
                this.jumpTime = 0.0;
            }
            base.OnMouseDown(e);
        }

        private void OnTick(object sender, EventArgs e)
        {
            double dt = TickMs / 1000.0;
            this.ticks++;

            if (this.walking)
            {
                this.animTime += dt;
                this.x += this.direction * this.speed * dt;
                if (this.x <= 0)
                {
                    this.x = 0;
                    this.direction = 1;
                }
                else if (this.x >= this.maxX)
                {
                    this.x = this.maxX;
                    this.direction = -1;
                }
                else if (this.random.NextDouble() < 0.004)
                {
                    this.direction = -this.direction;
                }

                if (this.random.NextDouble() < 0.005)
                {
                    this.walking = false;
                    this.idleLeft = 0.8 + this.random.NextDouble() * 2.2;
                }
            }
            else
            {
                this.idleLeft -= dt;
                if (this.idleLeft <= 0)
                {
                    this.walking = true;
                }
            }

            if (this.jumpTime >= 0)
            {
                this.jumpTime += dt;
                if (this.jumpTime > JumpSeconds)
                {
                    this.jumpTime = -1.0;
                }
            }

            // 다른 창을 클릭해도 항상 맨 앞에 남도록 자주 다시 주장한다.
            if (this.ticks % TopmostTicks == 0)
            {
                this.RaiseAboveAll();
            }

            this.MoveToPlace();
            this.Invalidate();
        }

        /// <summary>포커스를 빼앗지 않으면서 창을 최상위로 올린다.</summary>
        private void RaiseAboveAll()
        {
            if (!this.IsHandleCreated || this.IsDisposed)
            {
                return;
            }
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    SetWindowPos(this.Handle, HwndTopmost, 0, 0, 0, 0,
                        SwpNoSize | SwpNoMove | SwpNoActivate);
                    return;
                }
                catch (DllNotFoundException)
                {
                    // 윈도우가 아닌 환경. 아래로 넘어간다.
                }
                catch (EntryPointNotFoundException)
                {
                }
            }
            this.TopMost = true;
        }

        private void MoveToPlace()
        {
            int y = this.baseY;
            if (this.jumpTime >= 0)
            {
                double height = this.scale * 6 * Math.Sin(Math.PI * this.jumpTime / JumpSeconds);
                y -= (int)height;
            }
            this.Location = new Point((int)this.x, y);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.timer != null)
                {
                    this.timer.Stop();
                    this.timer.Dispose();
                }
                foreach (Bitmap[] row in this.images)
                {
                    foreach (Bitmap image in row)
                    {
                        image.Dispose();
                    }
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>펫 여러 마리를 관리한다.</summary>
    public class PetWorld : ApplicationContext
    {
        public readonly Options Options;
        public readonly Random Random = new Random();
        private readonly List<PetForm> pets = new List<PetForm>();
        private bool quitting;

        public PetWorld(Options options)
        {
            this.Options = options;
            foreach (string key in options.Species)
            {
                this.Add(key);
            }
        }

        public void Add(string key)
        {
            PokemonSprite sprite = Sprites.Find(key);
            if (sprite == null)
            {
                return;
            }
            PetForm pet = new PetForm(this, sprite);
            pet.FormClosed += delegate { this.Forget(pet); };
            this.pets.Add(pet);
            pet.Show();
        }

        public void AddRandom()
        {
            this.Add(Sprites.All[this.Random.Next(Sprites.All.Count)].Key);
        }

        public void Remove(PetForm pet)
        {
            pet.Close();
        }

        private void Forget(PetForm pet)
        {
            this.pets.Remove(pet);
            if (this.pets.Count == 0 && !this.quitting)
            {
                this.ExitThread();
            }
        }

        public void QuitAll()
        {
            this.quitting = true;
            foreach (PetForm pet in this.pets.ToArray())
            {
                pet.Close();
            }
            this.pets.Clear();
            this.ExitThread();
        }
    }

    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private const string Usage =
            "하단바 포켓몬\n\n" +
            "  -p, --pokemon <이름>   등장시킬 포켓몬 (여러 번 사용 가능)\n" +
            "  -c, --count <수>       마리 수 (기본 1)\n" +
            "  -s, --scale <배율>     도트 확대 배율 (기본 3)\n" +
            "      --speed <속도>     이동 속도, 초당 픽셀 (기본 55)\n" +
            "      --offset <픽셀>    바닥에서 더 띄울 높이 (기본 0)\n" +
            "      --on-taskbar       작업 표시줄 위에 올라서지 않고 표시줄 위를 걷는다\n" +
            "      --list             포켓몬 목록 보기\n\n" +
            "포켓몬을 왼쪽 클릭하면 점프하고, 오른쪽 클릭하면 메뉴가 열립니다.";

        public static Options Parse(string[] argv)
        {
            Options options = new Options();
            for (int i = 0; i < argv.Length; i++)
            {
                string arg = argv[i];
                bool needsValue = arg == "-p" || arg == "--pokemon" || arg == "-c" || arg == "--count"
                    || arg == "-s" || arg == "--scale" || arg == "--speed" || arg == "--offset";
                if (needsValue && i + 1 >= argv.Length)
                {
                    options.Error = arg + " 뒤에 값이 필요합니다.";
                    return options;
                }

                switch (arg)
                {
                    case "-p":
                    case "--pokemon":
                        string key = argv[++i];
                        if (Sprites.Find(key) == null)
                        {
                            options.Error = "모르는 포켓몬입니다: " + key;
                            return options;
                        }
                        options.Species.Add(key);
                        break;
                    case "-c":
                    case "--count":
                        if (!int.TryParse(argv[++i], out options.Count) || options.Count < 1)
                        {
                            options.Error = "--count 는 1 이상의 숫자여야 합니다.";
                            return options;
                        }
                        break;
                    case "-s":
                    case "--scale":
                        if (!int.TryParse(argv[++i], out options.Scale) || options.Scale < 1)
                        {
                            options.Error = "--scale 은 1 이상의 숫자여야 합니다.";
                            return options;
                        }
                        break;
                    case "--speed":
                        if (!double.TryParse(argv[++i], out options.Speed) || options.Speed <= 0)
                        {
                            options.Error = "--speed 는 0보다 큰 숫자여야 합니다.";
                            return options;
                        }
                        break;
                    case "--offset":
                        if (!int.TryParse(argv[++i], out options.Offset))
                        {
                            options.Error = "--offset 은 숫자여야 합니다.";
                            return options;
                        }
                        break;
                    case "--on-taskbar":
                        options.OnTaskbar = true;
                        break;
                    case "--list":
                        options.ShowList = true;
                        break;
                    case "-h":
                    case "--help":
                    case "/?":
                        options.ShowHelp = true;
                        break;
                    default:
                        options.Error = "모르는 옵션입니다: " + arg;
                        return options;
                }
            }

            if (options.Species.Count == 0)
            {
                options.Species.Add("pikachu");
            }
            Random random = new Random();
            while (options.Species.Count < options.Count)
            {
                options.Species.Add(Sprites.All[random.Next(Sprites.All.Count)].Key);
            }
            return options;
        }

        [STAThread]
        public static int Main(string[] argv)
        {
            Options options = Parse(argv);

            if (options.Error != null)
            {
                MessageBox.Show(options.Error + "\n\n" + Usage, "하단바 포켓몬",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }

            if (options.ShowHelp)
            {
                MessageBox.Show(Usage, "하단바 포켓몬");
                return 0;
            }

            if (options.ShowList)
            {
                // 어떤 도트가 들어 있는지 보여 준다. 어느 빌드를 쓰는지 확인할 때 쓴다.
                string list = "";
                foreach (PokemonSprite sprite in Sprites.All)
                {
                    List<Color?[][]> frames = SpriteFactory.Frames(sprite);
                    list += string.Format(
                        "{0}  {1}  {2}x{3}  {4}프레임  {5} 보는 그림\n",
                        sprite.Key.PadRight(12), sprite.NameKo,
                        frames[0][0].Length, frames[0].Length, frames.Count,
                        sprite.FacesRight ? "오른쪽" : "왼쪽");
                }
                MessageBox.Show(list, "하단바 포켓몬 - 목록");
                return 0;
            }

            try
            {
                SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                // 아주 오래된 윈도우에서는 없을 수 있다. 무시해도 동작한다.
            }
            catch (DllNotFoundException)
            {
                // 윈도우가 아닌 환경(Mono 등).
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PetWorld(options));
            return 0;
        }
    }
}
