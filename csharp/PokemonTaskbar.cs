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
        public int Coins = 3000;
        public int Food = 0;
        public int GrowthDrops = 0;
        public int[] StockPrices = { 1000, 1800, 2700 };
        public int[] StockShares = { 0, 0, 0 };
        public string SettingsPath = null;
        public bool SpeciesFromCommandLine = false;
        public bool ShowList = false;
        public bool ShowWhere = false;
        public bool ShowCheck = false;
        public bool ShowHelp = false;
        public string Error = null;
    }

    /// <summary>사용자 설정을 파일에 저장하고 불러온다.
    ///
    /// 파이썬 판과 같은 파일을 읽고 쓰므로 형식(한 줄에 `이름 = 값`)과
    /// 숫자 표기(InvariantCulture)를 맞춰 둔다.</summary>
    /// <summary>시작 과정을 파일로 남긴다. 창이 안 떠도 이유가 남는다.</summary>
    public static class Log
    {
        private static string path;

        /// <summary>로그 파일 위치. 설정 파일과 같은 폴더에 둔다.</summary>
        public static string Path
        {
            get
            {
                if (path == null)
                {
                    try
                    {
                        string appdata = Environment.GetFolderPath(
                            Environment.SpecialFolder.ApplicationData);
                        string folder = System.IO.Path.Combine(appdata, "PokemonTaskbar");
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }
                        path = System.IO.Path.Combine(folder, "startup.log");
                    }
                    catch (Exception)
                    {
                        // 쓸 곳이 없으면 exe 옆에라도 남긴다.
                        try
                        {
                            path = System.IO.Path.Combine(
                                System.IO.Path.GetDirectoryName(Application.ExecutablePath),
                                "startup.log");
                        }
                        catch (Exception)
                        {
                            path = "startup.log";
                        }
                    }
                }
                return path;
            }
        }

        /// <summary>실행할 때마다 이어 쓴다. 너무 커지면 처음부터 다시 쓴다.</summary>
        public static void Begin()
        {
            try
            {
                FileInfo info = new FileInfo(Path);
                if (info.Exists && info.Length > 200000)
                {
                    File.WriteAllText(Path, "", new System.Text.UTF8Encoding(false));
                }
            }
            catch (Exception)
            {
            }
            Write("");
            Write("=========================================================");
            Write("시작: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>절대 예외를 밖으로 내보내지 않는다.</summary>
        public static void Write(string line)
        {
            try
            {
                File.AppendAllText(Path, line + Environment.NewLine,
                    new System.Text.UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        public static void Fail(string where, Exception error)
        {
            Write("!! 실패 [" + where + "]");
            try
            {
                Write(error == null ? "  (예외 정보 없음)" : error.ToString());
            }
            catch (Exception)
            {
                // ToString 자체가 실패하는 예외도 있다. 그럴 때는 형식 이름만이라도.
                try
                {
                    Write("  " + error.GetType().FullName);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    public static class SettingsFile
    {
        public const string EnvOverride = "POKEMON_TASKBAR_SETTINGS";
        public const int CurrencyVersion = 2;
        public const int CurrencyScale = 100;

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

        private static int[] ParseStockValues(string value, bool requirePositive)
        {
            string[] parts = value.Split(',');
            if (parts.Length != 3)
            {
                return null;
            }
            int[] numbers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out numbers[i])
                    || numbers[i] < 0 || (requirePositive && numbers[i] == 0))
                {
                    return null;
                }
            }
            return numbers;
        }

        private static int ScaleLegacyMoney(int amount)
        {
            return amount > int.MaxValue / CurrencyScale
                ? int.MaxValue : amount * CurrencyScale;
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

            int storedCurrencyVersion = 1;
            bool sawCoins = false;
            bool sawStockPrices = false;

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
                    case "coins":
                        if (int.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out whole) && whole >= 0)
                        {
                            options.Coins = whole;
                            sawCoins = true;
                        }
                        break;
                    case "food":
                        if (int.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out whole) && whole >= 0)
                        {
                            options.Food = whole;
                        }
                        break;
                    case "growth_drops":
                        if (int.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out whole) && whole >= 0)
                        {
                            options.GrowthDrops = whole;
                        }
                        break;
                    case "stock_prices":
                        int[] prices = ParseStockValues(value, true);
                        if (prices != null)
                        {
                            options.StockPrices = prices;
                            sawStockPrices = true;
                        }
                        break;
                    case "stock_shares":
                        int[] shares = ParseStockValues(value, false);
                        if (shares != null)
                        {
                            options.StockShares = shares;
                        }
                        break;
                    case "currency_version":
                        if (int.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out whole) && whole > 0)
                        {
                            storedCurrencyVersion = whole;
                        }
                        break;
                }
            }
            if (storedCurrencyVersion < CurrencyVersion)
            {
                if (sawCoins)
                {
                    options.Coins = ScaleLegacyMoney(options.Coins);
                }
                if (sawStockPrices)
                {
                    for (int i = 0; i < options.StockPrices.Length; i++)
                    {
                        options.StockPrices[i] = ScaleLegacyMoney(options.StockPrices[i]);
                    }
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
                lines.Add("coins = " + options.Coins.ToString(CultureInfo.InvariantCulture));
                lines.Add("food = " + options.Food.ToString(CultureInfo.InvariantCulture));
                lines.Add("growth_drops = " + options.GrowthDrops.ToString(CultureInfo.InvariantCulture));
                lines.Add("currency_version = " + CurrencyVersion.ToString(CultureInfo.InvariantCulture));
                lines.Add("stock_prices = " + string.Join(", ", Array.ConvertAll(
                    options.StockPrices, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
                lines.Add("stock_shares = " + string.Join(", ", Array.ConvertAll(
                    options.StockShares, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
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

        public const int WalkBodySize = 1;

        /// <summary>발걸음마다 몸 전체가 눌리고 늘어나는 걷기 프레임을 만든다.
        ///
        /// 디딤(0/2)에서는 몸통·귀·꼬리까지 낮고 넓게 눌리고, 발을 든
        /// 프레임(1/3)에서는 전체 실루엣이 길고 가늘게 늘어난다.</summary>
        public static List<Color?[][]> WholeWalkFrames(List<Color?[][]> frames)
        {
            List<Color?[][]> shaped = new List<Color?[][]>();
            int width = 0;
            int height = 0;
            for (int index = 0; index < frames.Count; index++)
            {
                Color?[][] frame = frames[index];
                int size = index % 2 == 0 ? WalkBodySize : -WalkBodySize;
                Color?[][] changed = ResampleGrid(frame,
                    Math.Max(1, frame[0].Length + size),
                    Math.Max(1, frame.Length - size));
                shaped.Add(changed);
                width = Math.Max(width, changed[0].Length);
                height = Math.Max(height, changed.Length);
            }

            List<Color?[][]> whole = new List<Color?[][]>();
            foreach (Color?[][] frame in shaped)
            {
                whole.Add(PadOnGround(frame, width, height));
            }
            return whole;
        }

        /// <summary>도트 격자 전체를 최근접 이웃으로 늘리거나 줄인다.</summary>
        private static Color?[][] ResampleGrid(Color?[][] grid, int width, int height)
        {
            Color?[][] changed = new Color?[height][];
            for (int y = 0; y < height; y++)
            {
                changed[y] = new Color?[width];
                int sourceY = Math.Min(grid.Length - 1, y * grid.Length / height);
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Math.Min(grid[0].Length - 1, x * grid[0].Length / width);
                    changed[y][x] = grid[sourceY][sourceX];
                }
            }
            return changed;
        }

        /// <summary>크기가 달라진 그림을 가운데·아래에 맞춘 같은 캔버스에 놓는다.</summary>
        public static Color?[][] PadOnGround(Color?[][] grid, int width, int height)
        {
            Color?[][] padded = new Color?[height][];
            int top = height - grid.Length;
            int left = (width - grid[0].Length) / 2;
            for (int y = 0; y < height; y++)
            {
                padded[y] = new Color?[width];
            }
            for (int y = 0; y < grid.Length; y++)
            {
                Array.Copy(grid[y], 0, padded[top + y], left, grid[y].Length);
            }
            return padded;
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
        // 걷는 포켓몬은 가끔 제자리에서 두 번 폴짝 뛰며 장난을 친다.
        private const double PlayChance = 0.28;
        private const int PlayHops = 2;
        private const double PlayHopSpeed = 145.0;
        private const double PlayWaitSeconds = 0.12;
        private const double PlayTurnChance = 0.45;
        // 공중에 떠다니는 포켓몬(뮤). 바닥을 딛지 않는다.
        private const double FloatHeightMin = 26.0;   // 바닥에서 떠 있는 높이 범위(px)
        private const double FloatHeightMax = 120.0;
        private const double FloatRetargetMin = 1.6;  // 이 간격으로 높이를 새로 고른다
        private const double FloatRetargetMax = 4.5;
        private const double FloatEase = 1.6;         // 새 높이로 옮겨 가는 빠르기
        private const double FloatBobSeconds = 2.2;   // 위아래로 살랑거리는 한 주기
        private const double FloatBobDots = 1.5;      // 살랑거리는 폭(도트 단위)
        private const double FloatSpeed = 0.7;        // 걷는 포켓몬보다 느긋하게
        private const double FloatStepSeconds = 0.30; // 프레임 넘기는 간격
        private const double FloatTurnChance = 0.003;
        private const double FloatStopChance = 0.004;
        private const double FloatNudge = 30.0;       // 쓰다듬으면 이만큼 위로

        // 진화. 함께 걸은 거리와 쓰다듬은 횟수를 채운 뒤, 메뉴에서 직접 진화한다.
        //
        // 시간이 흘렀다고 저절로 진화하지는 않는다. 아끼던 모습이 예고 없이
        // 바뀌면 곤란하므로, 진화할지 말지는 쓰다듬는 사람이 정한다.
        private const double EvolvePetNeed = 8.0;      // 이만큼 쓰다듬으면 친밀도 조건을 채운다
        private const double EvolvePerPet = 1.0;       // 한 번 쓰다듬을 때마다
        private const double EvolveWalkNeed = 600.0;   // 이만큼 걸으면 산책 조건을 채운다(px)
        private const int EvolveFlashes = 7;           // 두 모습을 번갈아 번쩍이는 횟수
        private const double EvolveFirstSeconds = 0.30;
        private const double EvolveLastSeconds = 0.07; // 갈수록 빨라진다
        private const double EvolveHoldSeconds = 0.55; // 끝에 새하얗게 머무는 시간
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
        private const double IdleActionChance = 0.55;
        private const double IdleActionMinSeconds = 0.9;
        private const double IdleActionMaxSeconds = 1.6;
        private const double IdleEffectEvery = 0.55;
        public const double GreetingDistance = 150.0;
        private const double GreetingSeconds = 1.15;
        private const double GreetingCooldown = 5.0;
        private const double GreetingTalkEvery = 0.34;
        // 걸음 프레임은 시간 대신 실제 이동 거리에 맞춘다. 속도가 바뀌어도 발이 미끄러지지 않는다.
        private const double WalkStride = 35.0;     // 4프레임 한 바퀴에 나아가는 거리(px)
        private const double WalkAccel = 220.0;     // 걷기 시작할 때 속도를 올리는 가속도
        private const double WalkDecel = 420.0;     // 멈추거나 돌아설 때 속도를 줄이는 감속도
        private const double TurnPauseSeconds = 0.12; // 멈춰 몸을 낮춘 채 방향을 바꾸는 시간
        private const int WalkSubsteps = 8;         // 4장 도트를 더 부드럽게 보이게 나눈 보행 박자
        private static readonly double[] WalkBob = { 0.0, 0.45, 1.0, 0.45, 0.0, 0.45, 1.0, 0.45 };

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
        private static readonly int[,] SparkDots = { {1,0},{1,1},{0,1},{2,1},{1,2} };
        private static readonly int[,] FlameDots = { {1,0},{0,1},{1,1},{2,1},{0,2},{1,2} };
        private static readonly int[,] LeafDots = { {1,0},{2,0},{0,1},{1,1},{2,1},{1,2} };
        private static readonly int[,] BubbleDots = { {0,0},{1,0},{0,1},{1,1} };
        private static readonly int[,] TwinkleDots = { {1,0},{0,1},{1,1},{2,1},{1,2} };
        private static readonly int[,] TalkDots = {
            {1,0},{2,0},{3,0},
            {0,1},{4,1},
            {0,2},{4,2},
            {1,3},{2,3},{3,3},
            {1,4},
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
        private double walkSpeed;
        private double gaitDistance;
        private int direction;
        private bool walking = true;
        private string stopKind;
        private double turnLeft;
        private int turnDirection;
        private readonly bool hops;
        private readonly bool floats;
        private readonly string nextKey;      // 진화하면 무엇이 되는지
        private double friendship;
        private double walked;                 // 스스로 걸은 거리(px). 끌어다 놓은 거리는 세지 않는다.
        private bool evolving;
        private int evolveStep;
        private double evolveTimer;
        private Bitmap[][] whiteImages;       // [모습][방향] 하얀 실루엣
        private int[] whiteOffsetX;
        private int[] whiteOffsetY;
        private readonly int ownWidth;
        private readonly int ownHeight;
        private readonly int ownOffsetX;
        private readonly int ownOffsetY;
        private ToolStripMenuItem evolveItem;
        private double floatBase;
        private double floatTarget;
        private double floatTimer;
        private double floatPhase;
        private readonly int bouncePixels;
        private string hopState = "rest";
        private double hopTimer;
        private double idleLeft;
        private string idleAction;
        private double idleActionLeft;
        private double idleEffectLeft;
        private double idlePhase;
        private double greetingLeft;
        private double greetingPhase;
        private double greetingCooldown;
        private bool greetingLeads;
        private int greetingTalkTurn = -1;
        private string playState;
        private double playLeft;
        private int playHops;
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
            if (!sprite.Hops && !sprite.Floats)
            {
                frames = SpriteFactory.WholeWalkFrames(frames);
            }
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
            if (!sprite.Hops && !sprite.Floats)
            {
                int frameWidth = frames[0][0].Length;
                int frameHeight = frames[0].Length;
                foreach (string name in new List<string>(poseGrids.Keys))
                {
                    poseGrids[name] = SpriteFactory.PadOnGround(
                        poseGrids[name], frameWidth, frameHeight);
                }
            }
            this.poseImages[0] = new Dictionary<string, Bitmap>();
            this.poseImages[1] = new Dictionary<string, Bitmap>();
            foreach (KeyValuePair<string, Color?[][]> pair in poseGrids)
            {
                this.poseImages[0][pair.Key] =
                    SpriteFactory.Render(pair.Value, scale, !sprite.FacesRight);
                this.poseImages[1][pair.Key] =
                    SpriteFactory.Render(pair.Value, scale, sprite.FacesRight);
            }

            this.ownWidth = this.images[0][0].Width;
            this.ownHeight = this.images[0][0].Height;
            // 진화하면 몸집이 달라진다. 번쩍이는 동안 잘리지 않도록 두 모습이
            // 모두 들어갈 크기로 창을 잡아 둔다. 그림은 아래쪽에 맞춰 그리므로
            // 창이 커져도 발은 바닥에 그대로 붙어 있다.
            this.nextKey = sprite.EvolvesTo;
            this.spriteWidth = this.ownWidth;
            this.spriteHeight = this.ownHeight;
            if (this.nextKey != null)
            {
                // 굳이 그려 보지 않고 크기만 같은 규칙으로 계산한다.
                PokemonSprite after = Sprites.Find(this.nextKey);
                Color?[][] afterFrame = SpriteFactory.Frames(after)[0];
                double afterScale = Math.Max(MinSpriteScale,
                    world.Options.Scale * after.ScaleFactor);
                this.spriteWidth = Math.Max(this.spriteWidth, Math.Max(1,
                    (int)Math.Floor(afterFrame[0].Length * afterScale + 0.5)));
                this.spriteHeight = Math.Max(this.spriteHeight, Math.Max(1,
                    (int)Math.Floor(afterFrame.Length * afterScale + 0.5)));
            }
            this.ownOffsetX = (this.spriteWidth - this.ownWidth) / 2;
            this.ownOffsetY = this.spriteHeight - this.ownHeight;
            this.hop = Math.Max(1, (int)Math.Round(scale));
            // 먼지나 하트가 몸 밖으로 튀어나갈 자리를 창에 미리 마련해 둔다.
            this.dot = Math.Max(1, (int)Math.Round(scale));
            this.marginX = this.dot * 7;
            this.marginTop = this.dot * 9;
            this.windowWidth = this.spriteWidth + this.marginX * 2;
            this.windowHeight = this.spriteHeight + this.hop + this.marginTop;
            this.hops = sprite.Hops;
            this.floats = sprite.Floats;
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
            // 어떤 이유로든 화면 밖으로 나가지 않도록 붙잡아 둔다.
            int wanted = ground - this.windowHeight - world.Options.Offset;
            int lowest = screen.Bottom - this.windowHeight;
            this.baseY = Math.Max(screen.Top, Math.Min(wanted, lowest));
            this.x = this.random.NextDouble() * this.maxX;
            this.direction = this.random.Next(2) == 0 ? -1 : 1;
            this.turnDirection = this.direction;
            this.speedValue = world.Options.Speed * (0.85 + this.random.NextDouble() * 0.3);
            this.hopTimer = HopRestMin + this.random.NextDouble() * (HopRestMax - HopRestMin);
            this.floatBase = this.PickFloatHeight();
            this.floatTarget = this.floatBase;
            this.floatTimer = FloatRetargetMin
                + this.random.NextDouble() * (FloatRetargetMax - FloatRetargetMin);
            this.floatPhase = this.random.NextDouble() * FloatBobSeconds;
            // 떠다니는 포켓몬은 처음부터 공중에 있다.
            this.lift = this.floats ? this.floatBase : 0.0;
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
            // 진화해야 만날 수 있는 포켓몬은 목록에 넣지 않는다.
            foreach (PokemonSprite sprite in Sprites.BaseSpecies())
            {
                string key = sprite.Key;
                add.DropDownItems.Add(sprite.NameKo, null, delegate { world.AddAndSave(key); });
            }
            add.DropDownItems.Add(new ToolStripSeparator());
            add.DropDownItems.Add("무작위", null, delegate { world.AddRandom(); });
            menu.Items.Add(add);

            menu.Items.Add("이 포켓몬 보내주기", null, delegate { world.Remove(this); });

            // 먹이와 진화 아이템은 모두가 공유한다. 메뉴를 열 때마다 수량을 새로 만든다.
            ToolStripMenuItem shop = new ToolStripMenuItem(
                string.Format("상점 (보유 {0})", PetWorld.FormatWon(world.Options.Coins)));
            ToolStripMenuItem buyFood = new ToolStripMenuItem(
                string.Format("포켓푸드 구매 — {0}", PetWorld.FormatWon(PetWorld.FoodCost)), null,
                delegate { world.BuyFood(); });
            buyFood.Enabled = world.Options.Coins >= PetWorld.FoodCost;
            shop.DropDownItems.Add(buyFood);
            ToolStripMenuItem buyGrowthDrop = new ToolStripMenuItem(
                string.Format("성장의 물방울 구매 — {0}", PetWorld.FormatWon(PetWorld.GrowthDropCost)), null,
                delegate { world.BuyGrowthDrop(); });
            buyGrowthDrop.Enabled = world.Options.Coins >= PetWorld.GrowthDropCost;
            shop.DropDownItems.Add(buyGrowthDrop);
            menu.Items.Add(shop);

            ToolStripMenuItem feed = new ToolStripMenuItem(
                string.Format("포켓푸드 주기 ({0}개)", world.Options.Food), null,
                delegate { world.Feed(this); });
            feed.Enabled = world.Options.Food > 0 && !this.evolving;
            menu.Items.Add(feed);

            ToolStripMenuItem market = new ToolStripMenuItem("주식시장");
            ToolStripMenuItem marketInfo = new ToolStripMenuItem("20초마다 가격 변동");
            marketInfo.Enabled = false;
            market.DropDownItems.Add(marketInfo);
            for (int i = 0; i < PetWorld.StockNames.Length; i++)
            {
                int index = i;
                string name = PetWorld.StockNames[index];
                int price = world.Options.StockPrices[index];
                int shares = world.Options.StockShares[index];
                ToolStripMenuItem buy = new ToolStripMenuItem(
                    string.Format("{0} 1주 매수 — {1} (보유 {2}주)", name,
                        PetWorld.FormatWon(price), shares), null,
                    delegate { world.BuyStock(index); });
                buy.Enabled = world.Options.Coins >= price;
                market.DropDownItems.Add(buy);
                ToolStripMenuItem sell = new ToolStripMenuItem(
                    string.Format("{0} 1주 매도 — {1} (보유 {2}주)", name,
                        PetWorld.FormatWon(price), shares), null,
                    delegate { world.SellStock(index); });
                sell.Enabled = shares > 0;
                market.DropDownItems.Add(sell);
            }
            menu.Items.Add(market);

            // 진화하는 포켓몬이면 여기에 진행 상황을 보여 준다.
            if (this.nextKey != null)
            {
                string name = Sprites.Find(this.nextKey).NameKo;
                this.evolveItem = new ToolStripMenuItem();
                if (this.evolving)
                {
                    this.evolveItem.Enabled = false;
                    this.evolveItem.Text = "진화하는 중...";
                }
                else if (this.CanEvolve())
                {
                    this.evolveItem.Text = string.Format("{0}로 진화하기", name);
                    this.evolveItem.Click += delegate { this.StartEvolving(); };
                }
                else
                {
                    List<string> needs = new List<string>();
                    if (this.PetsLeft() > 0)
                    {
                        needs.Add(string.Format("{0}번 더 쓰다듬기", this.PetsLeft()));
                    }
                    if (this.WalkLeft() > 0)
                    {
                        needs.Add(string.Format("{0}px 더 산책", this.WalkLeft()));
                    }
                    if (world.Options.GrowthDrops <= 0)
                    {
                        needs.Add("성장의 물방울 1개");
                    }
                    this.evolveItem.Enabled = false;
                    this.evolveItem.Text = string.Format("{0}까지 {1}", name,
                        string.Join(" · ", needs.ToArray()));
                }
                menu.Items.Add(this.evolveItem);
            }
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
            if (this.evolving)
            {
                this.PaintEvolving(e.Graphics);
                return;
            }

            int frame;
            if (this.dragging)
            {
                frame = 0;
            }
            else if (this.hops)
            {
                frame = this.HopFrame();
            }
            else if (this.floats)
            {
                frame = (int)(this.animTime / FloatStepSeconds) % this.frameCount;
            }
            else if (this.walking || this.stopKind != null)
            {
                frame = this.WalkFrame();
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
            bool walkingNow = !this.hops && !this.floats
                && (this.walking || this.stopKind != null) && !this.dragging;
            int bounce = (walkingNow && image != null && pose == null)
                ? this.WalkBobPixels() : 0;
            // 들려 있으면 버둥거린다.
            int sway = (this.dragging && (int)(this.wiggle / WiggleSeconds) % 2 == 1)
                ? this.dot : 0;
            if (this.idleAction == "wiggle" && (int)(this.idlePhase / WiggleSeconds) % 2 == 1)
            {
                sway += this.dot;
            }
            int greetBob = this.GreetingSpeaking()
                ? (int)Math.Floor(this.dot * 0.45) : 0;
            e.Graphics.DrawImageUnscaled(
                image,
                this.marginX + this.ownOffsetX + sway,
                this.marginTop + this.hop + this.ownOffsetY - bounce - greetBob);
            this.PaintEffects(e.Graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (this.evolving)
            {
                return;              // 진화하는 동안에는 건드릴 수 없다
            }
            if (e.Button == MouseButtons.Left && !this.IsDisposed)
            {
                // 누른 자리를 기억해 두고 끌기를 시작한다.
                // 낮잠이나 장난 중에도 손에 들면 바로 평소 상태로 돌아온다.
                if (!this.hops && !this.floats)
                {
                    this.walking = true;
                    this.napping = false;
                    this.playState = null;
                    this.stopKind = null;
                    this.turnLeft = 0.0;
                    this.walkSpeed = 0.0;
                }
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
                double ceiling = this.Ceiling();
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
                if (this.floats)
                {
                    // 떠다니는 포켓몬은 떨어지지 않는다. 놓은 자리에서 이어서 떠 있다가
                    // 스스로 제 높이로 돌아간다.
                    this.floatBase = this.lift;
                    this.floatPhase = 0.0;
                    if (this.dragMoved)
                    {
                        this.floatTarget = this.PickFloatHeight();
                        this.floatTimer = FloatRetargetMin
                            + this.random.NextDouble() * (FloatRetargetMax - FloatRetargetMin);
                    }
                    else
                    {
                        // 쓰다듬으면 기분 좋게 조금 더 떠오른다.
                        this.floatTarget = Math.Min(this.lift + FloatNudge, this.Ceiling());
                        this.floatTimer = Math.Max(this.floatTimer, 1.2);
                        this.Petted();
                    }
                }
                else if (this.dragMoved)
                {
                    this.verticalSpeed = 0.0;
                }
                else
                {
                    this.verticalSpeed = JumpSpeed;
                    this.Petted();
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

            if (this.evolving)
            {
                // 진화하는 동안에는 제자리에서 번쩍이기만 한다.
                if (this.EvolveTick(dt))
                {
                    this.world.FinishEvolving(this);
                    return;
                }
            }
            else if (this.world.Paused)
            {
                // 잠시 멈춤: 제자리에서 가만히
            }
            else if (this.greetingLeft > 0)
            {
                this.GreetingStep(dt);
            }
            else if (this.world.TryStartGreeting(this))
            {
                this.GreetingStep(dt);
            }
            else if (this.hops)
            {
                this.HopStep(dt);
            }
            else if (this.floats)
            {
                this.FloatStep(dt);
            }
            else if (this.stopKind != null)
            {
                this.SlowStopStep(dt);
            }
            else if (this.turnLeft > 0)
            {
                this.TurnStep(dt);
            }
            else if (this.playState != null)
            {
                this.PlayStep(dt);
            }
            else if (this.walking)
            {
                this.WalkStep(dt);
            }
            else
            {
                this.idleLeft -= dt;
                if (this.idleLeft <= 0)
                {
                    this.walking = true;
                    this.napping = false;
                    this.walkSpeed = 0.0;
                }
            }

            // 떠 있으면 중력으로 끌어내린다. 떠다니는 포켓몬은 예외다.
            if (!this.evolving && !this.floats
                && (this.lift > 0 || this.verticalSpeed != 0))
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
            this.UpdateIdleAction(dt);
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

        /// <summary>진화 연출 중인지. 먹이를 줄 수 없게 하는 데 쓴다.</summary>
        public bool IsEvolving { get { return this.evolving; } }

        /// <summary>진화하면 무엇이 되는지(키). 진화하지 않으면 null.</summary>
        public string NextKey
        {
            get { return this.nextKey; }
        }

        /// <summary>보고 있는 방향. 진화한 뒤에도 그대로 이어받는다.</summary>
        public int Facing
        {
            get { return this.direction; }
            set { this.direction = value >= 0 ? 1 : -1; this.Invalidate(); }
        }

        /// <summary>가로 위치. 크기를 바꿔 다시 만들 때 자리를 이어받는다.</summary>
        public double Position
        {
            get { return this.x; }
            set { this.x = Math.Min(Math.Max(0, value), this.maxX); this.MoveToPlace(); }
        }

        /// <summary>다른 포켓몬과의 거리를 재는 그림 가운데 위치.</summary>
        public double CenterPosition
        {
            get { return this.x + this.spriteWidth / 2.0; }
        }

        /// <summary>어디로 갔는지 안 보일 때, 확실히 보이는 자리로 데려온다.</summary>
        public void Recall()
        {
            this.dragging = false;
            this.lift = 0.0;
            this.verticalSpeed = 0.0;
            this.x = Math.Max(0, this.maxX / 2.0);
            this.MoveToPlace();
            this.BringToFront();
            this.TopMost = true;
            SetWindowPos(this.Handle, HwndTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate);
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

        /// <summary>머리 위로 하트·Zzz·말풍선을 띄운다.</summary>
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

        /// <summary>포켓몬마다 다른 짧은 대기 모션을 시작한다.</summary>
        private void StartIdleAction()
        {
            if (this.random.NextDouble() >= IdleActionChance)
            {
                return;
            }
            this.idleAction = this.IdleActionForSprite();
            if (this.idleAction == null)
            {
                return;
            }
            this.idleActionLeft = IdleActionMinSeconds + this.random.NextDouble()
                * (IdleActionMaxSeconds - IdleActionMinSeconds);
            this.idleEffectLeft = 0.0;
            this.idlePhase = 0.0;
        }

        private string IdleActionForSprite()
        {
            switch (this.SpriteKey)
            {
                case "pikachu": return "spark";
                case "charmander": return "flame";
                case "bulbasaur": return "leaf";
                case "squirtle":
                case "wartortle": return "bubble";
                case "ditto": return "wiggle";
                case "mew": return "twinkle";
                default: return null;
            }
        }

        private Color IdleColor()
        {
            switch (this.idleAction)
            {
                case "spark": return Color.FromArgb(255, 225, 77);
                case "flame": return Color.FromArgb(255, 120, 61);
                case "leaf": return Color.FromArgb(121, 201, 93);
                case "bubble": return Color.FromArgb(139, 217, 255);
                case "wiggle": return Color.FromArgb(220, 122, 232);
                default: return Color.FromArgb(246, 165, 229);
            }
        }

        private void UpdateIdleAction(double dt)
        {
            if (this.idleAction == null)
            {
                return;
            }
            this.idlePhase += dt;
            this.idleActionLeft -= dt;
            this.idleEffectLeft -= dt;
            if (this.idleEffectLeft <= 0)
            {
                this.idleEffectLeft = IdleEffectEvery;
                this.SpawnIdleEffect();
            }
            if (this.idleActionLeft <= 0)
            {
                this.idleAction = null;
            }
        }

        private void SpawnIdleEffect()
        {
            if (this.idleAction == "wiggle")
            {
                return;
            }
            Effect effect = new Effect();
            effect.Kind = this.idleAction;
            effect.X = this.marginX + this.spriteWidth * (0.48 + this.random.NextDouble() * 0.24);
            effect.Y = this.marginTop + this.spriteHeight * 0.16;
            effect.SpeedX = -8 + this.random.NextDouble() * 16;
            effect.SpeedY = -18.0;
            effect.Life = EmoteLife;
            effect.Tint = this.IdleColor();
            this.effects.Add(effect);
        }

        /// <summary>다른 포켓몬을 만났을 때 인사할 수 있는 상태인지.</summary>
        public bool CanGreet()
        {
            return this.walking && !this.dragging && !this.evolving
                && (this.floats || this.lift <= 0) && this.greetingLeft <= 0
                && this.greetingCooldown <= 0;
        }

        /// <summary>가까이 온 포켓몬을 바라보고 잠깐 인사한다.</summary>
        public void StartGreeting(PetForm partner)
        {
            this.walking = false;
            this.stopKind = null;
            this.playState = null;
            this.napping = false;
            this.idleAction = null;
            this.walkSpeed = 0.0;
            this.greetingLeft = GreetingSeconds;
            this.greetingPhase = 0.0;
            this.greetingCooldown = GreetingCooldown;
            this.greetingLeads = this.x < partner.x;
            this.greetingTalkTurn = -1;
            this.direction = partner.x > this.x ? 1 : -1;
        }

        /// <summary>대화 박자에서 지금 말풍선을 띄울 쪽인지.</summary>
        private bool GreetingSpeaking()
        {
            int turn = (int)(this.greetingPhase / GreetingTalkEvery) % 2;
            return this.greetingLeft > 0 && (turn == 0) == this.greetingLeads;
        }

        private void GreetingStep(double dt)
        {
            this.greetingLeft -= dt;
            this.greetingPhase += dt;
            int turn = (int)(this.greetingPhase / GreetingTalkEvery);
            if (turn != this.greetingTalkTurn)
            {
                this.greetingTalkTurn = turn;
                if (this.GreetingSpeaking())
                {
                    this.SpawnEmote("talk");
                }
            }
            if (this.greetingLeft <= 0)
            {
                this.walking = true;
                this.walkSpeed = 0.0;
            }
        }

        /// <summary>눈 깜빡임, 착지 눌림, 숨쉬기, 버둥거림 박자를 센다.</summary>
        private void UpdateTimers(double dt)
        {
            if (this.landSquash > 0)
            {
                this.landSquash -= dt;
            }
            if (this.greetingCooldown > 0)
            {
                this.greetingCooldown -= dt;
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
            if (this.dragging || this.evolving)
            {
                return null;
            }
            // 떠다니는 포켓몬은 늘 공중에 있으므로 그것만으로 늘어나지는 않는다.
            if (!this.floats && this.lift > this.dot)
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
            if (this.greetingLeft > 0)
            {
                return this.GreetingSpeaking() ? "stretch" : "squash";
            }
            if (this.idleAction != null && (int)(this.idlePhase / 0.22) % 2 == 1)
            {
                return this.idleAction == "spark" || this.idleAction == "flame"
                    || this.idleAction == "twinkle" ? "stretch" : "squash";
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
                    int[,] dots = EmoteDots(effect.Kind);
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

        private static int[,] EmoteDots(string kind)
        {
            switch (kind)
            {
                case "heart": return HeartDots;
                case "spark": return SparkDots;
                case "flame": return FlameDots;
                case "leaf": return LeafDots;
                case "bubble": return BubbleDots;
                case "twinkle": return TwinkleDots;
                case "talk": return TalkDots;
                default: return ZzzDots;
            }
        }

        /// <summary>메타몽처럼 폴짝폴짝 뛰어서 이동한다.
        ///
        /// 웅크렸다가(crouch) 튀어올라(air) 앞으로 나아가고, 착지해서 납작해졌다가
        /// (land) 잠시 쉰 뒤(rest) 다시 뛴다. 공중에 있는 동안에만 앞으로 간다.</summary>
        /// <summary>진화할 때 번갈아 보여 줄 하얀 실루엣 둘.
        ///
        /// 지금 모습과 진화한 모습의 윤곽만 새하얗게 칠한 것이다. 한 창 안에서
        /// 번갈아 보여 주므로, 그림마다 가운데·아래에 맞춰 놓을 위치도 함께 둔다.
        /// </summary>
        private void PrepareWhite()
        {
            if (this.whiteImages != null || this.nextKey == null)
            {
                return;
            }
            PokemonSprite before = Sprites.Find(this.SpriteKey);
            PokemonSprite after = Sprites.Find(this.nextKey);
            this.whiteImages = new Bitmap[2][];
            this.whiteOffsetX = new int[2];
            this.whiteOffsetY = new int[2];
            PokemonSprite[] forms = { before, after };
            for (int index = 0; index < 2; index++)
            {
                PokemonSprite form = forms[index];
                double scale = Math.Max(MinSpriteScale,
                    this.world.Options.Scale * form.ScaleFactor);
                Color?[][] shape = Silhouette(SpriteFactory.Frames(form)[0]);
                Bitmap right = SpriteFactory.Render(shape, scale, !form.FacesRight);
                Bitmap left = SpriteFactory.Render(shape, scale, form.FacesRight);
                this.whiteImages[index] = new Bitmap[] { right, left };
                this.whiteOffsetX[index] = (this.spriteWidth - right.Width) / 2;
                this.whiteOffsetY[index] = this.spriteHeight - right.Height;
            }
        }

        /// <summary>윤곽만 남기고 전부 하얗게 칠한 도트.</summary>
        private static Color?[][] Silhouette(Color?[][] grid)
        {
            Color?[][] shape = new Color?[grid.Length][];
            for (int y = 0; y < grid.Length; y++)
            {
                shape[y] = new Color?[grid[y].Length];
                for (int x = 0; x < grid[y].Length; x++)
                {
                    shape[y][x] = grid[y][x] == null ? (Color?)null : Color.White;
                }
            }
            return shape;
        }

        /// <summary>진화할 준비가 됐는지.</summary>
        public bool CanEvolve()
        {
            return this.nextKey != null
                && this.friendship >= EvolvePetNeed
                && this.walked >= EvolveWalkNeed
                && this.world.Options.GrowthDrops > 0
                && !this.evolving;
        }

        /// <summary>진화까지 몇 번 더 쓰다듬어야 하는지.</summary>
        public int PetsLeft()
        {
            double left = (EvolvePetNeed - this.friendship) / EvolvePerPet;
            return Math.Max(0, (int)Math.Ceiling(left));
        }

        /// <summary>진화까지 몇 픽셀을 더 산책해야 하는지.</summary>
        public int WalkLeft()
        {
            return Math.Max(0, (int)Math.Ceiling(EvolveWalkNeed - this.walked));
        }

        /// <summary>진화 연출을 시작한다. 끝나면 세계가 새 포켓몬으로 갈아 끼운다.</summary>
        public void StartEvolving()
        {
            if (!this.CanEvolve())
            {
                return;
            }
            this.PrepareWhite();
            this.world.Options.GrowthDrops--;
            this.world.SaveSettings();
            this.evolving = true;
            this.evolveStep = 0;
            this.evolveTimer = EvolveFirstSeconds;
            this.dragging = false;
        }

        /// <summary>번쩍임 간격. 갈수록 짧아져 점점 빨라진다.
        /// (EvolveFlashes 는 2 이상이어야 한다.)</summary>
        public static double EvolveFlashSeconds(int step)
        {
            double share = Math.Min(1.0, step / (double)(EvolveFlashes - 1));
            return EvolveFirstSeconds + (EvolveLastSeconds - EvolveFirstSeconds) * share;
        }

        /// <summary>번쩍임을 한 칸 진행한다. 다 끝났으면 true.</summary>
        private bool EvolveTick(double dt)
        {
            this.evolveTimer -= dt;
            if (this.evolveTimer > 0)
            {
                return false;
            }
            this.evolveStep++;
            if (this.evolveStep > EvolveFlashes)
            {
                return true;
            }
            this.evolveTimer = this.evolveStep == EvolveFlashes
                ? EvolveHoldSeconds          // 마지막엔 새하얗게 머문다
                : EvolveFlashSeconds(this.evolveStep);
            return false;
        }

        /// <summary>진화 연출. 지금 모습과 진화한 모습을 번갈아 하얗게 보여 준다.</summary>
        private void PaintEvolving(Graphics graphics)
        {
            // 마지막 한 박자는 진화한 모습으로 새하얗게 머문다.
            int form = (this.evolveStep % 2 != 0 || this.evolveStep >= EvolveFlashes) ? 1 : 0;
            int side = this.direction > 0 ? 0 : 1;
            graphics.DrawImageUnscaled(
                this.whiteImages[form][side],
                this.marginX + this.whiteOffsetX[form],
                this.marginTop + this.hop + this.whiteOffsetY[form]);
            this.PaintEffects(graphics);
        }

        /// <summary>쓰다듬었을 때. 하트가 뜨고 친밀도가 오른다.
        ///
        /// </summary>
        private void Petted()
        {
            this.SpawnEmote("heart");
            this.world.EarnCoins(PetWorld.CoinsPerPet);
            if (this.nextKey == null || this.evolving)
            {
                return;
            }
            this.friendship = Math.Min(EvolvePetNeed, this.friendship + EvolvePerPet);
        }

        /// <summary>포켓푸드를 먹었을 때. 하트가 뜨고 친밀도가 크게 오른다.</summary>
        public void Fed()
        {
            this.SpawnEmote("heart");
            if (this.nextKey == null || this.evolving)
            {
                return;
            }
            this.friendship = Math.Min(EvolvePetNeed,
                this.friendship + PetWorld.FoodFriendship);
        }

        /// <summary>걸은 만큼 옮기고, 실제 이동 거리로 보행 프레임과 산책을 진행한다.</summary>
        private double AdvanceWalk(double distance)
        {
            double beforeX = this.x;
            this.x += this.direction * distance;
            this.x = Math.Min(Math.Max(0.0, this.x), this.maxX);
            double actual = Math.Abs(this.x - beforeX);
            this.gaitDistance += actual;
            this.walked = Math.Min(EvolveWalkNeed, this.walked + actual);
            this.world.EarnWalkCoins(actual);
            return actual;
        }

        /// <summary>감속한 뒤 쉬거나 장난치거나 방향을 바꾼다.</summary>
        private void BeginStop(string kind, int nextDirection)
        {
            this.walking = false;
            this.stopKind = kind;
            this.turnDirection = nextDirection;
        }

        /// <summary>감속이 끝났을 때 다음 동작으로 넘긴다.</summary>
        private void FinishStop()
        {
            string kind = this.stopKind;
            this.stopKind = null;
            if (kind == "turn")
            {
                this.turnLeft = TurnPauseSeconds;
                this.landSquash = Math.Max(this.landSquash, TurnPauseSeconds);
            }
            else if (kind == "play")
            {
                this.StartPlaying();
            }
            else
            {
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
                    this.StartIdleAction();
                }
            }
        }

        /// <summary>지금 속도에서 부드럽게 멈춘다.</summary>
        private void SlowStopStep(double dt)
        {
            double beforeSpeed = this.walkSpeed;
            this.walkSpeed = Math.Max(0.0, this.walkSpeed - WalkDecel * dt);
            this.AdvanceWalk((beforeSpeed + this.walkSpeed) * 0.5 * dt);
            if (this.walkSpeed <= 0)
            {
                this.FinishStop();
            }
        }

        /// <summary>한 박자 멈춘 뒤 새 방향으로 걷기 시작한다.</summary>
        private void TurnStep(double dt)
        {
            this.turnLeft -= dt;
            if (this.turnLeft <= 0)
            {
                this.direction = this.turnDirection;
                this.walking = true;
                this.walkSpeed = 0.0;
            }
        }

        /// <summary>가속하며 걷고, 실제 이동 거리에 맞춰 발 프레임을 진행한다.</summary>
        private void WalkStep(double dt)
        {
            this.walkSpeed = Math.Min(this.speedValue, this.walkSpeed + WalkAccel * dt);
            double intended = this.walkSpeed * dt;
            double actual = this.AdvanceWalk(intended);
            if (actual + 0.01 < intended)
            {
                this.BeginStop("turn", -this.direction);
            }
            else if (this.random.NextDouble() < 0.004)
            {
                this.BeginStop("turn", -this.direction);
            }
            else if (this.random.NextDouble() < 0.005)
            {
                this.BeginStop(this.random.NextDouble() < PlayChance ? "play" : "idle",
                    this.direction);
            }
        }

        /// <summary>실제 걸은 거리에 맞는 보행 프레임.</summary>
        private int WalkFrame()
        {
            int phase = this.WalkPhase();
            return phase * this.frameCount / WalkSubsteps;
        }

        /// <summary>한 걸음 안에서 몸이 어디까지 올라왔는지(8단계).</summary>
        private int WalkPhase()
        {
            return (int)(this.gaitDistance / WalkStride * WalkSubsteps) % WalkSubsteps;
        }

        /// <summary>발을 드는 중에는 부드럽게 올라갔다가, 디딜 때 다시 내려온다.</summary>
        private int WalkBobPixels()
        {
            return (int)Math.Floor(this.bouncePixels * WalkBob[this.WalkPhase()] + 0.5);
        }

        /// <summary>올라갈 수 있는 가장 높은 곳. 창이 화면 위로 나가지 않게 한다.</summary>
        private double Ceiling()
        {
            return Math.Max(0.0, (double)this.baseY);
        }

        /// <summary>떠 있을 높이를 하나 고른다. 화면이 낮으면 그만큼 낮게 잡는다.</summary>
        private double PickFloatHeight()
        {
            double high = Math.Min(FloatHeightMax, this.Ceiling());
            double low = Math.Min(FloatHeightMin, high);
            return low + this.random.NextDouble() * (high - low);
        }

        /// <summary>뮤처럼 바닥을 딛지 않고 공중을 떠다닌다.
        ///
        /// 가로로는 느긋하게 흘러 다니고, 세로로는 목표 높이를 이따금 새로 골라
        /// 스르르 옮겨 가면서 그 위에서 살랑살랑 흔들린다. 중력은 받지 않는다.
        /// </summary>
        private void FloatStep(double dt)
        {
            this.animTime += dt;

            if (this.walking)
            {
                this.x += this.direction * this.speedValue * FloatSpeed * dt;
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
                else if (this.random.NextDouble() < FloatTurnChance)
                {
                    this.direction = -this.direction;
                }

                if (this.random.NextDouble() < FloatStopChance)
                {
                    this.walking = false;
                    if (this.random.NextDouble() < NapChance)
                    {
                        this.idleLeft = 4.0 + this.random.NextDouble() * 5.0;
                        this.napping = true;
                        this.zzzTimer = 0.35;
                    }
                    else
                    {
                        this.idleLeft = 0.8 + this.random.NextDouble() * 2.2;
                        this.StartIdleAction();
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

            this.floatTimer -= dt;
            if (this.floatTimer <= 0)
            {
                this.floatTarget = this.PickFloatHeight();
                this.floatTimer = FloatRetargetMin
                    + this.random.NextDouble() * (FloatRetargetMax - FloatRetargetMin);
            }

            // 목표 높이로 스르르 (한 틱에 다 가지 않도록 1.0 을 넘기지 않는다)
            this.floatBase +=
                (this.floatTarget - this.floatBase) * Math.Min(1.0, FloatEase * dt);

            this.floatPhase += dt;
            double bob = Math.Sin(this.floatPhase / FloatBobSeconds * 2.0 * Math.PI);
            double wanted = this.floatBase + bob * this.dot * FloatBobDots;
            this.lift = Math.Min(Math.Max(0.0, wanted), this.Ceiling());
        }

        /// <summary>걷는 포켓몬이 가끔 하는 짧은 제자리 점프 놀이를 시작한다.</summary>
        private void StartPlaying()
        {
            this.walking = false;
            this.napping = false;
            this.playState = "wait";
            this.playLeft = PlayWaitSeconds;
            this.playHops = 0;
        }

        /// <summary>잠깐 뜸을 들인 뒤 두 번 폴짝 뛰고 다시 걷는다.</summary>
        private void PlayStep(double dt)
        {
            if (this.playState == "air")
            {
                if (this.lift > 0)
                {
                    return;
                }
                if (this.playHops >= PlayHops)
                {
                    this.playState = null;
                    this.walking = true;
                    return;
                }
                this.playState = "wait";
                this.playLeft = PlayWaitSeconds;
                if (this.random.NextDouble() < PlayTurnChance)
                {
                    this.direction = -this.direction;
                }
                return;
            }

            this.playLeft -= dt;
            if (this.playLeft <= 0)
            {
                this.playHops++;
                this.verticalSpeed = PlayHopSpeed;
                this.playState = "air";
            }
        }

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
                this.StartIdleAction();
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
        public const int CoinsPerPet = 100;        // 한 번 쓰다듬을 때마다 받는 돈(원)
        public const int CoinsPerWalk = 100;       // 100px를 걸을 때마다 받는 돈(원)
        public const double CoinWalkDistance = 100.0; // 이만큼 걸을 때마다 돈을 받는다
        public const int FoodCost = 400;           // 포켓푸드 한 개 가격(원)
        public const double FoodFriendship = 2.0;  // 포켓푸드 한 개가 채우는 친밀도
        public const int GrowthDropCost = 2500;    // 성장의 물방울 한 개 가격(원)
        public const int MarketUpdateMilliseconds = 20000;
        public static readonly string[] StockNames = {
            "피카츄전기", "꼬부기워터", "이상해씨농장"
        };

        /// <summary>게임 안의 돈을 천 단위 쉼표가 있는 원 단위로 표시한다.</summary>
        public static string FormatWon(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture) + "원";
        }

        public readonly Options Options;
        public readonly Random Random = new Random();
        private readonly List<PetForm> pets = new List<PetForm>();
        private double coinWalkProgress;
        private Timer marketTimer;
        private bool quitting;
        private bool rebuilding;
        public bool Paused;

        private NotifyIcon tray;

        public PetWorld(Options options)
        {
            this.Options = options;
            foreach (string key in options.Species)
            {
                this.Add(key);
            }
            if (this.pets.Count == 0)
            {
                // 설정이 이상해도 빈 화면으로 남지 않도록 한 마리는 꼭 띄운다.
                this.Add("pikachu");
            }
            if (this.pets.Count == 0)
            {
                MessageBox.Show("포켓몬 그림을 하나도 불러오지 못했습니다.",
                    "하단바 포켓몬", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.ExitThread();
                return;
            }
            this.marketTimer = new Timer();
            this.marketTimer.Interval = MarketUpdateMilliseconds;
            this.marketTimer.Tick += delegate { this.UpdateMarket(); };
            this.marketTimer.Start();
            this.BuildTray();
        }

        /// <summary>알림 영역 아이콘. 포켓몬이 안 보여도 여기서 부르거나 끌 수 있다.</summary>
        private void BuildTray()
        {
            try
            {
                this.tray = new NotifyIcon();
                this.tray.Icon = LoadTrayIcon();
                this.tray.Text = "하단바 포켓몬";
                this.tray.Visible = true;

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Opening += delegate { this.BuildTrayMenu(menu); };
                this.tray.ContextMenuStrip = menu;
                this.tray.DoubleClick += delegate { this.RecallAll(); };
                Log.Write("알림 영역 아이콘 만듦");
            }
            catch (Exception error)
            {
                this.tray = null;        // 트레이가 없어도 프로그램은 그대로 돈다.
                Log.Fail("알림 영역 아이콘", error);
            }
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                Icon own = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (own != null)
                {
                    return own;
                }
            }
            catch (Exception)
            {
            }
            return SystemIcons.Application;
        }

        private void BuildTrayMenu(ContextMenuStrip menu)
        {
            menu.Items.Clear();

            ToolStripMenuItem add = new ToolStripMenuItem("포켓몬 추가");
            foreach (PokemonSprite sprite in Sprites.BaseSpecies())
            {
                string key = sprite.Key;
                add.DropDownItems.Add(sprite.NameKo, null, delegate { this.AddAndSave(key); });
            }
            menu.Items.Add(add);

            menu.Items.Add("화면 가운데로 데려오기", null, delegate { this.RecallAll(); });

            ToolStripMenuItem pause = new ToolStripMenuItem("잠시 멈춤", null,
                delegate { this.TogglePause(); });
            pause.Checked = this.Paused;
            menu.Items.Add(pause);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("어디에 있는지 보기", null, delegate
            {
                MessageBox.Show(Program.Diagnose(this.Options), "하단바 포켓몬 - 진단");
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("종료", null, delegate { this.QuitAll(); });
        }

        /// <summary>모든 포켓몬을 화면 가운데의 잘 보이는 자리로 부른다.</summary>
        public void RecallAll()
        {
            foreach (PetForm pet in this.pets.ToArray())
            {
                pet.Recall();
            }
        }

        public void Add(string key)
        {
            PokemonSprite sprite = Sprites.Find(key);
            if (sprite == null)
            {
                Log.Write("  " + key + ": 모르는 포켓몬이라 건너뜀");
                return;
            }
            PetForm pet = new PetForm(this, sprite);
            pet.FormClosed += delegate { this.Forget(pet); };
            this.pets.Add(pet);
            pet.Show();
            Log.Write("  " + key + ": 창 만들고 보임 " + pet.Bounds
                + " 보이는중=" + pet.Visible + " 맨앞=" + pet.TopMost);
        }

        /// <summary>걷다가 가까워진 두 포켓몬을 함께 인사시킨다.</summary>
        public bool TryStartGreeting(PetForm pet)
        {
            if (!pet.CanGreet())
            {
                return false;
            }
            foreach (PetForm partner in this.pets)
            {
                if (partner == pet || !partner.CanGreet())
                {
                    continue;
                }
                if (Math.Abs(pet.CenterPosition - partner.CenterPosition) <= PetForm.GreetingDistance)
                {
                    pet.StartGreeting(partner);
                    partner.StartGreeting(pet);
                    return true;
                }
            }
            return false;
        }

        public void AddRandom()
        {
            List<PokemonSprite> choices = Sprites.BaseSpecies();
            this.AddAndSave(choices[this.Random.Next(choices.Count)].Key);
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

        /// <summary>돈을 얻고 설정 파일에도 남긴다.</summary>
        public void EarnCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            this.Options.Coins += amount;
            this.SaveSettings();
        }

        /// <summary>스스로 걸은 100px마다 100원을 얻는다.</summary>
        public void EarnWalkCoins(double distance)
        {
            this.coinWalkProgress += distance;
            int amount = (int)(this.coinWalkProgress / CoinWalkDistance);
            if (amount > 0)
            {
                this.coinWalkProgress -= amount * CoinWalkDistance;
                this.EarnCoins(amount * CoinsPerWalk);
            }
        }

        /// <summary>포켓푸드를 한 개 산다.</summary>
        public void BuyFood()
        {
            if (this.Options.Coins < FoodCost)
            {
                return;
            }
            this.Options.Coins -= FoodCost;
            this.Options.Food++;
            this.SaveSettings();
        }

        /// <summary>진화에 필요한 성장의 물방울을 한 개 산다.</summary>
        public void BuyGrowthDrop()
        {
            if (this.Options.Coins < GrowthDropCost)
            {
                return;
            }
            this.Options.Coins -= GrowthDropCost;
            this.Options.GrowthDrops++;
            this.SaveSettings();
        }

        /// <summary>포켓푸드 하나를 골라 둔 포켓몬에게 준다.</summary>
        public void Feed(PetForm pet)
        {
            if (this.Options.Food <= 0 || pet.IsEvolving)
            {
                return;
            }
            this.Options.Food--;
            pet.Fed();
            this.SaveSettings();
        }

        /// <summary>현재 가격으로 가상 주식 한 주를 산다.</summary>
        public void BuyStock(int index)
        {
            int price = this.Options.StockPrices[index];
            if (this.Options.Coins < price)
            {
                return;
            }
            this.Options.Coins -= price;
            this.Options.StockShares[index]++;
            this.SaveSettings();
        }

        /// <summary>현재 가격으로 가상 주식 한 주를 판다.</summary>
        public void SellStock(int index)
        {
            if (this.Options.StockShares[index] <= 0)
            {
                return;
            }
            this.Options.StockShares[index]--;
            this.Options.Coins += this.Options.StockPrices[index];
            this.SaveSettings();
        }

        /// <summary>세 종목의 가상 가격을 조금씩 흔들고 저장한다.</summary>
        public void UpdateMarket()
        {
            for (int i = 0; i < this.Options.StockPrices.Length; i++)
            {
                this.Options.StockPrices[i] = Math.Max(100,
                    this.Options.StockPrices[i] + this.Random.Next(-2, 3) * 100);
            }
            this.SaveSettings();
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

        /// <summary>번쩍임이 끝났다. 같은 자리에 진화한 포켓몬을 놓는다.</summary>
        public void FinishEvolving(PetForm pet)
        {
            string key = pet.NextKey;
            if (key == null || Sprites.Find(key) == null)
            {
                return;
            }
            double where = pet.Position;
            int facing = pet.Facing;
            int index = this.pets.IndexOf(pet);

            this.rebuilding = true;       // 마지막 한 마리여도 프로그램이 끝나지 않게
            pet.Close();
            this.pets.Remove(pet);
            this.rebuilding = false;

            PetForm grown = new PetForm(this, Sprites.Find(key));
            grown.FormClosed += delegate { this.Forget(grown); };
            if (index < 0 || index > this.pets.Count)
            {
                index = this.pets.Count;
            }
            this.pets.Insert(index, grown);
            grown.Show();
            grown.Position = where;
            grown.Facing = facing;
            Log.Write("  " + key + ": 진화해서 나타남 " + grown.Bounds);
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
                this.QuitAll();
            }
            else if (!this.quitting && !this.rebuilding)
            {
                this.SaveSettings();
            }
        }

        public void QuitAll()
        {
            this.quitting = true;
            if (this.marketTimer != null)
            {
                this.marketTimer.Stop();
                this.marketTimer.Dispose();
            }
            if (this.tray != null)
            {
                this.tray.Visible = false;
                this.tray.Dispose();
                this.tray = null;
            }
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
            "      --list             포켓몬 목록 보기\n" +
            "      --where            화면 어디에 그려지는지 보기\n" +
            "      --check            창 없이 글자로만 자체 점검\n\n" +
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
                    case "--where":
                        options.ShowWhere = true;
                        break;
                    case "--check":
                        options.ShowCheck = true;
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
            // 진화해야 만날 수 있는 포켓몬은 무작위로 나눠 주지 않는다.
            List<PokemonSprite> choices = Sprites.BaseSpecies();
            while (options.Species.Count < options.Count)
            {
                options.Species.Add(choices[random.Next(choices.Count)].Key);
            }
            if (options.Count > 0 && options.Species.Count > options.Count)
            {
                options.Species = options.Species.GetRange(0, options.Count);
            }
            return options;
        }

        /// <summary>어떤 도트가 들어 있는지. 어느 빌드를 쓰는지 확인할 때 쓴다.</summary>
        private static string SpriteList()
        {
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
            return list;
        }

        /// <summary>조용히 죽지 않도록 오류를 창으로 보여 준다.</summary>
        private static void ShowCrash(string where, Exception error)
        {
            Log.Fail(where, error);
            try
            {
                MessageBox.Show(
                    "포켓몬을 띄우는 중 문제가 생겼습니다.\n\n[" + where + "]\n"
                        + (error == null ? "(예외 정보 없음)" : error.ToString())
                        + "\n\n자세한 기록: " + Log.Path,
                    "하단바 포켓몬 - 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>어디에 어떻게 그려지는지 알려 준다(안 보일 때 원인 찾기용).</summary>
        public static string Diagnose(Options options)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            text.AppendLine("화면: " + bounds.Width + "x" + bounds.Height
                + "  (좌상단 " + bounds.X + "," + bounds.Y + ")");
            text.AppendLine("작업 영역 아래쪽 y: " + work.Bottom + "  (작업 표시줄 높이 "
                + (bounds.Bottom - work.Bottom) + ")");
            text.AppendLine("모니터 수: " + Screen.AllScreens.Length);
            text.AppendLine();
            text.AppendLine("설정 파일: " + (options.SettingsPath ?? SettingsFile.DefaultPath()));
            try
            {
                string path = options.SettingsPath ?? SettingsFile.DefaultPath();
                text.AppendLine(File.Exists(path) ? File.ReadAllText(path) : "  (아직 없음)");
            }
            catch (Exception error)
            {
                text.AppendLine("  읽기 실패: " + error.Message);
            }
            text.AppendLine("포켓몬: " + string.Join(", ", options.Species.ToArray()));
            text.AppendLine("크기 " + options.Scale + " / 속도 " + options.Speed
                + " / 띄울 높이 " + options.Offset + " / 표시줄 위 " + options.OnTaskbar);
            text.AppendLine();

            int ground = options.OnTaskbar ? bounds.Bottom : work.Bottom;
            foreach (string key in options.Species)
            {
                PokemonSprite sprite = Sprites.Find(key);
                if (sprite == null)
                {
                    text.AppendLine(key + ": 없는 포켓몬");
                    continue;
                }
                double scale = Math.Max(MinSpriteScaleForCheck,
                    options.Scale * sprite.ScaleFactor);
                List<Color?[][]> frames = SpriteFactory.Frames(sprite);
                int dot = Math.Max(1, (int)Math.Round(scale));
                int spriteWidth = Math.Max(1, (int)Math.Floor(frames[0][0].Length * scale + 0.5));
                int spriteHeight = Math.Max(1, (int)Math.Floor(frames[0].Length * scale + 0.5));
                int windowWidth = spriteWidth + dot * 14;
                int windowHeight = spriteHeight + dot + dot * 9;
                text.AppendLine(string.Format(
                    "{0}: 창 {1}x{2}, 창 위쪽 y={3} (바닥 {4})",
                    key, windowWidth, windowHeight,
                    ground - windowHeight - options.Offset, ground));
            }
            return text.ToString();
        }

        private const double MinSpriteScaleForCheck = 0.5;

        [STAThread]
        public static int Main(string[] argv)
        {
            // 무슨 일이 있어도 흔적이 남도록 가장 먼저 로그부터 연다.
            Log.Begin();
            Log.Write("exe: " + Application.ExecutablePath);
            Log.Write("인자: " + string.Join(" ", argv));
            try
            {
                Log.Write("OS: " + Environment.OSVersion + " / 64비트 프로세스 "
                    + (IntPtr.Size == 8) + " / CLR " + Environment.Version);
            }
            catch (Exception error)
            {
                Log.Fail("환경 조사", error);
            }

            // 처리되지 않은 예외도 로그와 창에 남긴다. 어떤 일보다 먼저 걸어 둔다.
            AppDomain.CurrentDomain.UnhandledException +=
                delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    ShowCrash("바깥", e.ExceptionObject as Exception);
                };

            try
            {
                return Run(argv);
            }
            catch (Exception error)
            {
                ShowCrash("시작", error);
                return 1;
            }
        }

        private static int Run(string[] argv)
        {
            try
            {
                SetProcessDPIAware();
                Log.Write("DPI 인식 설정 완료");
            }
            catch (EntryPointNotFoundException)
            {
                // 아주 오래된 윈도우에서는 없을 수 있다. 무시해도 동작한다.
            }
            catch (DllNotFoundException)
            {
                // 윈도우가 아닌 환경(Mono 등).
            }

            Options options = Parse(argv);
            Log.Write("옵션 해석 완료");

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
                MessageBox.Show(SpriteList(), "하단바 포켓몬 - 목록");
                return 0;
            }

            if (options.ShowCheck)
            {
                // 콘솔에 한글을 쓰면 .NET Framework 에서 터지는 경우가 있어
                // 로그 파일에만 남긴다. check.bat 이 type 으로 보여 준다.
                Log.Write("--- 자체 점검 ---");
                Log.Write(Diagnose(options));
                Log.Write(SpriteList());
                Log.Write("여기까지 나왔으면 그림과 계산은 정상입니다.");
                return 0;
            }

            if (options.ShowWhere)
            {
                MessageBox.Show(Diagnose(options), "하단바 포켓몬 - 진단");
                return 0;
            }

            // 조용히 죽는 대신 무슨 일인지 보이게 한다.
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                ShowCrash("실행 중", e.Exception);
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Log.Write(Diagnose(options));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Log.Write("포켓몬 만드는 중...");
            PetWorld world = new PetWorld(options);
            Log.Write("메시지 루프 시작");
            Application.Run(world);
            Log.Write("정상 종료");
            return 0;
        }
    }
}
