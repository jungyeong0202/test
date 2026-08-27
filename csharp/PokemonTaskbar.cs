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
        public int[] StockPrices = { 1000, 1800, 2700, 1300, 2200, 3500 };
        public int[] StockShares = { 0, 0, 0, 0, 0, 0 };
        public int[] StockListingIds = { 0, 1, 2, 3, 4, 5 };
        public int[] StockDelisted = { 0, 0, 0, 0, 0, 0 };
        public int[] StockRelistSeconds = { 0, 0, 0, 0, 0, 0 };
        public int[] StockAveragePrices = { 0, 0, 0, 0, 0, 0 };
        public int[] StockHaltSeconds = { 0, 0, 0, 0, 0, 0 };
        public int[] FoodBoostSeconds = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public string SettingsPath = null;
        public bool SpeciesFromCommandLine = false;
        public bool ShowList = false;
        public bool ShowWhere = false;
        public bool ShowCheck = false;
        public bool ShowHelp = false;
        public string Error = null;
    }

    /// <summary>포켓볼 색 조합으로 메뉴를 그린다.</summary>
    public class PokemonMenuColors : ProfessionalColorTable
    {
        private static readonly Color Cream = Color.FromArgb(255, 247, 230);
        private static readonly Color Red = Color.FromArgb(217, 52, 59);
        private static readonly Color DarkRed = Color.FromArgb(151, 34, 42);
        private static readonly Color Brown = Color.FromArgb(58, 45, 38);

        public override Color ToolStripDropDownBackground { get { return Cream; } }
        public override Color ToolStripBorder { get { return DarkRed; } }
        public override Color MenuBorder { get { return DarkRed; } }
        public override Color MenuItemBorder { get { return DarkRed; } }
        public override Color MenuItemSelected { get { return Red; } }
        public override Color MenuItemSelectedGradientBegin { get { return Red; } }
        public override Color MenuItemSelectedGradientEnd { get { return Red; } }
        public override Color MenuItemPressedGradientBegin { get { return Cream; } }
        public override Color MenuItemPressedGradientEnd { get { return Cream; } }
        public override Color ImageMarginGradientBegin { get { return Cream; } }
        public override Color ImageMarginGradientMiddle { get { return Cream; } }
        public override Color ImageMarginGradientEnd { get { return Cream; } }
        public override Color SeparatorDark { get { return DarkRed; } }
        public override Color SeparatorLight { get { return Brown; } }
    }

    /// <summary>선택 항목은 포켓볼 빨강 위에 흰 글씨로 보여 준다.</summary>
    public class PokemonMenuRenderer : ToolStripProfessionalRenderer
    {
        public PokemonMenuRenderer() : base(new PokemonMenuColors())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is ToolStripLabel)
            {
                base.OnRenderItemText(e);
                return;
            }
            e.TextColor = !e.Item.Enabled ? Color.FromArgb(168, 145, 125)
                : e.Item.Selected ? Color.White : Color.FromArgb(58, 45, 38);
            base.OnRenderItemText(e);
        }
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

        private static int[] ParseStockValues(string value, bool requirePositive,
            int[] defaults, out int loaded)
        {
            string[] parts = value.Split(',');
            loaded = 0;
            if (parts.Length < 1 || parts.Length > defaults.Length)
            {
                return null;
            }
            int[] numbers = (int[])defaults.Clone();
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out numbers[i])
                    || numbers[i] < 0 || (requirePositive && numbers[i] == 0))
                {
                    return null;
                }
            }
            loaded = parts.Length;
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
            int loadedStockPrices = 0;

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
                        int priceCount;
                        int[] prices = ParseStockValues(value, true, options.StockPrices, out priceCount);
                        if (prices != null)
                        {
                            options.StockPrices = prices;
                            loadedStockPrices = priceCount;
                        }
                        break;
                    case "stock_shares":
                        int shareCount;
                        int[] shares = ParseStockValues(value, false, options.StockShares, out shareCount);
                        if (shares != null)
                        {
                            options.StockShares = shares;
                        }
                        break;
                    case "stock_listing_ids":
                        int listingCount;
                        int[] listings = ParseStockValues(value, false, options.StockListingIds, out listingCount);
                        if (listings != null)
                        {
                            options.StockListingIds = listings;
                        }
                        break;
                    case "stock_delisted":
                        int delistedCount;
                        int[] delisted = ParseStockValues(value, false, options.StockDelisted, out delistedCount);
                        if (delisted != null)
                        {
                            options.StockDelisted = delisted;
                        }
                        break;
                    case "stock_relist_seconds":
                        int relistCount;
                        int[] relist = ParseStockValues(value, false, options.StockRelistSeconds, out relistCount);
                        if (relist != null)
                        {
                            options.StockRelistSeconds = relist;
                        }
                        break;
                    case "stock_average_prices":
                        int averageCount;
                        int[] averages = ParseStockValues(value, false, options.StockAveragePrices, out averageCount);
                        if (averages != null)
                        {
                            options.StockAveragePrices = averages;
                        }
                        break;
                    case "stock_halt_seconds":
                        int haltCount;
                        int[] halts = ParseStockValues(value, false, options.StockHaltSeconds, out haltCount);
                        if (halts != null)
                        {
                            options.StockHaltSeconds = halts;
                        }
                        break;
                    case "food_boost_seconds":
                        int boostCount;
                        int[] boosts = ParseStockValues(value, false, options.FoodBoostSeconds, out boostCount);
                        if (boosts != null)
                        {
                            options.FoodBoostSeconds = boosts;
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
                if (loadedStockPrices > 0)
                {
                    for (int i = 0; i < loadedStockPrices; i++)
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
                lines.Add("stock_listing_ids = " + string.Join(", ", Array.ConvertAll(
                    options.StockListingIds, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
                lines.Add("stock_delisted = " + string.Join(", ", Array.ConvertAll(
                    options.StockDelisted, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
                lines.Add("stock_relist_seconds = " + string.Join(", ", Array.ConvertAll(
                    options.StockRelistSeconds, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
                lines.Add("stock_average_prices = " + string.Join(", ", Array.ConvertAll(
                    options.StockAveragePrices, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
                lines.Add("stock_halt_seconds = " + string.Join(", ", Array.ConvertAll(
                    options.StockHaltSeconds, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
                lines.Add("food_boost_seconds = " + string.Join(", ", Array.ConvertAll(
                    options.FoodBoostSeconds, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
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
        private const double EvolvePerPet = 1.0;       // 한 번 쓰다듬을 때마다
        private static readonly double[] EvolvePetNeeds = { 10.0, 25.0 };
        private static readonly double[] EvolveWalkNeeds = { 10000.0, 40000.0 };
        private static readonly int[] EvolveDropNeeds = { 1, 3 };
        private static readonly double[] EvolutionIncomeMultipliers = { 1.0, 1.5, 2.25 };
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
        private double foodBoostLeft;
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
            menu.Renderer = PetWorld.PokemonMenuRenderer;
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
            menu.Items.Add(PetWorld.CreateMenuTitle());
            menu.Items.Add(PetWorld.CreateMenuStatus(world.Options));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(PetWorld.CreateMenuSection("━━ 포켓몬 관리 ━━"));

            ToolStripMenuItem add = new ToolStripMenuItem("◆ 새 포켓몬 영입");
            ToolStripMenuItem randomPet = new ToolStripMenuItem(
                "랜덤 영입 — " + PetWorld.FormatWon(PetWorld.PokemonPrice)
                    + "  (일반 88% · 준전설 10% · 초전설 2%)", null,
                delegate { world.BuyRandomPet(); });
            randomPet.Enabled = world.Options.Coins >= PetWorld.PokemonPrice;
            add.DropDownItems.Add(randomPet);
            menu.Items.Add(add);

            menu.Items.Add("이 포켓몬 보내주기", null, delegate { world.Remove(this); });

            // 먹이와 진화 아이템은 모두가 공유한다. 메뉴를 열 때마다 수량을 새로 만든다.
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(PetWorld.CreateMenuSection("━━ 생활 · 경제 ━━"));
            ToolStripMenuItem shop = new ToolStripMenuItem(
                string.Format("◆ 상점 · 보유금 {0}", PetWorld.FormatWon(world.Options.Coins)));
            ToolStripMenuItem buyFood = new ToolStripMenuItem(
                string.Format("포켓푸드 · {0} · 2배 산책 5분 · 현재 {1}개",
                    PetWorld.FormatWon(PetWorld.FoodCost), world.Options.Food), null,
                delegate { world.BuyFood(); });
            buyFood.Enabled = world.Options.Coins >= PetWorld.FoodCost;
            shop.DropDownItems.Add(buyFood);
            ToolStripMenuItem buyGrowthDrop = new ToolStripMenuItem(
                string.Format("성장의 물방울 · {0} · 현재 {1}개", PetWorld.FormatWon(PetWorld.GrowthDropCost), world.Options.GrowthDrops), null,
                delegate { world.BuyGrowthDrop(); });
            buyGrowthDrop.Enabled = world.Options.Coins >= PetWorld.GrowthDropCost;
            shop.DropDownItems.Add(buyGrowthDrop);
            menu.Items.Add(shop);

            ToolStripMenuItem feed = new ToolStripMenuItem(
                string.Format("▶ 먹이 주기 · {0} · {1}개 보유", this.FoodBoostLabel(), world.Options.Food), null,
                delegate { world.Feed(this); });
            feed.Enabled = world.Options.Food > 0 && !this.evolving;
            menu.Items.Add(feed);

            menu.Items.Add(string.Format("▶ 주식시장 열기 · 평가액 {0}",
                PetWorld.FormatWon(world.StockPortfolioValue())), null,
                delegate { world.OpenStockOverlay(); });

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
                    if (world.Options.GrowthDrops < this.EvolveDropNeed)
                    {
                        needs.Add("성장의 물방울 " + this.EvolveDropNeed + "개");
                    }
                    this.evolveItem.Enabled = false;
                    this.evolveItem.Text = string.Format("{0}까지 {1}", name,
                        string.Join(" · ", needs.ToArray()));
                }
                menu.Items.Add(this.evolveItem);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(PetWorld.CreateMenuSection("━━ 움직임 · 설정 ━━"));

            ToolStripMenuItem sizes = new ToolStripMenuItem("크기 조절");
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

            ToolStripMenuItem speeds = new ToolStripMenuItem("산책 속도");
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

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                this.world.OpenGameMenu();
            }
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

            if (this.foodBoostLeft > 0 && !this.world.Paused)
            {
                this.foodBoostLeft = Math.Max(0.0, this.foodBoostLeft - dt);
            }

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
        private int EvolutionStage()
        {
            int stage = 0;
            string key = this.SpriteKey;
            while (true)
            {
                PokemonSprite previous = null;
                foreach (PokemonSprite sprite in Sprites.All)
                {
                    if (sprite.EvolvesTo == key)
                    {
                        previous = sprite;
                        break;
                    }
                }
                if (previous == null)
                {
                    return stage;
                }
                stage++;
                key = previous.Key;
            }
        }

        private double EvolvePetNeed
        {
            get { return EvolvePetNeeds[Math.Min(this.EvolutionStage(), EvolvePetNeeds.Length - 1)]; }
        }

        private double EvolveWalkNeed
        {
            get { return EvolveWalkNeeds[Math.Min(this.EvolutionStage(), EvolveWalkNeeds.Length - 1)]; }
        }

        private int EvolveDropNeed
        {
            get { return EvolveDropNeeds[Math.Min(this.EvolutionStage(), EvolveDropNeeds.Length - 1)]; }
        }

        private double IncomeMultiplier()
        {
            int stage = Math.Min(this.EvolutionStage(), EvolutionIncomeMultipliers.Length - 1);
            return PetWorld.PokemonIncomeMultiplier(this.SpriteKey) * EvolutionIncomeMultipliers[stage];
        }

        public bool CanEvolve()
        {
            return this.nextKey != null
                && this.friendship >= this.EvolvePetNeed
                && this.walked >= this.EvolveWalkNeed
                && this.world.Options.GrowthDrops >= this.EvolveDropNeed
                && !this.evolving;
        }

        /// <summary>진화까지 몇 번 더 쓰다듬어야 하는지.</summary>
        public int PetsLeft()
        {
            double left = (this.EvolvePetNeed - this.friendship) / EvolvePerPet;
            return Math.Max(0, (int)Math.Ceiling(left));
        }

        /// <summary>진화까지 몇 픽셀을 더 산책해야 하는지.</summary>
        public int WalkLeft()
        {
            return Math.Max(0, (int)Math.Ceiling(this.EvolveWalkNeed - this.walked));
        }

        /// <summary>진화 연출을 시작한다. 끝나면 세계가 새 포켓몬으로 갈아 끼운다.</summary>
        public void StartEvolving()
        {
            if (!this.CanEvolve())
            {
                return;
            }
            this.PrepareWhite();
            this.world.Options.GrowthDrops -= this.EvolveDropNeed;
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
            if (this.nextKey == null || this.evolving)
            {
                return;
            }
            this.friendship = Math.Min(this.EvolvePetNeed, this.friendship + EvolvePerPet);
        }

        /// <summary>포켓푸드로 친밀도와 5분짜리 2배 산책 버프를 준다.</summary>
        public void Fed()
        {
            this.SpawnEmote("heart");
            this.foodBoostLeft = PetWorld.FoodBoostSeconds;
            if (this.nextKey == null || this.evolving)
            {
                return;
            }
            this.friendship = Math.Min(this.EvolvePetNeed,
                this.friendship + PetWorld.FoodFriendship);
        }

        /// <summary>메뉴에서 남은 산책 부스트 시간을 짧게 보여 준다.</summary>
        public string FoodBoostLabel()
        {
            if (this.foodBoostLeft <= 0)
            {
                return "2배 산책 5분";
            }
            int seconds = (int)Math.Ceiling(this.foodBoostLeft);
            return string.Format("2배 산책 {0}:{1:00}", seconds / 60, seconds % 60);
        }

        public int FoodBoostSecondsLeft
        {
            get { return Math.Max(0, (int)Math.Ceiling(this.foodBoostLeft)); }
        }

        public void SetFoodBoost(int seconds)
        {
            this.foodBoostLeft = Math.Max(0, seconds);
        }

        /// <summary>걸은 만큼 옮기고, 실제 이동 거리로 보행 프레임과 산책을 진행한다.</summary>
        private double AdvanceWalk(double distance)
        {
            double beforeX = this.x;
            this.x += this.direction * distance;
            this.x = Math.Min(Math.Max(0.0, this.x), this.maxX);
            double actual = Math.Abs(this.x - beforeX);
            this.gaitDistance += actual;
            this.walked = Math.Min(this.EvolveWalkNeed, this.walked + actual);
            this.world.EarnWalkCoins(actual * this.IncomeMultiplier());
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
            double multiplier = this.foodBoostLeft > 0 ? PetWorld.FoodSpeedMultiplier : 1.0;
            this.walkSpeed = Math.Min(this.speedValue * multiplier,
                this.walkSpeed + WalkAccel * multiplier * dt);
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

    /// <summary>최근 주가를 작은 선 그래프로 보여 준다.</summary>
    public class StockGraph : Panel
    {
        private int[] values = { 1 };

        public StockGraph()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(255, 253, 247);
        }

        public void SetValues(int[] source)
        {
            this.values = source == null || source.Length == 0 ? new int[] { 1 } : source;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int low = this.values[0];
            int high = this.values[0];
            for (int i = 1; i < this.values.Length; i++)
            {
                low = Math.Min(low, this.values[i]);
                high = Math.Max(high, this.values[i]);
            }
            int spread = Math.Max(100, high - low);
            using (Pen grid = new Pen(Color.FromArgb(240, 223, 196)))
            {
                for (int i = 1; i < 4; i++)
                {
                    int y = this.Height * i / 4;
                    e.Graphics.DrawLine(grid, 0, y, this.Width, y);
                }
            }
            Point[] points = new Point[this.values.Length];
            for (int i = 0; i < this.values.Length; i++)
            {
                int x = this.values.Length == 1 ? 4
                    : 4 + (this.Width - 8) * i / (this.values.Length - 1);
                int y = this.Height - 5 - (this.Height - 10) * (this.values[i] - low) / spread;
                points[i] = new Point(x, y);
            }
            Color line = this.values[this.values.Length - 1] >= this.values[0]
                ? Color.FromArgb(47, 155, 103) : Color.FromArgb(217, 52, 59);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(line, 3))
            {
                if (points.Length > 1)
                {
                    e.Graphics.DrawLines(pen, points);
                }
            }
            using (SolidBrush brush = new SolidBrush(line))
            {
                Point last = points[points.Length - 1];
                e.Graphics.FillEllipse(brush, last.X - 3, last.Y - 3, 6, 6);
            }
        }
    }

    /// <summary>주식 거래와 최근 가격 흐름을 보여 주는 포켓몬풍 오버레이.</summary>
    public class StockOverlayForm : Form
    {
        private readonly PetWorld world;
        private Label balance;
        private readonly Label[] names = new Label[PetWorld.StockSlotCount];
        private readonly Label[] prices = new Label[PetWorld.StockSlotCount];
        private readonly Label[] positions = new Label[PetWorld.StockSlotCount];
        private readonly NumericUpDown[] quantities = new NumericUpDown[PetWorld.StockSlotCount];
        private readonly Button[] buys = new Button[PetWorld.StockSlotCount];
        private readonly Button[] sells = new Button[PetWorld.StockSlotCount];
        private readonly StockGraph[] graphs = new StockGraph[PetWorld.StockSlotCount];
        private Label notice;
        // 기존 카드 레이아웃은 컴파일에서 제외했지만, 호환용 갱신 경로는 유지한다.
        private Label marketEvent = new Label();
        private Label eventHistory = new Label();
        private Label updateHint;
        private bool tossLayout;
        private readonly Panel[] tossRows = new Panel[PetWorld.StockSlotCount];
        private readonly Label[] tossNames = new Label[PetWorld.StockSlotCount];
        private readonly Label[] tossHoldings = new Label[PetWorld.StockSlotCount];
        private readonly Label[] tossPrices = new Label[PetWorld.StockSlotCount];
        private readonly Label[] tossChanges = new Label[PetWorld.StockSlotCount];
        private Label tossDetailName;
        private Label tossDetailPrice;
        private Label tossDetailMeta;
        private Label tossDetailHolding;
        private Label tossDetailEvent;
        private StockGraph tossGraph;
        private NumericUpDown tossQuantity;
        private Button tossBuy;
        private Button tossSell;
        private int selectedStock;
        private Point dragCursor;
        private Point dragLocation;
        private bool dragging;

        public StockOverlayForm(PetWorld world)
        {
            this.world = world;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(217, 52, 59);
            this.Padding = new Padding(3);
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            bool compact = workArea.Width < 800 || workArea.Height < 820;
            this.ClientSize = compact
                ? new Size(Math.Max(420, workArea.Width - 20), Math.Max(420, workArea.Height - 20))
                : new Size(740, 800);
            this.AutoScroll = compact;
            this.AutoScrollMinSize = new Size(734, 794);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    this.Close();
                }
            };
            this.BuildTossLayout();
            return;

#if LEGACY_STOCK_UI
            Panel body = new Panel();
            body.BackColor = Color.FromArgb(255, 247, 230);
            body.Location = new Point(0, 0);
            body.Size = new Size(734, 794);
            this.Controls.Add(body);

            Panel header = new Panel();
            header.BackColor = Color.FromArgb(217, 52, 59);
            header.Location = new Point(0, 0);
            header.Size = new Size(734, 48);
            body.Controls.Add(header);
            header.MouseDown += this.BeginDrag;
            header.MouseMove += this.Drag;
            header.MouseUp += this.EndDrag;
            Label title = new Label();
            title.Text = "●  포켓몬 주식시장  ●";
            title.ForeColor = Color.White;
            title.BackColor = header.BackColor;
            title.Font = new Font("Malgun Gothic", 13.0f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(14, 10);
            header.Controls.Add(title);
            title.MouseDown += this.BeginDrag;
            title.MouseMove += this.Drag;
            title.MouseUp += this.EndDrag;
            this.updateHint = new Label();
            this.updateHint.Text = "다음 갱신 10초";
            this.updateHint.ForeColor = Color.FromArgb(255, 230, 199);
            this.updateHint.BackColor = header.BackColor;
            this.updateHint.Font = new Font("Malgun Gothic", 8.0f);
            this.updateHint.AutoSize = true;
            this.updateHint.Location = new Point(430, 15);
            header.Controls.Add(this.updateHint);
            this.updateHint.MouseDown += this.BeginDrag;
            this.updateHint.MouseMove += this.Drag;
            this.updateHint.MouseUp += this.EndDrag;
            Button close = new Button();
            close.Text = "×";
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.BackColor = header.BackColor;
            close.ForeColor = Color.White;
            close.Font = new Font("Malgun Gothic", 13.0f, FontStyle.Bold);
            close.Location = new Point(690, 4);
            close.Size = new Size(36, 36);
            close.Click += delegate { this.Close(); };
            header.Controls.Add(close);

            this.balance = new Label();
            this.balance.ForeColor = Color.FromArgb(58, 45, 38);
            this.balance.BackColor = body.BackColor;
            this.balance.Font = new Font("Malgun Gothic", 10.0f, FontStyle.Bold);
            this.balance.Location = new Point(15, 54);
            this.balance.Size = new Size(705, 42);
            body.Controls.Add(this.balance);
            Label rule = new Label();
            rule.Text = "매수·매도 수수료 2%  ·  이벤트 종목은 20초간 거래 정지";
            rule.ForeColor = Color.FromArgb(168, 145, 125);
            rule.BackColor = body.BackColor;
            rule.Font = new Font("Malgun Gothic", 8.0f);
            rule.Location = new Point(15, 96);
            rule.Size = new Size(705, 17);
            body.Controls.Add(rule);

            Panel eventBox = new Panel();
            eventBox.BackColor = Color.FromArgb(255, 240, 213);
            eventBox.BorderStyle = BorderStyle.FixedSingle;
            eventBox.Location = new Point(12, 115);
            eventBox.Size = new Size(710, 78);
            body.Controls.Add(eventBox);
            Label eventTitle = new Label();
            eventTitle.Text = "시장 속보";
            eventTitle.ForeColor = Color.FromArgb(217, 52, 59);
            eventTitle.BackColor = eventBox.BackColor;
            eventTitle.Font = new Font("Malgun Gothic", 9.0f, FontStyle.Bold);
            eventTitle.Location = new Point(8, 8);
            eventTitle.AutoSize = true;
            eventBox.Controls.Add(eventTitle);
            this.marketEvent = new Label();
            this.marketEvent.ForeColor = Color.FromArgb(58, 45, 38);
            this.marketEvent.BackColor = eventBox.BackColor;
            this.marketEvent.Font = new Font("Malgun Gothic", 10.0f, FontStyle.Bold);
            this.marketEvent.Location = new Point(74, 4);
            this.marketEvent.Size = new Size(625, 26);
            this.marketEvent.TextAlign = ContentAlignment.MiddleLeft;
            this.marketEvent.AutoEllipsis = true;
            eventBox.Controls.Add(this.marketEvent);
            this.eventHistory = new Label();
            this.eventHistory.ForeColor = Color.FromArgb(168, 145, 125);
            this.eventHistory.BackColor = eventBox.BackColor;
            this.eventHistory.Font = new Font("Malgun Gothic", 8.0f);
            this.eventHistory.Location = new Point(8, 34);
            this.eventHistory.Size = new Size(690, 38);
            this.eventHistory.TextAlign = ContentAlignment.TopLeft;
            this.eventHistory.AutoEllipsis = true;
            eventBox.Controls.Add(this.eventHistory);

            for (int i = 0; i < PetWorld.StockSlotCount; i++)
            {
                this.CreateStockCard(body, i, 12 + (i % 2) * 360, 202 + (i / 2) * 184);
            }
            this.notice = new Label();
            this.notice.Text = "가격은 10초마다 변동합니다";
            this.notice.ForeColor = Color.FromArgb(168, 145, 125);
            this.notice.BackColor = body.BackColor;
            this.notice.Font = new Font("Malgun Gothic", 9.0f);
            this.notice.AutoSize = true;
            this.notice.Location = new Point(210, 760);
            body.Controls.Add(this.notice);
            this.RefreshMarket();
#endif
        }

        private void BuildTossLayout()
        {
            this.tossLayout = true;
            Panel body = new Panel();
            body.BackColor = Color.FromArgb(245, 246, 248);
            body.Location = new Point(0, 0);
            body.Size = new Size(734, 794);
            this.Controls.Add(body);

            Panel header = new Panel();
            header.BackColor = Color.FromArgb(217, 52, 59);
            header.Location = new Point(0, 0);
            header.Size = new Size(734, 46);
            header.MouseDown += this.BeginDrag;
            header.MouseMove += this.Drag;
            header.MouseUp += this.EndDrag;
            body.Controls.Add(header);
            Label title = new Label();
            title.Text = "포켓몬 주식";
            title.ForeColor = Color.White;
            title.BackColor = header.BackColor;
            title.Font = new Font("Malgun Gothic", 13.0f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(16, 9);
            header.Controls.Add(title);
            this.updateHint = new Label();
            this.updateHint.ForeColor = Color.FromArgb(255, 230, 199);
            this.updateHint.BackColor = header.BackColor;
            this.updateHint.Font = new Font("Malgun Gothic", 8.0f, FontStyle.Bold);
            this.updateHint.AutoSize = true;
            this.updateHint.Location = new Point(460, 14);
            header.Controls.Add(this.updateHint);
            Button close = new Button();
            close.Text = "×";
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.BackColor = header.BackColor;
            close.ForeColor = Color.White;
            close.Font = new Font("Malgun Gothic", 13.0f, FontStyle.Bold);
            close.Location = new Point(690, 4);
            close.Size = new Size(36, 36);
            close.Click += delegate { this.Close(); };
            header.Controls.Add(close);

            Panel portfolio = new Panel();
            portfolio.BackColor = Color.White;
            portfolio.BorderStyle = BorderStyle.FixedSingle;
            portfolio.Location = new Point(12, 56);
            portfolio.Size = new Size(710, 83);
            body.Controls.Add(portfolio);
            this.balance = new Label();
            this.balance.BackColor = portfolio.BackColor;
            this.balance.ForeColor = Color.FromArgb(32, 33, 36);
            this.balance.Font = new Font("Malgun Gothic", 11.0f, FontStyle.Bold);
            this.balance.Location = new Point(13, 7);
            this.balance.Size = new Size(680, 44);
            portfolio.Controls.Add(this.balance);
            this.notice = new Label();
            this.notice.BackColor = portfolio.BackColor;
            this.notice.ForeColor = Color.FromArgb(107, 114, 128);
            this.notice.Font = new Font("Malgun Gothic", 8.0f, FontStyle.Bold);
            this.notice.Location = new Point(13, 54);
            this.notice.Size = new Size(680, 22);
            portfolio.Controls.Add(this.notice);

            Panel watch = new Panel();
            watch.BackColor = Color.White;
            watch.BorderStyle = BorderStyle.FixedSingle;
            watch.Location = new Point(12, 150);
            watch.Size = new Size(220, 630);
            body.Controls.Add(watch);
            Label watchTitle = new Label();
            watchTitle.Text = "전체 종목";
            watchTitle.BackColor = watch.BackColor;
            watchTitle.ForeColor = Color.FromArgb(32, 33, 36);
            watchTitle.Font = new Font("Malgun Gothic", 10.0f, FontStyle.Bold);
            watchTitle.Location = new Point(12, 9);
            watchTitle.AutoSize = true;
            watch.Controls.Add(watchTitle);
            for (int i = 0; i < PetWorld.StockSlotCount; i++)
            {
                int rowIndex = i;
                Panel row = new Panel();
                row.Location = new Point(6, 36 + i * 66);
                row.Size = new Size(206, 63);
                watch.Controls.Add(row);
                this.tossRows[i] = row;
                this.tossNames[i] = TossLabel(row, new Point(8, 7), new Size(106, 19),
                    ContentAlignment.MiddleLeft, 9.0f, FontStyle.Bold);
                this.tossHoldings[i] = TossLabel(row, new Point(8, 31), new Size(110, 18),
                    ContentAlignment.MiddleLeft, 7.5f, FontStyle.Regular);
                this.tossPrices[i] = TossLabel(row, new Point(116, 7), new Size(82, 19),
                    ContentAlignment.MiddleRight, 8.5f, FontStyle.Bold);
                this.tossChanges[i] = TossLabel(row, new Point(116, 31), new Size(82, 18),
                    ContentAlignment.MiddleRight, 8.0f, FontStyle.Bold);
                this.BindTossSelection(row, rowIndex);
                this.BindTossSelection(this.tossNames[i], rowIndex);
                this.BindTossSelection(this.tossHoldings[i], rowIndex);
                this.BindTossSelection(this.tossPrices[i], rowIndex);
                this.BindTossSelection(this.tossChanges[i], rowIndex);
            }

            Panel detail = new Panel();
            detail.BackColor = Color.White;
            detail.BorderStyle = BorderStyle.FixedSingle;
            detail.Location = new Point(242, 150);
            detail.Size = new Size(480, 630);
            body.Controls.Add(detail);
            this.tossDetailName = TossLabel(detail, new Point(16, 10), new Size(440, 25),
                ContentAlignment.MiddleLeft, 13.0f, FontStyle.Bold);
            this.tossDetailPrice = TossLabel(detail, new Point(16, 38), new Size(440, 32),
                ContentAlignment.MiddleLeft, 17.0f, FontStyle.Bold);
            this.tossDetailMeta = TossLabel(detail, new Point(16, 73), new Size(440, 21),
                ContentAlignment.MiddleLeft, 8.0f, FontStyle.Regular);
            this.tossGraph = new StockGraph();
            this.tossGraph.Location = new Point(14, 96);
            this.tossGraph.Size = new Size(450, 165);
            detail.Controls.Add(this.tossGraph);
            this.tossDetailHolding = TossLabel(detail, new Point(16, 269), new Size(448, 52),
                ContentAlignment.MiddleLeft, 9.0f, FontStyle.Bold);
            this.tossDetailHolding.BackColor = Color.FromArgb(248, 250, 252);
            this.tossDetailEvent = TossLabel(detail, new Point(16, 327), new Size(448, 49),
                ContentAlignment.TopLeft, 8.0f, FontStyle.Regular);
            this.tossDetailEvent.BackColor = Color.FromArgb(255, 245, 230);
            this.tossDetailEvent.ForeColor = Color.FromArgb(138, 75, 19);
            Label quantityLabel = TossLabel(detail, new Point(16, 391), new Size(32, 24),
                ContentAlignment.MiddleLeft, 8.0f, FontStyle.Bold);
            quantityLabel.Text = "수량";
            this.tossQuantity = new NumericUpDown();
            this.tossQuantity.Minimum = 1;
            this.tossQuantity.Maximum = 99;
            this.tossQuantity.Value = 1;
            this.tossQuantity.Font = new Font("Malgun Gothic", 9.0f, FontStyle.Bold);
            this.tossQuantity.TextAlign = HorizontalAlignment.Center;
            this.tossQuantity.Location = new Point(51, 392);
            this.tossQuantity.Size = new Size(54, 22);
            this.tossQuantity.ValueChanged += delegate { this.RefreshTossMarket(); };
            detail.Controls.Add(this.tossQuantity);
            int[] quickAmounts = { 1, 5, 10 };
            for (int i = 0; i < quickAmounts.Length; i++)
            {
                int amount = quickAmounts[i];
                Button quick = CreateQuickButton(amount.ToString());
                quick.Location = new Point(114 + i * 25, 393);
                quick.Click += delegate { this.SetTossQuantity(amount); };
                detail.Controls.Add(quick);
            }
            Button maximum = CreateQuickButton("최대");
            maximum.Location = new Point(189, 393);
            maximum.Size = new Size(31, 20);
            maximum.Click += delegate { this.SetTossQuantity(this.MaximumTossQuantity()); };
            detail.Controls.Add(maximum);
            this.tossBuy = CreateActionButton("매수", Color.FromArgb(217, 52, 59));
            this.tossBuy.Location = new Point(16, 430);
            this.tossBuy.Size = new Size(216, 48);
            this.tossBuy.Click += delegate { this.TradeToss(true); };
            detail.Controls.Add(this.tossBuy);
            this.tossSell = CreateActionButton("매도", Color.FromArgb(49, 130, 206));
            this.tossSell.Location = new Point(248, 430);
            this.tossSell.Size = new Size(216, 48);
            this.tossSell.Click += delegate { this.TradeToss(false); };
            detail.Controls.Add(this.tossSell);
            this.RefreshTossMarket();
        }

        private static Label TossLabel(Control parent, Point location, Size size,
            ContentAlignment alignment, float fontSize, FontStyle style)
        {
            Label label = new Label();
            label.BackColor = Color.White;
            label.ForeColor = Color.FromArgb(32, 33, 36);
            label.Font = new Font("Malgun Gothic", fontSize, style);
            label.Location = location;
            label.Size = size;
            label.TextAlign = alignment;
            label.AutoEllipsis = true;
            parent.Controls.Add(label);
            return label;
        }

        private void BindTossSelection(Control control, int index)
        {
            control.Click += delegate { this.selectedStock = index; this.RefreshTossMarket(); };
        }

        private void SetTossQuantity(int quantity)
        {
            this.tossQuantity.Value = Math.Min(99, Math.Max(1, quantity));
            this.RefreshTossMarket();
        }

        private int MaximumTossQuantity()
        {
            int affordable = this.world.Options.Coins / Math.Max(1, this.world.StockBuyCost(this.selectedStock));
            return Math.Min(99, Math.Max(1, Math.Max(affordable, this.world.Options.StockShares[this.selectedStock])));
        }

        private void TradeToss(bool buying)
        {
            int quantity = (int)this.tossQuantity.Value;
            int index = this.selectedStock;
            int amount = (buying ? this.world.StockBuyCost(index) : this.world.StockSellProceeds(index)) * quantity;
            if (quantity >= 10 || (buying && amount >= this.world.Options.Coins * 0.2))
            {
                string action = buying ? "매수" : "매도";
                if (MessageBox.Show(action + " " + quantity + "주\n" + PetWorld.FormatWon(amount)
                    + "\n거래할까요?", "거래 확인", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }
            if (buying)
            {
                this.world.BuyStock(index, quantity);
            }
            else
            {
                this.world.SellStock(index, quantity);
            }
        }

        private void RefreshTossMarket()
        {
            int portfolio = this.world.StockPortfolioValue();
            this.balance.Text = "총 자산  " + PetWorld.FormatWon(this.world.Options.Coins + portfolio)
                + "\n주식 평가액  " + PetWorld.FormatWon(portfolio)
                + string.Format("  ({0:+0.0;-0.0;0.0}%)", this.world.StockPortfolioChangePercent());
            this.notice.Text = "현금 " + PetWorld.FormatWon(this.world.Options.Coins)
                + "  ·  " + this.world.MarketMoverSummary;
            this.updateHint.Text = this.world.MarketSessionText + (this.world.MarketIsOpen
                ? " · " + this.world.MarketSecondsLeft + "초 후 갱신" : "");
            for (int i = 0; i < PetWorld.StockSlotCount; i++)
            {
                bool selected = i == this.selectedStock;
                Color background = selected ? Color.FromArgb(255, 240, 240) : Color.White;
                double delta = this.world.StockChangePercent(i);
                Color trend = delta < 0.0 ? Color.FromArgb(224, 49, 49)
                    : delta > 0.0 ? Color.FromArgb(25, 113, 194) : Color.FromArgb(107, 114, 128);
                this.tossRows[i].BackColor = background;
                this.tossNames[i].BackColor = background;
                this.tossHoldings[i].BackColor = background;
                this.tossPrices[i].BackColor = background;
                this.tossChanges[i].BackColor = background;
                this.tossNames[i].Text = this.world.StockName(i);
                this.tossNames[i].ForeColor = selected ? Color.FromArgb(217, 52, 59) : Color.FromArgb(32, 33, 36);
                this.tossHoldings[i].Text = this.world.Options.StockShares[i] > 0
                    ? "보유 " + this.world.Options.StockShares[i] + "주" : this.world.StockProfile(i);
                this.tossPrices[i].Text = this.world.IsStockDelisted(i) ? "상장폐지"
                    : PetWorld.FormatWon(this.world.Options.StockPrices[i]);
                this.tossChanges[i].Text = this.world.IsStockDelisted(i) ? "신규 상장 대기"
                    : string.Format("{0:+0.0;-0.0;0.0}%", delta);
                this.tossPrices[i].ForeColor = trend;
                this.tossChanges[i].ForeColor = trend;
            }
            int index = this.selectedStock;
            int price = this.world.Options.StockPrices[index];
            double percent = this.world.StockChangePercent(index);
            Color detailTrend = percent < 0.0 ? Color.FromArgb(224, 49, 49)
                : percent > 0.0 ? Color.FromArgb(25, 113, 194) : Color.FromArgb(32, 33, 36);
            this.tossDetailName.Text = this.world.StockName(index);
            this.tossGraph.SetValues(this.world.StockHistory(index));
            this.tossDetailEvent.Text = "시장 소식  ·  " + (string.IsNullOrEmpty(this.world.StockEvent)
                ? "시장 속보를 기다리는 중입니다." : this.world.StockEvent);
            if (this.world.IsStockDelisted(index))
            {
                this.tossDetailPrice.Text = "상장폐지";
                this.tossDetailPrice.ForeColor = Color.FromArgb(217, 52, 59);
                this.tossDetailMeta.Text = "신규 상장까지 " + this.world.RelistingMinutes(index) + "분";
                this.tossDetailHolding.Text = "보유 주식은 소멸했습니다. 새 종목 상장을 기다려 주세요.";
                this.tossQuantity.Enabled = false;
                this.tossBuy.Text = "매수 불가";
                this.tossSell.Text = "매도 불가";
                this.tossBuy.Enabled = false;
                this.tossSell.Enabled = false;
                return;
            }
            this.tossDetailPrice.Text = PetWorld.FormatWon(price) + string.Format("  {0:+0.0;-0.0;0.0}%", percent);
            this.tossDetailPrice.ForeColor = detailTrend;
            this.tossDetailMeta.Text = "장 기준가 " + PetWorld.FormatWon(this.world.StockSessionOpeningPrice(index))
                + " · 변동폭 ±" + this.world.StockVolatilityText(index) + "% · "
                + (this.world.MarketIsOpen ? "거래 가능" : "휴장 중");
            this.tossDetailHolding.Text = this.world.StockPositionText(index);
            if (!this.world.MarketIsOpen)
            {
                this.tossQuantity.Enabled = false;
                this.tossBuy.Text = "휴장 중";
                this.tossSell.Text = "휴장 중";
                this.tossBuy.Enabled = false;
                this.tossSell.Enabled = false;
                return;
            }
            if (this.world.IsStockHalted(index))
            {
                this.tossQuantity.Enabled = false;
                this.tossBuy.Text = "거래 정지";
                this.tossSell.Text = "거래 정지";
                this.tossBuy.Enabled = false;
                this.tossSell.Enabled = false;
                return;
            }
            int quantity = (int)this.tossQuantity.Value;
            this.tossQuantity.Enabled = true;
            this.tossBuy.Text = "매수 " + quantity + "주\r\n" + PetWorld.FormatWon(this.world.StockBuyCost(index) * quantity);
            this.tossSell.Text = "매도 " + quantity + "주\r\n" + PetWorld.FormatWon(this.world.StockSellProceeds(index) * quantity);
            this.tossBuy.Enabled = this.world.Options.Coins >= this.world.StockBuyCost(index) * quantity;
            this.tossSell.Enabled = this.world.Options.StockShares[index] >= quantity;
        }

        private void CreateStockCard(Panel parent, int index, int left, int top)
        {
            Panel card = new Panel();
            card.BackColor = Color.FromArgb(255, 253, 247);
            card.BorderStyle = BorderStyle.FixedSingle;
            card.Location = new Point(left, top);
            card.Size = new Size(350, 180);
            parent.Controls.Add(card);
            this.names[index] = new Label();
            this.names[index].Text = this.world.StockName(index);
            this.names[index].ForeColor = Color.FromArgb(58, 45, 38);
            this.names[index].BackColor = card.BackColor;
            this.names[index].Font = new Font("Malgun Gothic", 10.0f, FontStyle.Bold);
            this.names[index].AutoSize = true;
            this.names[index].Location = new Point(8, 5);
            card.Controls.Add(this.names[index]);
            this.prices[index] = new Label();
            this.prices[index].ForeColor = Color.FromArgb(217, 52, 59);
            this.prices[index].BackColor = card.BackColor;
            this.prices[index].Font = new Font("Malgun Gothic", 10.0f, FontStyle.Bold);
            this.prices[index].AutoEllipsis = true;
            this.prices[index].Location = new Point(8, 27);
            this.prices[index].Size = new Size(220, 22);
            card.Controls.Add(this.prices[index]);
            this.positions[index] = new Label();
            this.positions[index].ForeColor = Color.FromArgb(168, 145, 125);
            this.positions[index].BackColor = card.BackColor;
            this.positions[index].Font = new Font("Malgun Gothic", 8.0f);
            this.positions[index].Location = new Point(8, 49);
            this.positions[index].Size = new Size(220, 34);
            this.positions[index].TextAlign = ContentAlignment.TopLeft;
            card.Controls.Add(this.positions[index]);
            this.graphs[index] = new StockGraph();
            this.graphs[index].Location = new Point(8, 86);
            this.graphs[index].Size = new Size(220, 86);
            card.Controls.Add(this.graphs[index]);
            Label quantityLabel = new Label();
            quantityLabel.Text = "거래 수량";
            quantityLabel.ForeColor = Color.FromArgb(168, 145, 125);
            quantityLabel.BackColor = card.BackColor;
            quantityLabel.Font = new Font("Malgun Gothic", 8.0f);
            quantityLabel.Location = new Point(238, 8);
            quantityLabel.AutoSize = true;
            card.Controls.Add(quantityLabel);
            this.quantities[index] = new NumericUpDown();
            this.quantities[index].Minimum = 1;
            this.quantities[index].Maximum = 99;
            this.quantities[index].Value = 1;
            this.quantities[index].Font = new Font("Malgun Gothic", 8.0f, FontStyle.Bold);
            this.quantities[index].TextAlign = HorizontalAlignment.Center;
            this.quantities[index].Location = new Point(286, 5);
            this.quantities[index].Size = new Size(56, 22);
            this.quantities[index].ValueChanged += delegate { this.RefreshMarket(); };
            card.Controls.Add(this.quantities[index]);
            int quickIndex = index;
            int[] quickAmounts = { 1, 5, 10 };
            for (int quick = 0; quick < quickAmounts.Length; quick++)
            {
                int amount = quickAmounts[quick];
                Button quickButton = CreateQuickButton(amount.ToString());
                quickButton.Location = new Point(238 + quick * 25, 31);
                quickButton.Click += delegate { this.SetQuantity(quickIndex, amount); };
                card.Controls.Add(quickButton);
            }
            Button maximum = CreateQuickButton("최대");
            maximum.Location = new Point(313, 31);
            maximum.Size = new Size(29, 20);
            maximum.Click += delegate { this.SetQuantity(quickIndex, this.MaximumQuantity(quickIndex)); };
            card.Controls.Add(maximum);
            this.buys[index] = CreateActionButton("매수", Color.FromArgb(217, 52, 59));
            this.buys[index].Location = new Point(238, 55);
            int buyIndex = index;
            this.buys[index].Click += delegate {
                int quantity = (int)this.quantities[buyIndex].Value;
                if (this.ConfirmTrade(buyIndex, true, quantity))
                {
                    this.world.BuyStock(buyIndex, quantity);
                }
            };
            card.Controls.Add(this.buys[index]);
            this.sells[index] = CreateActionButton("매도", Color.FromArgb(58, 129, 199));
            this.sells[index].Location = new Point(238, 105);
            int sellIndex = index;
            this.sells[index].Click += delegate {
                int quantity = (int)this.quantities[sellIndex].Value;
                if (this.ConfirmTrade(sellIndex, false, quantity))
                {
                    this.world.SellStock(sellIndex, quantity);
                }
            };
            card.Controls.Add(this.sells[index]);
        }

        private static Button CreateActionButton(string text, Color color)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.Font = new Font("Malgun Gothic", 8.0f, FontStyle.Bold);
            button.Size = new Size(104, 43);
            return button;
        }

        private static Button CreateQuickButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(184, 121, 70);
            button.ForeColor = Color.White;
            button.Font = new Font("Malgun Gothic", 7.0f, FontStyle.Bold);
            button.Size = new Size(23, 20);
            return button;
        }

        private void SetQuantity(int index, int quantity)
        {
            this.quantities[index].Value = Math.Min(99, Math.Max(1, quantity));
            this.RefreshMarket();
        }

        private int MaximumQuantity(int index)
        {
            int affordable = this.world.Options.Coins / Math.Max(1, this.world.StockBuyCost(index));
            return Math.Min(99, Math.Max(1, Math.Max(affordable, this.world.Options.StockShares[index])));
        }

        private bool ConfirmTrade(int index, bool buying, int quantity)
        {
            int amount = (buying ? this.world.StockBuyCost(index) : this.world.StockSellProceeds(index)) * quantity;
            if (quantity < 10 && (!buying || amount < this.world.Options.Coins * 0.2))
            {
                return true;
            }
            string action = buying ? "매수" : "매도";
            return MessageBox.Show(action + " " + quantity + "주\n"
                + PetWorld.FormatWon(amount) + "\n거래할까요?", "거래 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            this.dragging = true;
            this.dragCursor = Cursor.Position;
            this.dragLocation = this.Location;
        }

        private void Drag(object sender, MouseEventArgs e)
        {
            if (!this.dragging)
            {
                return;
            }
            Point cursor = Cursor.Position;
            this.Location = new Point(this.dragLocation.X + cursor.X - this.dragCursor.X,
                this.dragLocation.Y + cursor.Y - this.dragCursor.Y);
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            this.dragging = false;
        }

        public void RefreshMarket()
        {
            if (this.tossLayout)
            {
                this.RefreshTossMarket();
                return;
            }
            int portfolio = this.world.StockPortfolioValue();
            double portfolioPercent = this.world.StockPortfolioChangePercent();
            this.balance.Text = "현금  " + PetWorld.FormatWon(this.world.Options.Coins)
                + "   ·   총 자산  " + PetWorld.FormatWon(this.world.Options.Coins + portfolio)
                + "\n주식 평가액  " + PetWorld.FormatWon(portfolio)
                + string.Format(" ({0:+0.0;-0.0;0.0}%)", portfolioPercent)
                + "   ·   " + this.world.MarketRegimeLabel;
            this.marketEvent.Text = string.IsNullOrEmpty(this.world.StockEvent)
                ? "특별 이벤트를 기다리는 중입니다." : this.world.StockEvent;
            this.eventHistory.Text = "최근 기록: " + this.world.StockEventHistory;
            this.updateHint.Text = this.world.MarketMoverSummary;
            for (int i = 0; i < PetWorld.StockSlotCount; i++)
            {
                int price = this.world.Options.StockPrices[i];
                int shares = this.world.Options.StockShares[i];
                int quantity = (int)this.quantities[i].Value;
                double percent = this.world.StockChangePercent(i);
                this.names[i].Text = this.world.StockName(i);
                if (this.world.IsStockDelisted(i))
                {
                    this.prices[i].Text = "상장폐지 · 신규 상장까지 " + this.world.RelistingMinutes(i) + "분";
                    this.prices[i].ForeColor = Color.FromArgb(217, 52, 59);
                    this.positions[i].Text = "보유 주식 소멸 · 새 종목을 준비하고 있습니다";
                    this.buys[i].Text = "매수 불가";
                    this.sells[i].Text = "매도 불가";
                    this.quantities[i].Enabled = false;
                    this.buys[i].Enabled = false;
                    this.sells[i].Enabled = false;
                    this.graphs[i].SetValues(this.world.StockHistory(i));
                    continue;
                }
                if (this.world.IsStockHalted(i))
                {
                    this.prices[i].Text = "거래 일시정지 · " + this.world.Options.StockHaltSeconds[i] + "초";
                    this.prices[i].ForeColor = Color.FromArgb(217, 52, 59);
                    this.positions[i].Text = this.world.StockPositionText(i);
                    this.buys[i].Text = "거래 정지";
                    this.sells[i].Text = "거래 정지";
                    this.quantities[i].Enabled = false;
                    this.buys[i].Enabled = false;
                    this.sells[i].Enabled = false;
                    this.graphs[i].SetValues(this.world.StockHistory(i));
                    continue;
                }
                this.prices[i].Text = string.Format("현재 {0}  ·  {1:+0.0;-0.0;0.0}%",
                    PetWorld.FormatWon(price), percent);
                this.prices[i].ForeColor = percent > 0 ? Color.FromArgb(47, 155, 103)
                    : percent < 0 ? Color.FromArgb(217, 52, 59) : Color.FromArgb(58, 45, 38);
                this.positions[i].Text = this.world.StockPositionText(i);
                this.quantities[i].Enabled = true;
                this.buys[i].Text = string.Format("매수 {0}주\r\n{1}", quantity,
                    PetWorld.FormatWon(this.world.StockBuyCost(i) * quantity));
                this.sells[i].Text = string.Format("매도 {0}주\r\n{1}", quantity,
                    PetWorld.FormatWon(this.world.StockSellProceeds(i) * quantity));
                this.buys[i].Enabled = this.world.Options.Coins >= this.world.StockBuyCost(i) * quantity;
                this.sells[i].Enabled = shares >= quantity;
                this.graphs[i].SetValues(this.world.StockHistory(i));
            }
            this.notice.Text = "최근 20회 가격 흐름 · 모든 거래에 수수료 2% 적용";
        }
    }

    /// <summary>펫 여러 마리를 관리한다.</summary>
    public class PetWorld : ApplicationContext
    {
        public const int CoinsPerWalk = 100;       // 100px를 걸을 때마다 받는 돈(원)
        public const double CoinWalkDistance = 100.0; // 이만큼 걸을 때마다 돈을 받는다
        // 기본 속도 55px/초로 두 시간 산책: 55 × 2 × 60 × 60 ÷ 100 × 100 = 396,000원.
        public const int PokemonPrice = 396000;
        public const int FoodCost = 8000;          // 5분 2배 산책으로 얻는 추가 수입보다 조금 낮춘 가격(원)
        public const double FoodFriendship = 2.0;  // 포켓푸드 한 개가 채우는 친밀도
        public const double FoodSpeedMultiplier = 2.0;
        public const int FoodBoostSeconds = 5 * 60;
        public const int GrowthDropCost = 15000;   // 성장의 물방울 한 개 가격(원)
        public const int MarketUpdateMilliseconds = 10000;
        public const int MarketOpenSeconds = 60 * 60;
        public const int MarketClosedSeconds = 5 * 60;
        public const double StockEventChance = 0.13;
        public const double MarketTickScale = 0.70;
        public const double StockFeeRate = 0.02;
        public const int StockHaltSeconds = 20;
        public const int StockSlotCount = 6;
        public const int StockRelistSeconds = 30 * 60;
        public const int StockDelistPrice = 600;
        public const int StockCrisisPrice = 600;
        public static readonly string[] StockNames = {
            "피카츄전기", "꼬부기워터", "이상해씨농장", "파이리화력",
            "메타몽랩", "뮤테크", "이브이패션", "고라파덕물류",
            "럭키메디컬", "갸라도스해운", "잠만보식품", "팬텀게임즈"
        };
        public static readonly int[] StockStartingPrices = {
            1000, 1800, 2700, 1300, 2200, 3500, 1600, 1200, 2400, 3000, 1900, 2800
        };
        public static readonly int[] StockVolatilities = {
            12, 7, 10, 18, 24, 30, 15, 20, 9, 22, 11, 28
        };
        private static readonly string[] MarketRegimeNames = {
            "횡보장", "상승장", "하락장", "과열장", "공포장"
        };
        private static readonly double[] MarketRegimeDrifts = { 0.0, 2.0, -2.0, 4.0, -4.0 };
        private static readonly int[] MarketRegimeWeights = { 3, 2, 2, 1, 1 };
        public static readonly ToolStripRenderer PokemonMenuRenderer =
            new PokemonMenuRenderer();
        private static readonly Font PokemonMenuTitleFont =
            new Font("Malgun Gothic", 10.0f, FontStyle.Bold);

        /// <summary>모든 메뉴 위에 보이는 포켓몬 센터 제목.</summary>
        public static ToolStripLabel CreateMenuTitle()
        {
            ToolStripLabel title = new ToolStripLabel("●  포켓몬 센터  ●");
            title.ForeColor = Color.FromArgb(217, 52, 59);
            title.Font = PokemonMenuTitleFont;
            title.Margin = new Padding(6, 4, 6, 4);
            return title;
        }

        /// <summary>메뉴 맨 위에서 돈과 소모품을 바로 확인하게 한다.</summary>
        public static ToolStripLabel CreateMenuStatus(Options options)
        {
            ToolStripLabel status = new ToolStripLabel(string.Format(
                "보유금 {0}  ·  포켓푸드 {1}개  ·  성장 물방울 {2}개",
                FormatWon(options.Coins), options.Food, options.GrowthDrops));
            status.ForeColor = Color.FromArgb(168, 145, 125);
            status.Margin = new Padding(6, 1, 6, 4);
            return status;
        }

        /// <summary>긴 메뉴를 행동 단위로 나누는 구분 제목.</summary>
        public static ToolStripLabel CreateMenuSection(string text)
        {
            ToolStripLabel section = new ToolStripLabel(text);
            section.ForeColor = Color.FromArgb(217, 52, 59);
            section.Font = PokemonMenuTitleFont;
            section.Margin = new Padding(6, 3, 6, 2);
            return section;
        }

        /// <summary>게임 안의 돈을 천 단위 쉼표가 있는 원 단위로 표시한다.</summary>
        public static string FormatWon(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture) + "원";
        }

        public static string PokemonGrade(string key)
        {
            string baseKey = key;
            while (true)
            {
                PokemonSprite previous = null;
                foreach (PokemonSprite sprite in Sprites.All)
                {
                    if (sprite.EvolvesTo == baseKey)
                    {
                        previous = sprite;
                        break;
                    }
                }
                if (previous == null)
                {
                    break;
                }
                baseKey = previous.Key;
            }
            if (baseKey == "ditto") return "준전설";
            if (baseKey == "mew") return "초전설";
            return "일반";
        }

        public static double PokemonIncomeMultiplier(string key)
        {
            string grade = PokemonGrade(key);
            return grade == "준전설" ? 1.6 : grade == "초전설" ? 2.5 : 1.0;
        }

        public readonly Options Options;
        public readonly Random Random = new Random();
        private readonly List<PetForm> pets = new List<PetForm>();
        private double coinWalkProgress;
        private List<int>[] stockHistory;
        private int[] stockSessionOpeningPrices;
        private StockOverlayForm stockOverlay;
        private Form gameMenu;
        private Timer marketTimer;
        private Timer haltTimer;
        private string stockEvent = "";
        private readonly List<string> stockEventHistory = new List<string>();
        private int marketRegime;
        private int marketRegimeUpdates = 6;
        private int marketSecondsLeft = MarketUpdateMilliseconds / 1000;
        private bool marketOpen = true;
        private int marketSessionSecondsLeft = MarketOpenSeconds;
        private readonly int[] stockTrends = new int[StockSlotCount];
        private bool quitting;
        private bool rebuilding;
        public bool Paused;

        private NotifyIcon tray;

        public PetWorld(Options options)
        {
            this.Options = options;
            this.stockHistory = new List<int>[StockSlotCount];
            this.stockSessionOpeningPrices = new int[StockSlotCount];
            for (int i = 0; i < StockSlotCount; i++)
            {
                this.stockHistory[i] = new List<int>();
                this.stockHistory[i].Add(options.StockPrices[i]);
                this.stockSessionOpeningPrices[i] = options.StockPrices[i];
            }
            for (int i = 0; i < options.Species.Count; i++)
            {
                int boost = i < options.FoodBoostSeconds.Length ? options.FoodBoostSeconds[i] : 0;
                this.Add(options.Species[i], boost);
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
            this.marketTimer.Tick += delegate {
                if (this.marketOpen)
                {
                    this.marketSecondsLeft = MarketUpdateMilliseconds / 1000;
                    this.UpdateMarket();
                }
            };
            this.marketTimer.Start();
            this.haltTimer = new Timer();
            this.haltTimer.Interval = 1000;
            this.haltTimer.Tick += delegate { this.TickMarketClock(); };
            this.haltTimer.Start();
            this.BuildTray();
            this.OpenGameMenu();
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
                menu.Renderer = PokemonMenuRenderer;
                menu.Opening += delegate { this.BuildTrayMenu(menu); };
                this.tray.ContextMenuStrip = menu;
                this.tray.DoubleClick += delegate { this.OpenGameMenu(); };
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
            menu.Items.Add(CreateMenuTitle());
            menu.Items.Add(CreateMenuStatus(this.Options));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateMenuSection("━━ 빠른 실행 ━━"));

            menu.Items.Add("▶ 포켓몬 센터 열기", null, delegate { this.OpenGameMenu(); });
            ToolStripMenuItem add = new ToolStripMenuItem("◆ 새 포켓몬 영입");
            ToolStripMenuItem randomPet = new ToolStripMenuItem(
                "랜덤 영입 — " + FormatWon(PokemonPrice) + "  (일반 88% · 준전설 10% · 초전설 2%)", null,
                delegate { this.BuyRandomPet(); });
            randomPet.Enabled = this.Options.Coins >= PokemonPrice;
            add.DropDownItems.Add(randomPet);
            menu.Items.Add(add);
            menu.Items.Add(string.Format("▶ 주식시장 열기 · 평가액 {0}",
                FormatWon(this.StockPortfolioValue())), null, delegate { this.OpenStockOverlay(); });

            menu.Items.Add("화면 가운데로 데려오기", null, delegate { this.RecallAll(); });

            ToolStripMenuItem pause = new ToolStripMenuItem("잠시 멈춤", null,
                delegate { this.TogglePause(); });
            pause.Checked = this.Paused;
            menu.Items.Add(pause);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateMenuSection("━━ 도움말 · 종료 ━━"));
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

        public void Add(string key, int foodBoostSeconds)
        {
            PokemonSprite sprite = Sprites.Find(key);
            if (sprite == null)
            {
                Log.Write("  " + key + ": 모르는 포켓몬이라 건너뜀");
                return;
            }
            PetForm pet = new PetForm(this, sprite);
            pet.SetFoodBoost(foodBoostSeconds);
            pet.FormClosed += delegate { this.Forget(pet); };
            this.pets.Add(pet);
            pet.Show();
            Log.Write("  " + key + ": 창 만들고 보임 " + pet.Bounds
                + " 보이는중=" + pet.Visible + " 맨앞=" + pet.TopMost);
        }

        public void Add(string key)
        {
            this.Add(key, 0);
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

        /// <summary>포켓몬을 한 마리 늘리고 설정에 남긴다.</summary>
        public void AddAndSave(string key)
        {
            this.Add(key);
            this.SaveSettings();
        }

        /// <summary>두 시간 산책값으로 포켓몬 한 마리를 산다.</summary>
        public void BuyPet(string key)
        {
            if (this.Options.Coins < PokemonPrice)
            {
                return;
            }
            this.Options.Coins -= PokemonPrice;
            this.Add(key);
            this.SaveSettings();
        }

        /// <summary>두 시간 산책값으로 무작위 포켓몬 한 마리를 산다.</summary>
        public void BuyRandomPet()
        {
            if (this.Options.Coins < PokemonPrice)
            {
                return;
            }
            List<PokemonSprite> choices = Sprites.BaseSpecies();
            double roll = this.Random.NextDouble();
            string grade = roll < 0.88 ? "일반" : roll < 0.98 ? "준전설" : "초전설";
            List<PokemonSprite> gradeChoices = new List<PokemonSprite>();
            foreach (PokemonSprite sprite in choices)
            {
                if (PokemonGrade(sprite.Key) == grade)
                {
                    gradeChoices.Add(sprite);
                }
            }
            if (gradeChoices.Count == 0)
            {
                gradeChoices = choices;
            }
            this.BuyPet(gradeChoices[this.Random.Next(gradeChoices.Count)].Key);
        }

        /// <summary>지금 구성을 설정 파일에 남긴다.</summary>
        public void SaveSettings()
        {
            List<string> species = new List<string>();
            foreach (PetForm pet in this.pets)
            {
                species.Add(pet.SpriteKey);
            }
            for (int i = 0; i < this.Options.FoodBoostSeconds.Length; i++)
            {
                this.Options.FoodBoostSeconds[i] = i < this.pets.Count
                    ? this.pets[i].FoodBoostSecondsLeft : 0;
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
        public void BuyStock(int index, int quantity)
        {
            if (!this.marketOpen || this.IsStockDelisted(index) || this.IsStockHalted(index))
            {
                return;
            }
            quantity = Math.Max(1, quantity);
            int shares = this.Options.StockShares[index];
            int cost = this.StockBuyCost(index) * quantity;
            if (this.Options.Coins < cost)
            {
                return;
            }
            this.Options.Coins -= cost;
            this.Options.StockAveragePrices[index] = (int)Math.Round(
                (this.Options.StockAveragePrices[index] * (double)shares + cost) / (shares + quantity),
                MidpointRounding.AwayFromZero);
            this.Options.StockShares[index] = shares + quantity;
            this.SaveSettings();
            this.RefreshStockOverlay();
        }

        /// <summary>현재 가격으로 가상 주식 한 주를 판다.</summary>
        public void SellStock(int index, int quantity)
        {
            quantity = Math.Max(1, quantity);
            if (!this.marketOpen || this.IsStockDelisted(index) || this.IsStockHalted(index)
                || this.Options.StockShares[index] < quantity)
            {
                return;
            }
            this.Options.StockShares[index] -= quantity;
            this.Options.Coins += this.StockSellProceeds(index) * quantity;
            if (this.Options.StockShares[index] == 0)
            {
                this.Options.StockAveragePrices[index] = 0;
            }
            this.SaveSettings();
            this.RefreshStockOverlay();
        }

        /// <summary>종목 성격별 등락과 이벤트, 상장폐지·신규 상장을 처리한다.</summary>
        public void UpdateMarket()
        {
            if (!this.marketOpen)
            {
                return;
            }
            this.stockEvent = "";
            this.UpdateMarketRegime();
            List<int> active = new List<int>();
            for (int i = 0; i < StockSlotCount; i++)
            {
                if (!this.IsStockDelisted(i) && !this.IsStockHalted(i))
                {
                    active.Add(i);
                }
            }
            int eventIndex = -1;
            int eventPercent = 0;
            string eventText = "";
            if (active.Count > 0 && this.Random.NextDouble() < StockEventChance)
            {
                eventIndex = active[this.Random.Next(active.Count)];
                bool positive = this.Random.Next(2) == 0;
                eventPercent = positive ? this.StockVolatility(eventIndex) + 8
                    : -(this.StockVolatility(eventIndex) + 6);
                eventText = this.StockName(eventIndex) + " "
                    + this.StockEventText(eventIndex, positive) + "  "
                    + (eventPercent >= 0 ? "+" : "") + eventPercent + "%";
            }
            for (int i = 0; i < StockSlotCount; i++)
            {
                if (this.IsStockDelisted(i))
                {
                    this.Options.StockRelistSeconds[i] -= MarketUpdateMilliseconds / 1000;
                    if (this.Options.StockRelistSeconds[i] <= 0)
                    {
                        this.RelistStock(i);
                    }
                    continue;
                }
                if (this.IsStockHalted(i))
                {
                    continue;
                }
                int volatility = this.StockVolatility(i);
                double change = this.StockMarketChange(i, volatility);
                if (i == eventIndex)
                {
                    change += eventPercent;
                }
                int price = StockPriceAfterChange(this.Options.StockPrices[i], change);
                if (price <= StockDelistPrice)
                {
                    this.Options.StockPrices[i] = 0;
                    this.Options.StockShares[i] = 0;
                    this.Options.StockAveragePrices[i] = 0;
                    this.Options.StockDelisted[i] = 1;
                    this.Options.StockRelistSeconds[i] = StockRelistSeconds;
                    this.AnnounceStockEvent(this.StockName(i) + " 상장폐지! 보유 주식은 소멸했습니다.");
                }
                else
                {
                    this.Options.StockPrices[i] = price;
                }
                this.stockHistory[i].Add(this.Options.StockPrices[i]);
                if (this.stockHistory[i].Count > 20)
                {
                    this.stockHistory[i].RemoveAt(0);
                }
            }
            if (eventIndex >= 0 && !this.IsStockDelisted(eventIndex))
            {
                this.Options.StockHaltSeconds[eventIndex] = StockHaltSeconds;
                this.AnnounceStockEvent(eventText + " · 변동성 완화장치 발동(20초 거래 정지)");
            }
            this.SaveSettings();
            this.RefreshStockOverlay();
        }

        public string StockName(int index)
        {
            return StockNames[this.Options.StockListingIds[index] % StockNames.Length];
        }

        public string MarketRegimeLabel
        {
            get { return MarketRegimeNames[this.marketRegime]; }
        }

        public int MarketSecondsLeft
        {
            get { return this.marketSecondsLeft; }
        }

        public bool MarketIsOpen
        {
            get { return this.marketOpen; }
        }

        public string MarketSessionText
        {
            get
            {
                string left = string.Format("{0:00}:{1:00}",
                    this.marketSessionSecondsLeft / 60, this.marketSessionSecondsLeft % 60);
                return this.marketOpen ? "개장 · 마감까지 " + left : "휴장 · 재개까지 " + left;
            }
        }

        public string MarketMoverSummary
        {
            get
            {
                if (!this.marketOpen)
                {
                    return "휴장 중 · 재개까지 " + string.Format("{0:00}:{1:00}",
                        this.marketSessionSecondsLeft / 60, this.marketSessionSecondsLeft % 60);
                }
                int rising = 0;
                int falling = 0;
                int halted = 0;
                for (int i = 0; i < StockSlotCount; i++)
                {
                    if (this.IsStockHalted(i))
                    {
                        halted++;
                    }
                    if (!this.IsStockDelisted(i) && this.StockChangePercent(i) > 0.0)
                    {
                        rising++;
                    }
                    if (!this.IsStockDelisted(i) && this.StockChangePercent(i) < 0.0)
                    {
                        falling++;
                    }
                }
                return string.Format("다음 갱신 {0}초 · 상승 {1}  하락 {2}  정지 {3}",
                    this.marketSecondsLeft, rising, falling, halted);
            }
        }

        private void UpdateMarketRegime()
        {
            this.marketRegimeUpdates--;
            if (this.marketRegimeUpdates > 0)
            {
                return;
            }
            int total = 0;
            for (int i = 0; i < MarketRegimeWeights.Length; i++)
            {
                total += MarketRegimeWeights[i];
            }
            int roll = this.Random.Next(total);
            int running = 0;
            for (int i = 0; i < MarketRegimeWeights.Length; i++)
            {
                running += MarketRegimeWeights[i];
                if (roll < running)
                {
                    this.marketRegime = i;
                    break;
                }
            }
            this.marketRegimeUpdates = this.Random.Next(6, 19);
            this.AnnounceStockEvent("시장 국면 전환: " + this.MarketRegimeLabel);
        }

        private double StockMarketChange(int index, int volatility)
        {
            if (this.Random.NextDouble() < 0.20)
            {
                this.stockTrends[index] = this.Random.Next(-1, 2);
            }
            int listing = this.Options.StockListingIds[index] % StockStartingPrices.Length;
            double priceGap = (StockStartingPrices[listing] - this.Options.StockPrices[index])
                * 100.0 / StockStartingPrices[listing];
            double pullRate = volatility <= 10 ? 0.20 : volatility <= 18 ? 0.12 : 0.06;
            double meanReversion = this.Options.StockPrices[index] < StockCrisisPrice ? 0.0
                : Math.Max(-5.0, Math.Min(5.0, priceGap * pullRate));
            double trend = this.stockTrends[index] * Math.Max(1.0, volatility * 0.16);
            double noise = this.Random.Next(-volatility, volatility + 1);
            return (noise + MarketRegimeDrifts[this.marketRegime] + trend + meanReversion)
                * MarketTickScale;
        }

        private static int StockPriceAfterChange(int price, double change)
        {
            // +x%와 -x%를 역수로 처리해 같은 크기의 왕복이 가격을 깎지 않게 한다.
            double factor = change >= 0.0 ? 1.0 + change / 100.0
                : 1.0 / (1.0 - change / 100.0);
            return Math.Max(1, (int)Math.Round(price * factor, MidpointRounding.AwayFromZero));
        }

        public string StockProfile(int index)
        {
            int volatility = this.StockVolatility(index);
            return volatility <= 10 ? "안정형" : volatility <= 18 ? "성장형" : "고위험형";
        }

        public int StockBuyCost(int index)
        {
            int price = this.Options.StockPrices[index];
            return price + (int)Math.Ceiling(price * StockFeeRate);
        }

        public int StockSellProceeds(int index)
        {
            int price = this.Options.StockPrices[index];
            return Math.Max(0, price - (int)Math.Ceiling(price * StockFeeRate));
        }

        public double StockProfitPercent(int index)
        {
            int average = this.Options.StockAveragePrices[index];
            if (this.Options.StockShares[index] <= 0 || average <= 0)
            {
                return 0.0;
            }
            return (this.StockSellProceeds(index) - average) * 100.0 / average;
        }

        public int StockHoldingValue(int index)
        {
            return this.StockSellProceeds(index) * this.Options.StockShares[index];
        }

        public int StockHoldingProfit(int index)
        {
            return this.StockHoldingValue(index)
                - this.Options.StockAveragePrices[index] * this.Options.StockShares[index];
        }

        public string StockPositionText(int index)
        {
            int volatility = this.StockVolatility(index);
            string trend = this.stockTrends[index] < 0 ? "하락 추세"
                : this.stockTrends[index] > 0 ? "상승 추세" : "횡보";
            if (this.Options.StockShares[index] <= 0)
            {
                return this.StockProfile(index) + " · " + trend
                    + "\n변동폭 ±" + volatility + "% · 보유 없음";
            }
            return string.Format("보유 {0}주 · 평가 {1}\n손익 {2:+#,0;-#,0;0}원 ({3:+0.0;-0.0;0.0}%) · {4}",
                this.Options.StockShares[index], FormatWon(this.StockHoldingValue(index)),
                this.StockHoldingProfit(index), this.StockProfitPercent(index), trend);
        }

        private int StockVolatility(int index)
        {
            return StockVolatilities[this.Options.StockListingIds[index] % StockVolatilities.Length];
        }

        public int StockVolatilityText(int index)
        {
            return this.StockVolatility(index);
        }

        public int StockSessionOpeningPrice(int index)
        {
            return this.stockSessionOpeningPrices[index];
        }

        public bool IsStockDelisted(int index)
        {
            return this.Options.StockDelisted[index] != 0;
        }

        public bool IsStockHalted(int index)
        {
            return this.Options.StockHaltSeconds[index] > 0;
        }

        public int RelistingMinutes(int index)
        {
            return Math.Max(1, (int)Math.Ceiling(this.Options.StockRelistSeconds[index] / 60.0));
        }

        public int StockPortfolioValue()
        {
            int total = 0;
            for (int i = 0; i < StockSlotCount; i++)
            {
                if (!this.IsStockDelisted(i))
                {
                    total += this.StockSellProceeds(i) * this.Options.StockShares[i];
                }
            }
            return total;
        }

        public int StockPortfolioCostBasis()
        {
            int total = 0;
            for (int i = 0; i < StockSlotCount; i++)
            {
                if (!this.IsStockDelisted(i))
                {
                    total += this.Options.StockAveragePrices[i] * this.Options.StockShares[i];
                }
            }
            return total;
        }

        public double StockPortfolioChangePercent()
        {
            int costBasis = this.StockPortfolioCostBasis();
            if (costBasis <= 0)
            {
                return 0.0;
            }
            return (this.StockPortfolioValue() - costBasis) * 100.0 / costBasis;
        }

        public string StockEvent
        {
            get { return this.stockEvent; }
        }

        public string StockEventHistory
        {
            get
            {
                return this.stockEventHistory.Count == 0 ? "아직 없습니다"
                    : string.Join("  ·  ", this.stockEventHistory.GetRange(
                        0, Math.Min(2, this.stockEventHistory.Count)).ToArray());
            }
        }

        private void AnnounceStockEvent(string text)
        {
            this.stockEvent = text;
            this.stockEventHistory.Insert(0, DateTime.Now.ToString("HH:mm") + "  " + text);
            if (this.stockEventHistory.Count > 5)
            {
                this.stockEventHistory.RemoveAt(5);
            }
        }

        private string StockEventText(int index, bool positive)
        {
            int listing = this.Options.StockListingIds[index] % StockNames.Length;
            string[] good = { "번개 발전소 증설", "정수장 장기 계약", "친환경 농장 수확",
                "화력 발전 수요 급증", "변신 연구 특허", "신기술 발표", "신작 컬렉션 완판",
                "물류 허브 확장", "건강식 수요 증가", "해운 노선 확대", "간식 판매 호조", "대형 게임 출시" };
            string[] bad = { "송전탑 고장", "가뭄 경보", "병충해 주의보", "화산재 공급 차질",
                "실험 결과 논란", "연구소 보안 사고", "유행 변화", "배송 지연", "진료비 규제",
                "폭풍 운항 중단", "원재료 가격 급등", "서버 장애" };
            return positive ? good[listing] : bad[listing];
        }

        private void RelistStock(int index)
        {
            int next;
            do
            {
                next = this.Random.Next(StockNames.Length);
            }
            while (next == this.Options.StockListingIds[index]);
            this.Options.StockListingIds[index] = next;
            this.Options.StockPrices[index] = StockStartingPrices[next];
            this.Options.StockShares[index] = 0;
            this.Options.StockAveragePrices[index] = 0;
            this.Options.StockDelisted[index] = 0;
            this.Options.StockRelistSeconds[index] = 0;
            this.Options.StockHaltSeconds[index] = 0;
            this.stockHistory[index].Clear();
            this.stockHistory[index].Add(this.Options.StockPrices[index]);
            this.stockSessionOpeningPrices[index] = this.Options.StockPrices[index];
            this.AnnounceStockEvent(this.StockName(index) + " 신규 상장!");
        }

        private void TickMarketClock()
        {
            this.marketSessionSecondsLeft--;
            if (this.marketSessionSecondsLeft <= 0)
            {
                this.marketOpen = !this.marketOpen;
                this.marketSessionSecondsLeft = this.marketOpen ? MarketOpenSeconds : MarketClosedSeconds;
                this.marketSecondsLeft = 0;
                if (this.marketOpen)
                {
                    for (int i = 0; i < StockSlotCount; i++)
                    {
                        this.stockSessionOpeningPrices[i] = this.Options.StockPrices[i];
                        this.stockHistory[i].Clear();
                        this.stockHistory[i].Add(this.Options.StockPrices[i]);
                    }
                    this.AnnounceStockEvent("시장 개장! 1시간 동안 거래 가능합니다.");
                }
                else
                {
                    this.AnnounceStockEvent("장 마감! 5분 동안 휴장합니다.");
                }
                this.SaveSettings();
            }
            if (!this.marketOpen)
            {
                this.RefreshStockOverlay();
                return;
            }
            this.marketSecondsLeft = Math.Max(1, this.marketSecondsLeft - 1);
            bool changed = false;
            for (int i = 0; i < StockSlotCount; i++)
            {
                if (this.Options.StockHaltSeconds[i] > 0)
                {
                    this.Options.StockHaltSeconds[i]--;
                    changed = true;
                }
            }
            if (changed)
            {
                this.SaveSettings();
            }
            this.RefreshStockOverlay();
        }

        /// <summary>현재 실행 중에 쌓인 최근 주가를 오버레이에 넘긴다.</summary>
        public int[] StockHistory(int index)
        {
            return this.stockHistory[index].ToArray();
        }

        /// <summary>이번 개장 때 정한 기준가와 비교한 등락률.</summary>
        public double StockChangePercent(int index)
        {
            int openingPrice = this.stockSessionOpeningPrices[index];
            if (openingPrice <= 0)
            {
                return 0.0;
            }
            return (this.Options.StockPrices[index] - openingPrice) * 100.0 / openingPrice;
        }

        /// <summary>주식시장 오버레이를 하나만 열고, 이미 열려 있으면 앞으로 가져온다.</summary>
        public void OpenGameMenu()
        {
            if (this.gameMenu != null && !this.gameMenu.IsDisposed)
            {
                this.gameMenu.BringToFront();
                this.gameMenu.Activate();
                return;
            }
            Form form = new Form();
            form.Text = "포켓몬 센터";
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(440, 392);
            form.BackColor = Color.FromArgb(245, 246, 248);
            form.Font = new Font("Malgun Gothic", 9.0f);
            Label summary = new Label();
            summary.BackColor = Color.White;
            summary.Location = new Point(12, 12);
            summary.Size = new Size(416, 58);
            summary.Padding = new Padding(12, 8, 0, 0);
            summary.Font = new Font("Malgun Gothic", 10.0f, FontStyle.Bold);
            form.Controls.Add(summary);
            Button draw = new Button(); draw.Location = new Point(12, 82); draw.Size = new Size(416, 42);
            Button food = new Button(); food.Location = new Point(12, 130); food.Size = new Size(202, 36);
            Button drop = new Button(); drop.Location = new Point(226, 130); drop.Size = new Size(202, 36);
            Button stock = new Button(); stock.Location = new Point(12, 174); stock.Size = new Size(416, 38);
            CheckBox topmost = new CheckBox(); topmost.Text = "항상 위"; topmost.Location = new Point(12, 220);
            topmost.AutoSize = true;
            Button sendBack = new Button(); sendBack.Text = "뒤로 보내기"; sendBack.Location = new Point(310, 216); sendBack.Size = new Size(118, 28);
            Label pets = new Label(); pets.BackColor = Color.White; pets.Location = new Point(12, 256);
            pets.Size = new Size(416, 124); pets.Padding = new Padding(12, 8, 0, 0);
            form.Controls.Add(draw); form.Controls.Add(food); form.Controls.Add(drop); form.Controls.Add(stock); form.Controls.Add(topmost); form.Controls.Add(sendBack); form.Controls.Add(pets);
            Action refresh = delegate {
                summary.Text = "보유 코인  " + FormatWon(this.Options.Coins) + "\r\n포켓푸드 " + this.Options.Food + "개 · 성장의 물방울 " + this.Options.GrowthDrops + "개";
                draw.Text = "랜덤 영입  " + FormatWon(PokemonPrice) + "  ·  일반 88% / 준전설 10% / 초전설 2%";
                draw.Enabled = this.Options.Coins >= PokemonPrice;
                food.Text = "포켓푸드  " + FormatWon(FoodCost); food.Enabled = this.Options.Coins >= FoodCost;
                drop.Text = "성장의 물방울  " + FormatWon(GrowthDropCost); drop.Enabled = this.Options.Coins >= GrowthDropCost;
                List<string> lines = new List<string>();
                foreach (PetForm pet in this.pets) lines.Add(pet.SpriteKey + " · " + PokemonGrade(pet.SpriteKey));
                pets.Text = "내 포켓몬\r\n" + string.Join("\r\n", lines.ToArray());
            };
            draw.Click += delegate { this.BuyRandomPet(); refresh(); };
            food.Click += delegate { this.BuyFood(); refresh(); };
            drop.Click += delegate { this.BuyGrowthDrop(); refresh(); };
            stock.Click += delegate { this.OpenStockOverlay(); };
            topmost.CheckedChanged += delegate { form.TopMost = topmost.Checked; };
            sendBack.Click += delegate { topmost.Checked = false; form.SendToBack(); };
            form.FormClosed += delegate { this.gameMenu = null; };
            this.gameMenu = form;
            refresh();
            form.Show();
        }

        public void OpenStockOverlay()
        {
            if (this.stockOverlay != null && !this.stockOverlay.IsDisposed)
            {
                this.stockOverlay.Show();
                this.stockOverlay.BringToFront();
                this.stockOverlay.Activate();
                this.stockOverlay.RefreshMarket();
                return;
            }
            this.stockOverlay = new StockOverlayForm(this);
            this.stockOverlay.FormClosed += delegate { this.stockOverlay = null; };
            this.stockOverlay.Show();
        }

        private void RefreshStockOverlay()
        {
            if (this.stockOverlay == null || this.stockOverlay.IsDisposed)
            {
                return;
            }
            this.stockOverlay.RefreshMarket();
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
            List<int> boosts = new List<int>();
            foreach (PetForm pet in this.pets)
            {
                keys.Add(pet.SpriteKey);
                places.Add(pet.Position);
                boosts.Add(pet.FoodBoostSecondsLeft);
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
                this.Add(keys[i], boosts[i]);
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
            int boost = pet.FoodBoostSecondsLeft;
            int index = this.pets.IndexOf(pet);

            this.rebuilding = true;       // 마지막 한 마리여도 프로그램이 끝나지 않게
            pet.Close();
            this.pets.Remove(pet);
            this.rebuilding = false;

            PetForm grown = new PetForm(this, Sprites.Find(key));
            grown.SetFoodBoost(boost);
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
            if (this.stockOverlay != null && !this.stockOverlay.IsDisposed)
            {
                this.stockOverlay.Close();
            }
            if (this.marketTimer != null)
            {
                this.marketTimer.Stop();
                this.marketTimer.Dispose();
            }
            if (this.haltTimer != null)
            {
                this.haltTimer.Stop();
                this.haltTimer.Dispose();
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
