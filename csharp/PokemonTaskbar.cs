// 화면 하단(작업 표시줄) 위를 포켓몬이 돌아다니는 데스크톱 펫 - 파이썬 없이 도는 C# 판.
//
// 윈도우에 기본 탑재된 .NET Framework 컴파일러로 빌드한다. run.bat 참고.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PokemonTaskbar
{
    /// <summary>명령줄로 받은 설정.</summary>
    public class Options
    {
        public List<string> Species = new List<string>();
        public int Count = 0;      // 0 = 지정하지 않음
        public double Scale = 4.5;
        public double Speed = 55.0;
        public int Offset = 0;
        public bool OnTaskbar = false;
        public string SettingsPath = null;
        public bool SpeciesFromCommandLine = false;
        public bool ShowList = false;
        public bool ShowHelp = false;
        public string Error = null;
    }

    /// <summary>사용자 설정을 파일에 저장하고 불러온다.
    ///
    /// 파이썬 판과 같은 파일을 읽고 쓰므로 형식(한 줄에 `이름 = 값`)과
    /// 숫자 표기(InvariantCulture)를 맞춰 둔다.</summary>
    public static class SettingsFile
    {
        public const string EnvOverride = "POKEMON_TASKBAR_SETTINGS";

        public static string DefaultPath()
        {
            string over = Environment.GetEnvironmentVariable(EnvOverride);
            if (!string.IsNullOrEmpty(over))
            {
                return over;
            }
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(appdata, "PokemonTaskbar"), "settings.txt");
        }

        /// <summary>저장된 값을 options 에 채운다. 명령줄로 이미 정한 항목은 건드리지 않는다.</summary>
        public static void Load(Options options, HashSet<string> givenOnCommandLine)
        {
            string path = options.SettingsPath ?? DefaultPath();
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception)
            {
                return;                     // 파일이 없거나 읽을 수 없으면 기본값 그대로
            }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }
                int mark = line.IndexOf('=');
                if (mark < 0)
                {
                    continue;
                }
                string name = line.Substring(0, mark).Trim();
                string value = line.Substring(mark + 1).Trim();
                if (givenOnCommandLine.Contains(name))
                {
                    continue;               // 명령줄이 우선
                }

                double number;
                int whole;
                switch (name)
                {
                    case "species":
                        List<string> names = new List<string>();
                        foreach (string part in value.Split(','))
                        {
                            string key = part.Trim();
                            if (key.Length > 0 && Sprites.Find(key) != null)
                            {
                                names.Add(key);
                            }
                        }
                        if (names.Count > 0)
                        {
                            options.Species = names;
                        }
                        break;
                    case "scale":
                        if (double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out number) && number > 0)
                        {
                            options.Scale = number;
                        }
                        break;
                    case "speed":
                        if (double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out number) && number > 0)
                        {
                            options.Speed = number;
                        }
                        break;
                    case "offset":
                        if (int.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out whole))
                        {
                            options.Offset = whole;
                        }
                        break;
                    case "on_taskbar":
                        string flag = value.ToLowerInvariant();
                        options.OnTaskbar = flag == "1" || flag == "true" || flag == "yes" || flag == "on";
                        break;
                }
            }
        }

        /// <summary>실패해도 프로그램이 죽지 않도록 조용히 넘어간다.</summary>
        public static void Save(Options options, List<string> species)
        {
            string path = options.SettingsPath ?? DefaultPath();
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                List<string> lines = new List<string>();
                lines.Add("# 하단바 포켓몬 설정 - 프로그램이 자동으로 저장합니다.");
                lines.Add("# 직접 고쳐도 되고, 파일을 지우면 처음 상태로 돌아갑니다.");
                lines.Add("species = " + string.Join(", ", species.ToArray()));
                lines.Add("scale = " + options.Scale.ToString("G", CultureInfo.InvariantCulture));
                lines.Add("speed = " + options.Speed.ToString("G", CultureInfo.InvariantCulture));
                lines.Add("offset = " + options.Offset.ToString(CultureInfo.InvariantCulture));
                lines.Add("on_taskbar = " + (options.OnTaskbar ? "true" : "false"));
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>윈도우 시작 프로그램 등록. 현재 사용자(HKCU)에만 쓴다.</summary>
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PokemonTaskbar";

        public static bool Enabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Set(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    if (enabled)
                    {
                        key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
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
            int width = SpriteWidth(sprite);

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

        /// <summary>프레임과 자세를 통틀어 가장 넓은 줄의 길이.</summary>
        public static int SpriteWidth(PokemonSprite sprite)
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
            if (sprite.Poses != null)
            {
                foreach (string[] rows in sprite.Poses.Values)
                {
                    foreach (string row in rows)
                    {
                        if (row.Length > width)
                        {
                            width = row.Length;
                        }
                    }
                }
            }
            return width;
        }

        /// <summary>이름별 자세를 색 배열로.</summary>
        public static Dictionary<string, Color?[][]> Poses(PokemonSprite sprite)
        {
            Dictionary<string, Color?[][]> poses = new Dictionary<string, Color?[][]>();
            if (sprite.Poses == null)
            {
                return poses;
            }
            int width = SpriteWidth(sprite);
            foreach (KeyValuePair<string, string[]> pair in sprite.Poses)
            {
                string[] rows = pair.Value;
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
                poses[pair.Key] = grid;
            }
            return poses;
        }

        /// <summary>색 배열을 확대해 비트맵으로 그린다. flip 이면 좌우로 뒤집는다.
        ///
        /// scale 은 도트 하나가 차지할 화면 픽셀 수이며 소수여도 된다. 1.5 면
        /// 2픽셀과 1픽셀이 번갈아 나오도록 가장 가까운 도트를 찍는다.</summary>
        public static Bitmap Render(Color?[][] grid, double scale, bool flip)
        {
            int height = grid.Length;
            int width = grid[0].Length;
            // 가로세로에 같은 반올림 규칙을 써야 비율이 그대로 유지된다.
            // (Math.Round 는 .5 를 짝수로 보내므로 축마다 결과가 달라질 수 있다.)
            int outWidth = Math.Max(1, (int)Math.Floor(width * scale + 0.5));
            int outHeight = Math.Max(1, (int)Math.Floor(height * scale + 0.5));
            Bitmap bitmap = new Bitmap(outWidth, outHeight, PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                Dictionary<int, SolidBrush> brushes = new Dictionary<int, SolidBrush>();

                int outY = 0;
                while (outY < outHeight)
                {
                    int y = Math.Min(height - 1, outY * height / outHeight);
                    int endY = outY;
                    while (endY < outHeight && Math.Min(height - 1, endY * height / outHeight) == y)
                    {
                        endY++;
                    }
                    int band = endY - outY;

                    int outX = 0;
                    while (outX < outWidth)
                    {
                        int x = Math.Min(width - 1, outX * width / outWidth);
                        Color? color = grid[y][flip ? width - 1 - x : x];

                        int endX = outX;
                        while (endX < outWidth)
                        {
                            int nextX = Math.Min(width - 1, endX * width / outWidth);
                            Color? next = grid[y][flip ? width - 1 - nextX : nextX];
                            if (next == null != (color == null))
                            {
                                break;
                            }
                            if (next != null && next.Value.ToArgb() != color.Value.ToArgb())
                            {
                                break;
                            }
                            endX++;
                        }

                        if (color != null)
                        {
                            int argb = color.Value.ToArgb();
                            if (!brushes.ContainsKey(argb))
                            {
                                brushes[argb] = new SolidBrush(color.Value);
                            }
                            graphics.FillRectangle(brushes[argb], outX, outY, endX - outX, band);
                        }
                        outX = endX;
                    }
                    outY = endY;
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
        private const double Gravity = 900.0;      // 떨어지는 가속도(초당 픽셀^2)
        private const double JumpSpeed = 200.0;    // 클릭했을 때 튀어오르는 속도
        private const int DragSlack = 4;           // 이보다 많이 움직이면 끌기로 본다
        private const double HopSpeed = 205.0;     // 뛰어다니는 포켓몬이 튀어오르는 속도
        private const double HopCrouchSeconds = 0.10;
        private const double HopLandSeconds = 0.10;
        private const double HopRestMin = 0.10;
        private const double HopRestMax = 0.45;
        private const double HopBoost = 2.0;       // 공중에서만 나아가므로 걷기보다 빠르게
        private const double HopTurnChance = 0.12; // 착지할 때마다 이 확률로 방향을 바꾼다
        private const double EffectGravity = 260.0;   // 먼지가 떨어지는 가속도
        private const double DustLife = 0.40;
        private const double EmoteLife = 0.90;
        private const double LandDustSpeed = 60.0;    // 이보다 세게 떨어져야 먼지가 인다
        private const double NapChance = 0.18;        // 멈춰 설 때 이 확률로 낮잠
        private const double ZzzEvery = 1.1;
        private const double BlinkEveryMin = 3.0;
        private const double BlinkEveryMax = 7.0;
        private const double BlinkTime = 0.14;
        private const double LandSquashTime = 0.12;
        private const double BreathSeconds = 0.9;
        private const double WiggleSeconds = 0.10;

        // 효과에 쓰는 아주 작은 도트 그림
        private static readonly int[,] HeartDots = {
            {1,0},{2,0},{4,0},{5,0},
            {0,1},{1,1},{2,1},{3,1},{4,1},{5,1},{6,1},
            {0,2},{1,2},{2,2},{3,2},{4,2},{5,2},{6,2},
            {1,3},{2,3},{3,3},{4,3},{5,3},
            {2,4},{3,4},{4,4},
            {3,5},
        };
        private static readonly int[,] ZzzDots = {
            {0,0},{1,0},{2,0},{3,0},
            {2,1},
            {1,2},
            {0,3},{1,3},{2,3},{3,3},
        };
        private const int TopmostTicks = 5;   // 5틱 = 0.2초마다 맨 앞을 다시 주장
        private const double MinSpriteScale = 0.5;  // 도트 하나가 이보다 작아지지는 않는다

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;

        private class Effect
        {
            public string Kind;
            public double X;
            public double Y;
            public double SpeedX;
            public double SpeedY;
            public double Life;
            public Color Tint;
        }

        private readonly List<Effect> effects = new List<Effect>();
        private readonly Dictionary<string, Bitmap>[] poseImages =
            new Dictionary<string, Bitmap>[2];
        private double blinkTimer;
        private double blinking;
        private double landSquash;
        private double breath;
        private double wiggle;
        private readonly PetWorld world;
        private readonly Bitmap[][] images;   // [0] 오른쪽, [1] 왼쪽
        private readonly Timer timer;
        private readonly Random random;
        private readonly int spriteWidth;
        private readonly int spriteHeight;
        private readonly int hop;
        private readonly int dot;
        private readonly int marginX;
        private readonly int marginTop;
        private readonly int windowWidth;
        private readonly int windowHeight;
        private bool napping;
        private double zzzTimer;
        private readonly int frameCount;
        private readonly int maxX;
        private readonly int baseY;


        private double x;
        private double speedValue;
        private int direction;
        private bool walking = true;
        private readonly bool hops;
        private readonly int bouncePixels;
        private string hopState = "rest";
        private double hopTimer;
        private double idleLeft;
        private double animTime;
        private double lift;               // 바닥에서 떠 있는 높이(px)
        private double verticalSpeed;
        private bool dragging;
        private Point dragOffset;
        private Point dragStart;
        private bool dragMoved;
        private int ticks;

        public PetForm(PetWorld world, PokemonSprite sprite)
        {
            this.world = world;
            this.SpriteKey = sprite.Key;
            this.random = world.Random;

            List<Color?[][]> frames = SpriteFactory.Frames(sprite);
            this.frameCount = frames.Count;
            double scale = Math.Max(MinSpriteScale, world.Options.Scale * sprite.ScaleFactor);
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

            Dictionary<string, Color?[][]> poseGrids = SpriteFactory.Poses(sprite);
            this.poseImages[0] = new Dictionary<string, Bitmap>();
            this.poseImages[1] = new Dictionary<string, Bitmap>();
            foreach (KeyValuePair<string, Color?[][]> pair in poseGrids)
            {
                this.poseImages[0][pair.Key] =
                    SpriteFactory.Render(pair.Value, scale, !sprite.FacesRight);
                this.poseImages[1][pair.Key] =
                    SpriteFactory.Render(pair.Value, scale, sprite.FacesRight);
            }

            this.spriteWidth = this.images[0][0].Width;
            this.spriteHeight = this.images[0][0].Height;
            this.hop = Math.Max(1, (int)Math.Round(scale));
            // 먼지나 하트가 몸 밖으로 튀어나갈 자리를 창에 미리 마련해 둔다.
            this.dot = Math.Max(1, (int)Math.Round(scale));
            this.marginX = this.dot * 7;
            this.marginTop = this.dot * 9;
            this.windowWidth = this.spriteWidth + this.marginX * 2;
            this.windowHeight = this.spriteHeight + this.hop + this.marginTop;
            this.hops = sprite.Hops;
            // 프레임에 몸통 움직임이 그려져 있으면 프로그램 쪽 흔들림은 끈다.
            this.bouncePixels = sprite.Bounce ? this.hop : 0;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.ClientSize = new Size(this.windowWidth, this.windowHeight);
            this.Text = sprite.NameKo;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            Rectangle screen = Screen.PrimaryScreen.Bounds;
            // 기본값은 작업 표시줄 "위"에 올라서기. 작업 영역의 아래쪽 선이 곧 표시줄의 윗변이다.
            int ground = world.Options.OnTaskbar
                ? screen.Bottom
                : Screen.PrimaryScreen.WorkingArea.Bottom;
            this.maxX = Math.Max(0, screen.Width - this.windowWidth);
            this.baseY = ground - this.windowHeight - world.Options.Offset;
            this.x = this.random.NextDouble() * this.maxX;
            this.direction = this.random.Next(2) == 0 ? -1 : 1;
            this.speedValue = world.Options.Speed * (0.85 + this.random.NextDouble() * 0.3);
            this.hopTimer = HopRestMin + this.random.NextDouble() * (HopRestMax - HopRestMin);
            this.blinkTimer = BlinkEveryMin + this.random.NextDouble() * (BlinkEveryMax - BlinkEveryMin);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Opening += delegate { this.BuildMenu(menu, world); };
            this.ContextMenuStrip = menu;

            this.MoveToPlace();

            this.timer = new Timer();
            this.timer.Interval = TickMs;
            this.timer.Tick += this.OnTick;
            this.timer.Start();
        }

        /// <summary>우클릭 메뉴. 열 때마다 다시 만들어 지금 상태가 그대로 보이게 한다.</summary>
        private void BuildMenu(ContextMenuStrip menu, PetWorld world)
        {
            menu.Items.Clear();

            ToolStripMenuItem add = new ToolStripMenuItem("포켓몬 추가");
            foreach (PokemonSprite sprite in Sprites.All)
            {
                string key = sprite.Key;
                add.DropDownItems.Add(sprite.NameKo, null, delegate { world.AddAndSave(key); });
            }
            add.DropDownItems.Add(new ToolStripSeparator());
            add.DropDownItems.Add("무작위", null, delegate { world.AddRandom(); });
            menu.Items.Add(add);

            menu.Items.Add("이 포켓몬 보내주기", null, delegate { world.Remove(this); });
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem sizes = new ToolStripMenuItem("크기");
            string[] sizeNames = { "작게", "보통", "크게", "아주 크게" };
            double[] sizeValues = { 3.0, 4.5, 6.0, 9.0 };
            for (int i = 0; i < sizeNames.Length; i++)
            {
                double value = sizeValues[i];
                ToolStripMenuItem item = new ToolStripMenuItem(sizeNames[i], null,
                    delegate { world.SetScale(value); });
                item.Checked = Math.Abs(world.Options.Scale - value) < 0.01;
                sizes.DropDownItems.Add(item);
            }
            menu.Items.Add(sizes);

            ToolStripMenuItem speeds = new ToolStripMenuItem("속도");
            string[] speedNames = { "느리게", "보통", "빠르게" };
            double[] speedValues = { 30.0, 55.0, 95.0 };
            for (int i = 0; i < speedNames.Length; i++)
            {
                double value = speedValues[i];
                ToolStripMenuItem item = new ToolStripMenuItem(speedNames[i], null,
                    delegate { world.SetSpeed(value); });
                item.Checked = Math.Abs(world.Options.Speed - value) < 0.01;
                speeds.DropDownItems.Add(item);
            }
            menu.Items.Add(speeds);

            ToolStripMenuItem pause = new ToolStripMenuItem("잠시 멈춤", null,
                delegate { world.TogglePause(); });
            pause.Checked = world.Paused;
            menu.Items.Add(pause);

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem startup = new ToolStripMenuItem("윈도우 시작할 때 실행", null,
                delegate { AutoStart.Set(!AutoStart.Enabled()); });
            startup.Checked = AutoStart.Enabled();
            menu.Items.Add(startup);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("전부 종료", null, delegate { world.QuitAll(); });
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
            int frame;
            if (this.dragging)
            {
                frame = 0;
            }
            else if (this.hops)
            {
                frame = this.HopFrame();
            }
            else if (this.walking)
            {
                frame = (int)(this.animTime / StepSeconds) % this.frameCount;
            }
            else
            {
                frame = 0;
            }

            // 상황에 맞는 자세가 있으면 그것을, 없으면 평소 프레임을 쓴다.
            int side = this.direction > 0 ? 0 : 1;
            string pose = this.ChoosePose();
            Bitmap image = null;
            if (pose != null)
            {
                this.poseImages[side].TryGetValue(pose, out image);
            }
            if (image == null)
            {
                image = this.images[side][frame];
            }

            // 홀수 프레임에서 살짝 튀어올라 걷는 느낌을 준다.
            // 뛰어다니는 포켓몬은 점프 자체가 움직임이라 흔들지 않는다.
            bool walkingNow = !this.hops && this.walking && !this.dragging;
            int bounce = (walkingNow && image != null && pose == null && frame % 2 == 1)
                ? this.bouncePixels : 0;
            // 들려 있으면 버둥거린다.
            int sway = (this.dragging && (int)(this.wiggle / WiggleSeconds) % 2 == 1)
                ? this.dot : 0;
            e.Graphics.DrawImageUnscaled(
                image, this.marginX + sway, this.marginTop + this.hop - bounce);
            this.PaintEffects(e.Graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !this.IsDisposed)
            {
                // 누른 자리를 기억해 두고 끌기를 시작한다.
                this.dragging = true;
                this.dragMoved = false;
                this.dragStart = Control.MousePosition;
                this.dragOffset = new Point(
                    Control.MousePosition.X - (int)this.x,
                    Control.MousePosition.Y - (this.baseY - (int)this.lift));
                this.verticalSpeed = 0.0;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (this.dragging && !this.IsDisposed)
            {
                Point now = Control.MousePosition;
                if (Math.Abs(now.X - this.dragStart.X) > DragSlack
                    || Math.Abs(now.Y - this.dragStart.Y) > DragSlack)
                {
                    this.dragMoved = true;
                }

                // 바닥(0)과 화면 위쪽 사이로 제한한다. --offset 을 크게 줘서 바닥이
                // 화면 위로 올라가 버린 경우에도 음수가 되지 않도록 천장을 0 이상으로 둔다.
                double ceiling = Math.Max(0.0, (double)this.baseY);
                this.x = Math.Min(Math.Max(0, now.X - this.dragOffset.X), this.maxX);
                double height = this.baseY - (now.Y - this.dragOffset.Y);
                this.lift = Math.Min(Math.Max(0.0, height), ceiling);
                this.MoveToPlace();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && this.dragging)
            {
                // 놓으면 떨어진다. 거의 움직이지 않았으면 클릭으로 보고 폴짝 뛴다.
                this.dragging = false;
                if (this.dragMoved)
                {
                    this.verticalSpeed = 0.0;
                }
                else
                {
                    this.verticalSpeed = JumpSpeed;
                    this.SpawnEmote("heart");
                }
            }
            base.OnMouseUp(e);
        }

        private void OnTick(object sender, EventArgs e)
        {
            double dt = TickMs / 1000.0;
            this.ticks++;

            if (this.dragging)
            {
                // 손에 들려 있는 동안에는 스스로 움직이지 않는다.
                // 다만 다른 창에 가리지 않도록 맨 앞 주장은 계속한다.
                if (this.ticks % TopmostTicks == 0)
                {
                    this.RaiseAboveAll();
                }
                this.Invalidate();
                return;
            }

            if (this.world.Paused)
            {
                // 잠시 멈춤: 제자리에서 가만히
            }
            else if (this.hops)
            {
                this.HopStep(dt);
            }
            else if (this.walking)
            {
                this.animTime += dt;
                this.x += this.direction * this.speedValue * dt;
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
                    if (this.random.NextDouble() < NapChance)
                    {
                        // 가끔은 길게 낮잠을 잔다. 이때 머리 위로 Zzz 가 올라간다.
                        this.idleLeft = 4.0 + this.random.NextDouble() * 5.0;
                        this.napping = true;
                        this.zzzTimer = 0.35;
                    }
                    else
                    {
                        this.idleLeft = 0.8 + this.random.NextDouble() * 2.2;
                    }
                }
            }
            else
            {
                this.idleLeft -= dt;
                if (this.idleLeft <= 0)
                {
                    this.walking = true;
                    this.napping = false;
                }
            }

            // 떠 있으면 중력으로 끌어내린다.
            if (this.lift > 0 || this.verticalSpeed != 0)
            {
                this.verticalSpeed -= Gravity * dt;
                this.lift += this.verticalSpeed * dt;
                if (this.lift <= 0)
                {
                    // 세게 떨어졌으면 발밑에 먼지가 인다.
                    if (-this.verticalSpeed >= LandDustSpeed)
                    {
                        this.SpawnDust();
                        this.landSquash = LandSquashTime;
                    }
                    this.lift = 0.0;
                    this.verticalSpeed = 0.0;
                }
            }

            this.UpdateTimers(dt);
            this.UpdateEffects(dt);
            if (this.napping)
            {
                this.zzzTimer -= dt;
                if (this.zzzTimer <= 0)
                {
                    this.zzzTimer = ZzzEvery;
                    this.SpawnEmote("zzz");
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

        /// <summary>어떤 포켓몬인지(설정 저장에 쓴다).</summary>
        public string SpriteKey { get; private set; }

        /// <summary>가로 위치. 크기를 바꿔 다시 만들 때 자리를 이어받는다.</summary>
        public double Position
        {
            get { return this.x; }
            set { this.x = Math.Min(Math.Max(0, value), this.maxX); this.MoveToPlace(); }
        }

        /// <summary>이동 속도를 바꾼다(메뉴에서 속도를 고쳤을 때).</summary>
        public void SetSpeed(double speed)
        {
            this.speedValue = speed * (0.85 + this.random.NextDouble() * 0.3);
        }

        // --- 효과 ------------------------------------------------------

        /// <summary>착지할 때 발밑에서 먼지가 인다.</summary>
        private void SpawnDust()
        {
            double feetX = this.marginX + this.spriteWidth / 2.0;
            double feetY = this.marginTop + this.hop + this.spriteHeight;
            for (int index = 0; index < 6; index++)
            {
                int side = index % 2 == 0 ? -1 : 1;
                double spread = 0.4 + this.random.NextDouble() * 0.9;
                Effect dust = new Effect();
                dust.Kind = "dust";
                dust.X = feetX + side * this.spriteWidth * 0.18 * spread;
                dust.Y = feetY - this.dot;
                dust.SpeedX = side * (30 + this.random.NextDouble() * 55);
                dust.SpeedY = -(20 + this.random.NextDouble() * 45);
                dust.Life = DustLife * (0.7 + this.random.NextDouble() * 0.6);
                dust.Tint = index % 2 == 1
                    ? Color.FromArgb(242, 242, 242)
                    : Color.FromArgb(192, 192, 192);
                this.effects.Add(dust);
            }
        }

        /// <summary>머리 위로 하트나 Zzz 를 띄운다.</summary>
        private void SpawnEmote(string kind)
        {
            Effect emote = new Effect();
            emote.Kind = kind;
            emote.X = this.marginX + this.spriteWidth * (0.55 + this.random.NextDouble() * 0.2);
            emote.Y = this.marginTop;
            emote.SpeedX = 8 + this.random.NextDouble() * 10;
            emote.SpeedY = -28.0;
            emote.Life = EmoteLife;
            emote.Tint = kind == "heart"
                ? Color.FromArgb(255, 95, 131)
                : Color.FromArgb(255, 255, 255);
            this.effects.Add(emote);
        }

        /// <summary>눈 깜빡임, 착지 눌림, 숨쉬기, 버둥거림 박자를 센다.</summary>
        private void UpdateTimers(double dt)
        {
            if (this.landSquash > 0)
            {
                this.landSquash -= dt;
            }
            this.wiggle = this.dragging ? this.wiggle + dt : 0.0;
            this.breath = this.napping ? this.breath + dt : 0.0;

            if (this.blinking > 0)
            {
                this.blinking -= dt;
            }
            else if (this.lift <= 0 && !this.dragging)
            {
                this.blinkTimer -= dt;
                if (this.blinkTimer <= 0)
                {
                    this.blinking = BlinkTime;
                    this.blinkTimer = BlinkEveryMin
                        + this.random.NextDouble() * (BlinkEveryMax - BlinkEveryMin);
                }
            }
        }

        /// <summary>지금 상황에 맞는 자세 이름. 없으면 null(평소 프레임).</summary>
        private string ChoosePose()
        {
            if (this.dragging)
            {
                return null;
            }
            if (this.lift > this.dot)
            {
                return "stretch";
            }
            if (this.landSquash > 0)
            {
                return "squash";
            }
            if (this.napping && (int)(this.breath / BreathSeconds) % 2 == 1)
            {
                return "squash";
            }
            if (this.blinking > 0)
            {
                return "blink";
            }
            return null;
        }

        private void UpdateEffects(double dt)
        {
            for (int index = this.effects.Count - 1; index >= 0; index--)
            {
                Effect effect = this.effects[index];
                effect.Life -= dt;
                if (effect.Life <= 0)
                {
                    this.effects.RemoveAt(index);
                    continue;
                }
                effect.X += effect.SpeedX * dt;
                effect.Y += effect.SpeedY * dt;
                if (effect.Kind == "dust")
                {
                    effect.SpeedY += EffectGravity * dt;
                }
            }
        }

        /// <summary>효과를 사각형으로 찍는다.</summary>
        private void PaintEffects(Graphics graphics)
        {
            foreach (Effect effect in this.effects)
            {
                using (SolidBrush brush = new SolidBrush(effect.Tint))
                {
                    if (effect.Kind == "dust")
                    {
                        // 사라질수록 작아진다
                        int size = Math.Max(1, (int)(this.dot * (0.6 + 0.8 * effect.Life / DustLife)));
                        graphics.FillRectangle(brush, (int)effect.X, (int)effect.Y, size, size);
                        continue;
                    }

                    // 절반쯤 남으면 깜빡이며 사라진다
                    if (effect.Life < EmoteLife * 0.35 && (int)(effect.Life * 20) % 2 == 0)
                    {
                        continue;
                    }
                    int[,] dots = effect.Kind == "heart" ? HeartDots : ZzzDots;
                    for (int row = 0; row < dots.GetLength(0); row++)
                    {
                        graphics.FillRectangle(
                            brush,
                            (int)effect.X + dots[row, 0] * this.dot,
                            (int)effect.Y + dots[row, 1] * this.dot,
                            this.dot, this.dot);
                    }
                }
            }
        }

        /// <summary>메타몽처럼 폴짝폴짝 뛰어서 이동한다.
        ///
        /// 웅크렸다가(crouch) 튀어올라(air) 앞으로 나아가고, 착지해서 납작해졌다가
        /// (land) 잠시 쉰 뒤(rest) 다시 뛴다. 공중에 있는 동안에만 앞으로 간다.</summary>
        private void HopStep(double dt)
        {
            if (this.lift > 0)
            {
                this.hopState = "air";
                this.x += this.direction * this.speedValue * HopBoost * dt;
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
                return;
            }

            if (this.hopState == "air")          // 방금 착지했다
            {
                this.hopState = "land";
                this.hopTimer = HopLandSeconds;
                return;
            }

            this.hopTimer -= dt;
            if (this.hopTimer > 0)
            {
                return;
            }

            if (this.hopState == "land")
            {
                this.hopState = "rest";
                this.hopTimer = HopRestMin + this.random.NextDouble() * (HopRestMax - HopRestMin);
            this.blinkTimer = BlinkEveryMin + this.random.NextDouble() * (BlinkEveryMax - BlinkEveryMin);
                if (this.random.NextDouble() < HopTurnChance)
                {
                    this.direction = -this.direction;
                }
            }
            else if (this.hopState == "rest")
            {
                this.hopState = "crouch";
                this.hopTimer = HopCrouchSeconds;
            }
            else                                  // crouch
            {
                this.verticalSpeed = HopSpeed;
                this.hopState = "air";
            }
        }

        /// <summary>[평소, 웅크림, 늘어남] 중 지금 상태에 맞는 프레임.</summary>
        private int HopFrame()
        {
            int index;
            if (this.hopState == "air")
            {
                index = 2;
            }
            else if (this.hopState == "crouch" || this.hopState == "land")
            {
                index = 1;
            }
            else
            {
                index = 0;
            }
            return Math.Min(index, this.frameCount - 1);
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
            this.Location = new Point((int)this.x, this.baseY - (int)this.lift);
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
        private bool rebuilding;
        public bool Paused;

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
            this.AddAndSave(Sprites.All[this.Random.Next(Sprites.All.Count)].Key);
        }

        /// <summary>포켓몬을 한 마리 늘리고 설정에 남긴다.</summary>
        public void AddAndSave(string key)
        {
            this.Add(key);
            this.SaveSettings();
        }

        /// <summary>지금 구성을 설정 파일에 남긴다.</summary>
        public void SaveSettings()
        {
            List<string> species = new List<string>();
            foreach (PetForm pet in this.pets)
            {
                species.Add(pet.SpriteKey);
            }
            if (species.Count == 0)
            {
                species.Add("pikachu");
            }
            SettingsFile.Save(this.Options, species);
        }

        /// <summary>크기를 바꾸고 지금 있는 포켓몬을 그대로 다시 만든다.</summary>
        public void SetScale(double scale)
        {
            if (Math.Abs(this.Options.Scale - scale) < 0.001)
            {
                return;
            }
            this.Options.Scale = scale;
            this.Rebuild();
        }

        public void SetSpeed(double speed)
        {
            this.Options.Speed = speed;
            foreach (PetForm pet in this.pets)
            {
                pet.SetSpeed(speed);
            }
            this.SaveSettings();
        }

        public void TogglePause()
        {
            this.Paused = !this.Paused;
        }

        /// <summary>포켓몬을 모두 지웠다가 같은 구성으로 다시 만든다.</summary>
        private void Rebuild()
        {
            List<string> keys = new List<string>();
            List<double> places = new List<double>();
            foreach (PetForm pet in this.pets)
            {
                keys.Add(pet.SpriteKey);
                places.Add(pet.Position);
            }

            this.rebuilding = true;
            foreach (PetForm pet in this.pets.ToArray())
            {
                pet.Close();
            }
            this.pets.Clear();
            this.rebuilding = false;

            for (int i = 0; i < keys.Count; i++)
            {
                this.Add(keys[i]);
                this.pets[this.pets.Count - 1].Position = places[i];
            }
            this.SaveSettings();
        }

        public void Remove(PetForm pet)
        {
            pet.Close();
        }

        private void Forget(PetForm pet)
        {
            this.pets.Remove(pet);
            if (this.pets.Count == 0 && !this.quitting && !this.rebuilding)
            {
                this.ExitThread();
            }
            else if (!this.quitting && !this.rebuilding)
            {
                this.SaveSettings();
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
            "  -c, --count <수>       마리 수 (기본: 지난번 그대로)\n" +
            "      --settings <파일>  설정 파일 경로\n" +
            "  -s, --scale <배율>     크기 배율 (기본 4.5, 3 이면 예전 크기)\n" +
            "      --speed <속도>     이동 속도, 초당 픽셀 (기본 55)\n" +
            "      --offset <픽셀>    바닥에서 더 띄울 높이 (기본 0)\n" +
            "      --on-taskbar       작업 표시줄 위에 올라서지 않고 표시줄 위를 걷는다\n" +
            "      --list             포켓몬 목록 보기\n\n" +
            "포켓몬을 왼쪽 클릭하면 점프합니다.\n" +
            "누른 채로 끌면 원하는 자리로 옮길 수 있고, 놓으면 바닥으로 떨어집니다.\n" +
            "오른쪽 클릭하면 메뉴가 열립니다.";

        public static Options Parse(string[] argv)
        {
            Options options = new Options();
            HashSet<string> given = new HashSet<string>();
            for (int i = 0; i < argv.Length; i++)
            {
                string arg = argv[i];
                bool needsValue = arg == "-p" || arg == "--pokemon" || arg == "-c" || arg == "--count"
                    || arg == "-s" || arg == "--scale" || arg == "--speed" || arg == "--offset"
                    || arg == "--settings";
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
                        given.Add("species");
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
                        if (!double.TryParse(argv[++i], out options.Scale) || options.Scale <= 0)
                        {
                            options.Error = "--scale 은 0보다 큰 숫자여야 합니다.";
                            return options;
                        }
                        given.Add("scale");
                        break;
                    case "--speed":
                        if (!double.TryParse(argv[++i], out options.Speed) || options.Speed <= 0)
                        {
                            options.Error = "--speed 는 0보다 큰 숫자여야 합니다.";
                            return options;
                        }
                        given.Add("speed");
                        break;
                    case "--offset":
                        if (!int.TryParse(argv[++i], out options.Offset))
                        {
                            options.Error = "--offset 은 숫자여야 합니다.";
                            return options;
                        }
                        given.Add("offset");
                        break;
                    case "--on-taskbar":
                        options.OnTaskbar = true;
                        given.Add("on_taskbar");
                        break;
                    case "--settings":
                        options.SettingsPath = argv[++i];
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

            // 명령줄로 정하지 않은 항목은 저장된 설정에서 가져온다.
            SettingsFile.Load(options, given);
            options.SpeciesFromCommandLine = given.Contains("species");

            if (options.Species.Count == 0)
            {
                options.Species.Add("pikachu");
            }
            Random random = new Random();
            while (options.Species.Count < options.Count)
            {
                options.Species.Add(Sprites.All[random.Next(Sprites.All.Count)].Key);
            }
            if (options.Count > 0 && options.Species.Count > options.Count)
            {
                options.Species = options.Species.GetRange(0, options.Count);
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
