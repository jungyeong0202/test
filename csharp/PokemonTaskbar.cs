// 화면 하단(작업 표시줄) 위를 포켓몬이 돌아다니는 데스크톱 펫 - 파이썬 없이 도는 C# 판.
//
// 윈도우에 기본 탑재된 .NET Framework 컴파일러로 빌드한다. run.bat 참고.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
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
        public int[] StockPrimaryTraitIds = { 0, 1, 2, 3, 4, 5 };
        public int[] StockSecondaryTraitIds = { 0, 2, 4, 6, 1, 3 };
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

    /// <summary>EXE에 내장한 Noto Sans KR을 설치 없이 사용하는 UI 글꼴 팩토리.</summary>
    internal static class UiFonts
    {
        private const string FontResourceName = "PokemonTaskbar.NotoSansKR.ttf";
        private const string LicenseResourceName = "PokemonTaskbar.NotoSansKR.OFL.txt";
        private static readonly PrivateFontCollection Collection = new PrivateFontCollection();
        private static FontFamily family;
        private static IntPtr fontMemory = IntPtr.Zero;
        private static bool initialized;
        private static bool loadedFromResource;

        public static Font Create(float size)
        {
            return Create(size, FontStyle.Regular);
        }

        public static Font Create(float size, FontStyle style)
        {
            EnsureLoaded();
            FontStyle availableStyle = family.IsStyleAvailable(style) ? style : FontStyle.Regular;
            return new Font(family, size, availableStyle, GraphicsUnit.Point);
        }

        public static string Description
        {
            get
            {
                EnsureLoaded();
                bool licenseEmbedded = Assembly.GetExecutingAssembly()
                    .GetManifestResourceInfo(LicenseResourceName) != null;
                return family.Name + (loadedFromResource ? " (embedded)" : " (system fallback)")
                    + (licenseEmbedded ? ", OFL embedded" : ", OFL missing");
            }
        }

        private static void EnsureLoaded()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(FontResourceName))
                {
                    if (stream != null)
                    {
                        int length = checked((int)stream.Length);
                        byte[] bytes = new byte[length];
                        int offset = 0;
                        while (offset < bytes.Length)
                        {
                            int read = stream.Read(bytes, offset, bytes.Length - offset);
                            if (read <= 0) break;
                            offset += read;
                        }
                        if (offset != bytes.Length) throw new EndOfStreamException("Embedded font is incomplete.");
                        fontMemory = Marshal.AllocCoTaskMem(bytes.Length);
                        Marshal.Copy(bytes, 0, fontMemory, bytes.Length);
                        Collection.AddMemoryFont(fontMemory, bytes.Length);
                        family = FindFamily("Noto Sans KR");
                        loadedFromResource = family != null;
                    }
                }
            }
            catch
            {
                family = null;
                loadedFromResource = false;
            }
            if (family == null)
            {
                try { family = new FontFamily("Noto Sans KR"); }
                catch { family = new FontFamily("Malgun Gothic"); }
            }
            AppDomain.CurrentDomain.ProcessExit += delegate
            {
                Collection.Dispose();
                if (fontMemory != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(fontMemory);
                    fontMemory = IntPtr.Zero;
                }
            };
        }

        private static FontFamily FindFamily(string name)
        {
            foreach (FontFamily candidate in Collection.Families)
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) return candidate;
            return Collection.Families.Length > 0 ? Collection.Families[0] : null;
        }
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
                    case "stock_primary_trait_ids":
                        int primaryTraitCount;
                        int[] primaryTraits = ParseStockValues(value, false,
                            options.StockPrimaryTraitIds, out primaryTraitCount);
                        if (primaryTraits != null)
                        {
                            options.StockPrimaryTraitIds = primaryTraits;
                        }
                        break;
                    case "stock_secondary_trait_ids":
                        int secondaryTraitCount;
                        int[] secondaryTraits = ParseStockValues(value, false,
                            options.StockSecondaryTraitIds, out secondaryTraitCount);
                        if (secondaryTraits != null)
                        {
                            options.StockSecondaryTraitIds = secondaryTraits;
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
                lines.Add("stock_primary_trait_ids = " + string.Join(", ", Array.ConvertAll(
                    options.StockPrimaryTraitIds, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
                lines.Add("stock_secondary_trait_ids = " + string.Join(", ", Array.ConvertAll(
                    options.StockSecondaryTraitIds, delegate(int value) { return value.ToString(CultureInfo.InvariantCulture); })));
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

        // 진화. 먹이로 올린 친밀도와 함께 걸은 거리를 채운 뒤, 메뉴에서 직접 진화한다.
        //
        // 시간이 흘렀다고 저절로 진화하지는 않는다. 아끼던 모습이 예고 없이
        // 바뀌면 곤란하므로, 진화할지 말지는 플레이어가 메뉴에서 정한다.
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
            menu.Font = UiFonts.Create(9.0f);
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
                    if (this.FoodsLeft() > 0)
                    {
                        needs.Add(string.Format("포켓푸드 {0}개 더 필요", this.FoodsLeft()));
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
                this.dragStart = this.Pointer;
                this.dragOffset = new Point(
                    this.Pointer.X - (int)this.x,
                    this.Pointer.Y - (this.baseY - (int)this.lift));
                this.verticalSpeed = 0.0;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (this.dragging && !this.IsDisposed)
            {
                Point now = this.Pointer;
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

        /// <summary>게임 센터에서 진화 진행도를 표시하기 위한 읽기 전용 상태.</summary>
        public double FriendshipValue { get { return this.friendship; } }
        public double FriendshipNeed { get { return this.EvolvePetNeed; } }
        public double DisplayedFriendshipValue
        {
            get { return this.nextKey == null ? this.EvolvePetNeed : this.friendship; }
        }
        public double WalkedValue { get { return this.walked; } }
        public double WalkNeed { get { return this.EvolveWalkNeed; } }
        public int GrowthDropsNeed { get { return this.EvolveDropNeed; } }
        public int EvolutionStageValue { get { return this.EvolutionStage(); } }
        public double IncomeMultiplierValue { get { return this.IncomeMultiplier(); } }
        public Bitmap MenuImage { get { return this.images[0][0]; } }

        /// <summary>마우스가 지금 어디 있는지. 테스트는 이것을 직접 정한다.
        ///
        /// 끌기는 화면 좌표로 계산하므로 실제 커서를 읽어야 하는데, 그러면 테스트가
        /// 커서를 움직일 방법이 없다. 읽는 곳을 여기 한 군데로 모아 두었다.
        /// </summary>
        private Point pointerOverride = Point.Empty;
        private bool pointerIsSet;

        private Point Pointer
        {
            get { return this.pointerIsSet ? this.pointerOverride : Control.MousePosition; }
        }

        /// <summary>테스트가 마우스 자리를 정한다.</summary>
        internal void SetPointer(int x, int y)
        {
            this.pointerOverride = new Point(x, y);
            this.pointerIsSet = true;
        }

        /// <summary>누르기 / 끌기 / 놓기. 테스트가 실제 커서 없이 부른다.</summary>
        internal void Press(int x, int y)
        {
            this.SetPointer(x, y);
            this.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        }

        internal void DragTo(int x, int y)
        {
            this.SetPointer(x, y);
            this.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
        }

        internal void Release(int x, int y)
        {
            this.SetPointer(x, y);
            this.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        }

        internal bool IsDragging
        {
            get { return this.dragging; }
        }

        /// <summary>지금 떠 있는 효과(먼지·하트·Zzz)의 수.</summary>
        internal int EffectCount
        {
            get { return this.effects.Count; }
        }

        /// <summary>창 위쪽 y. 바닥선 검사에 쓴다.</summary>
        internal int BaseY
        {
            get { return this.baseY; }
        }

        internal int WindowW { get { return this.windowWidth; } }
        internal int WindowH { get { return this.windowHeight; } }
        internal int SpriteW { get { return this.spriteWidth; } }
        internal int SpriteH { get { return this.spriteHeight; } }

        /// <summary>걸은 거리를 바로 채운다. 테스트가 몇 분씩 기다리지 않게 한다.</summary>
        internal void SetWalked(double distance)
        {
            this.walked = Math.Min(this.EvolveWalkNeed, Math.Max(0.0, distance));
        }

        /// <summary>진화에 필요한 걷기 거리.</summary>
        internal double WalkNeedForTest
        {
            get { return this.EvolveWalkNeed; }
        }

        /// <summary>바닥에서 떠 있는 높이(px). 테스트가 들여다본다.</summary>
        internal double Lift
        {
            get { return this.lift; }
        }

        /// <summary>시간을 한 칸 흘린다. 테스트가 타이머를 기다리지 않게 한다.</summary>
        internal void Tick()
        {
            this.OnTick(this, EventArgs.Empty);
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

        /// <summary>진화 친밀도를 채우려면 포켓푸드가 몇 개 더 필요한지.</summary>
        public int FoodsLeft()
        {
            double left = (this.EvolvePetNeed - this.friendship) / PetWorld.FoodFriendship;
            return Math.Max(0, (int)Math.Ceiling(left));
        }

        /// <summary>이전 외부 코드와의 호환용 별칭.</summary>
        public int PetsLeft()
        {
            return this.FoodsLeft();
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

        /// <summary>쓰다듬었을 때. 친밀도와 별개로 하트 반응만 보여 준다.
        ///
        /// </summary>
        private void Petted()
        {
            this.SpawnEmote("heart");
        }

        /// <summary>포켓푸드로 친밀도와 누적되는 5분짜리 2배 산책 버프를 준다.</summary>
        public void Fed()
        {
            this.SpawnEmote("heart");
            this.foodBoostLeft += PetWorld.FoodBoostSeconds;
            if (this.evolving)
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
        private int referenceValue = 1;
        public Color GridColor = Color.FromArgb(240, 223, 196);
        public Color RiseColor = Color.FromArgb(47, 155, 103);
        public Color FallColor = Color.FromArgb(217, 52, 59);
        public Color TextColor = Color.FromArgb(170, 184, 205);

        public StockGraph()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(255, 253, 247);
        }

        public void SetValues(int[] source)
        {
            int[] safe = source == null || source.Length == 0 ? new int[] { 1 } : source;
            this.SetValues(safe, safe[0]);
        }

        public void SetValues(int[] source, int reference)
        {
            this.values = source == null || source.Length == 0 ? new int[] { 1 } : source;
            this.referenceValue = reference > 0 ? reference : this.values[0];
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int low = Math.Min(this.values[0], this.referenceValue);
            int high = Math.Max(this.values[0], this.referenceValue);
            for (int i = 1; i < this.values.Length; i++)
            {
                low = Math.Min(low, this.values[i]);
                high = Math.Max(high, this.values[i]);
            }
            int range = Math.Max(1, high - low);
            int margin = Math.Max(50, range / 4);
            low -= margin;
            high += margin;
            int spread = Math.Max(1, high - low);
            int plotTop = 29;
            int plotBottom = Math.Max(plotTop + 20, this.Height - 24);
            int plotHeight = plotBottom - plotTop;
            using (Pen grid = new Pen(this.GridColor))
            {
                for (int i = 1; i < 4; i++)
                {
                    int y = plotTop + plotHeight * i / 4;
                    e.Graphics.DrawLine(grid, 0, y, this.Width, y);
                }
            }
            int referenceY = plotBottom - plotHeight * (this.referenceValue - low) / spread;
            using (Pen referencePen = new Pen(this.TextColor))
            {
                referencePen.DashStyle = DashStyle.Dash;
                e.Graphics.DrawLine(referencePen, 0, referenceY, this.Width, referenceY);
            }
            Point[] points = new Point[this.values.Length];
            for (int i = 0; i < this.values.Length; i++)
            {
                int x = this.values.Length == 1 ? 4
                    : 4 + (this.Width - 8) * i / (this.values.Length - 1);
                int y = plotBottom - plotHeight * (this.values[i] - low) / spread;
                points[i] = new Point(x, y);
            }
            Color line = this.values[this.values.Length - 1] >= this.values[0]
                ? this.RiseColor : this.FallColor;
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
            using (Font labelFont = UiFonts.Create(10.0f, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, "최근 20회", labelFont,
                    new Rectangle(0, 2, 100, 22), this.TextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, "최고 " + PetWorld.FormatWon(high - margin), labelFont,
                    new Rectangle(this.Width - 170, 2, 170, 22), this.TextColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, "장 시작 " + PetWorld.FormatWon(this.referenceValue), labelFont,
                    new Rectangle(0, this.Height - 22, 200, 20), this.TextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, "최저 " + PetWorld.FormatWon(low + margin), labelFont,
                    new Rectangle(this.Width - 170, this.Height - 22, 170, 20), this.TextColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                if (this.values.Length == 1)
                {
                    TextRenderer.DrawText(e.Graphics, "가격 데이터를 모으는 중입니다", labelFont,
                        new Rectangle(0, referenceY - 28, this.Width, 22), this.TextColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    /// <summary>주식 거래와 최근 가격 흐름을 보여 주는 포켓몬풍 오버레이.</summary>
    public class StockOverlayForm : Form
    {
        private static readonly Color MenuRed = Color.FromArgb(238, 89, 96);
        private static readonly Color MenuRise = Color.FromArgb(255, 122, 133);
        private static readonly Color MenuRedDark = Color.FromArgb(183, 46, 54);
        private static readonly Color MenuBlue = Color.FromArgb(90, 167, 243);
        private static readonly Color MenuInk = Color.FromArgb(238, 244, 255);
        private static readonly Color MenuMuted = Color.FromArgb(170, 184, 205);
        private static readonly Color MenuPaper = Color.FromArgb(24, 34, 54);
        private static readonly Color MenuPanel = Color.FromArgb(32, 45, 67);
        private static readonly Color MenuSoft = Color.FromArgb(44, 57, 80);
        private static readonly Color MenuLine = Color.FromArgb(69, 83, 106);
        private static readonly Color MenuGreen = Color.FromArgb(84, 201, 149);
        private static readonly Color MenuYellow = Color.FromArgb(233, 189, 57);
        private readonly PetWorld world;
        private Label balance;
        private Label notice;
        private Label updateHint;
        private readonly Panel[] tossRows = new Panel[PetWorld.StockSlotCount];
        private readonly Label[] tossNames = new Label[PetWorld.StockSlotCount];
        private readonly Label[] tossHoldings = new Label[PetWorld.StockSlotCount];
        private readonly Label[] tossPrices = new Label[PetWorld.StockSlotCount];
        private readonly Label[] tossChanges = new Label[PetWorld.StockSlotCount];
        private readonly Panel[] tossRowAccents = new Panel[PetWorld.StockSlotCount];
        private Button tossAllStocksTab;
        private Button tossOwnedStocksTab;
        private bool tossOwnedOnly;
        private Label tossCashValue;
        private Label tossPortfolioValue;
        private Label tossPortfolioProfit;
        private Label tossPortfolioPercent;
        private Label tossDetailName;
        private Label tossDetailPrice;
        private Label tossDetailChange;
        private Label tossDetailMeta;
        private Label tossDetailHolding;
        private Label tossDetailProfitPercent;
        private GameCardPanel tossEventCard;
        private Label tossEventTitle;
        private Label tossEventPercent;
        private Label tossDetailEvent;
        private StockGraph tossGraph;
        private NumericUpDown tossQuantity;
        private GameActionButton tossBuyTab;
        private GameActionButton tossSellTab;
        private GameActionButton tossAction;
        private Label tossOrderSummary;
        private GameCardPanel tossToast;
        private Label tossToastTitle;
        private Label tossToastDetail;
        private Timer tossToastTimer;
        private bool tossBuying = true;
        private int selectedStock;
        private Point dragCursor;
        private Point dragLocation;
        private bool dragging;

        public StockOverlayForm(PetWorld world)
        {
            this.world = world;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96.0f, 96.0f);
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = MenuInk;
            this.Font = UiFonts.Create(9.0f);
            this.Padding = new Padding(3);
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            bool compact = workArea.Width < 840 || workArea.Height < 920;
            this.ClientSize = compact
                ? new Size(Math.Max(420, workArea.Width - 20), Math.Max(420, workArea.Height - 20))
                : new Size(814, 894);
            this.AutoScroll = compact;
            this.AutoScrollMinSize = compact ? new Size(814, 894) : Size.Empty;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    this.Close();
                    return;
                }
                if (e.Control && e.KeyCode == Keys.B)
                {
                    this.SetTossTradeMode(true); this.tossAction.Focus(); e.Handled = true;
                    return;
                }
                if (e.Control && e.KeyCode == Keys.S)
                {
                    this.SetTossTradeMode(false); this.tossAction.Focus(); e.Handled = true;
                    return;
                }
                if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6)
                {
                    int index = (int)e.KeyCode - (int)Keys.D1;
                    if (index < PetWorld.StockSlotCount)
                    {
                        this.selectedStock = index;
                        this.tossOwnedOnly = false;
                        this.tossRows[index].Focus();
                        this.RefreshTossMarket();
                    }
                    e.Handled = true;
                }
            };
            this.BuildTossLayout();
        }

        private void BuildTossLayout()
        {
            Panel body = new Panel();
            body.BackColor = MenuPaper;
            body.Location = new Point(3, 3);
            body.Size = new Size(808, 888);
            this.Controls.Add(body);

            Panel header = new Panel();
            header.BackColor = MenuRed;
            header.Location = new Point(0, 0);
            header.Size = new Size(808, 46);
            header.MouseDown += this.BeginDrag;
            header.MouseMove += this.Drag;
            header.MouseUp += this.EndDrag;
            body.Controls.Add(header);
            Label title = new Label();
            title.Text = "포켓몬 주식시장";
            title.ForeColor = Color.White;
            title.BackColor = header.BackColor;
            title.Font = UiFonts.Create(13.0f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(16, 9);
            header.Controls.Add(title);
            this.updateHint = new Label();
            this.updateHint.ForeColor = Color.FromArgb(252, 225, 226);
            this.updateHint.BackColor = header.BackColor;
            this.updateHint.Font = UiFonts.Create(10.0f, FontStyle.Bold);
            this.updateHint.AutoSize = false;
            this.updateHint.Location = new Point(350, 7);
            this.updateHint.Size = new Size(400, 32);
            this.updateHint.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(this.updateHint);
            Button close = new Button();
            close.Text = "×";
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.BackColor = MenuRedDark;
            close.ForeColor = Color.White;
            close.Font = UiFonts.Create(13.0f, FontStyle.Bold);
            close.Location = new Point(764, 4);
            close.Size = new Size(36, 36);
            close.AccessibleName = "주식창 닫기";
            close.Click += delegate { this.Close(); };
            header.Controls.Add(close);

            GameCardPanel portfolio = new GameCardPanel();
            portfolio.BackColor = MenuPanel;
            portfolio.BorderColor = MenuLine; portfolio.CornerRadius = 14;
            portfolio.Location = new Point(12, 56);
            portfolio.Size = new Size(784, 100);
            body.Controls.Add(portfolio);
            // 주식창에서 가장 궁금한 값은 "지금 얼마인가"(평가액)이므로 맨 앞의 큰
            // 자리에 둔다. 투자 원금은 그것과 비교해 보는 값이라 뒤로 보낸다.
            Label stockCaption = TossLabel(portfolio, new Point(16, 7), new Size(250, 20),
                ContentAlignment.MiddleLeft, 10.0f, FontStyle.Bold);
            stockCaption.Text = "주식 평가액"; stockCaption.ForeColor = MenuMuted;
            this.tossPortfolioValue = new Label();
            this.tossPortfolioValue.BackColor = portfolio.BackColor;
            this.tossPortfolioValue.ForeColor = MenuInk;
            this.tossPortfolioValue.Font = UiFonts.Create(17.0f, FontStyle.Bold);
            this.tossPortfolioValue.Location = new Point(16, 27);
            this.tossPortfolioValue.Size = new Size(190, 34);
            this.tossPortfolioValue.AccessibleName = "주식 평가액";
            portfolio.Controls.Add(this.tossPortfolioValue);
            this.tossPortfolioProfit = TossLabel(portfolio, new Point(206, 33), new Size(92, 25),
                ContentAlignment.MiddleLeft, 11.0f, FontStyle.Bold);
            this.tossPortfolioProfit.AccessibleName = "주식 평가 손익 금액";
            this.tossPortfolioPercent = TossLabel(portfolio, new Point(298, 33), new Size(72, 25),
                ContentAlignment.MiddleLeft, 11.0f, FontStyle.Bold);
            this.tossPortfolioPercent.AccessibleName = "주식 평가 수익률";
            Label cashCaption = TossLabel(portfolio, new Point(400, 8), new Size(140, 20),
                ContentAlignment.MiddleLeft, 9.5f, FontStyle.Regular);
            cashCaption.Text = "보유 현금"; cashCaption.ForeColor = MenuMuted;
            this.tossCashValue = TossLabel(portfolio, new Point(400, 29), new Size(140, 29),
                ContentAlignment.MiddleLeft, 12.0f, FontStyle.Bold);
            this.tossCashValue.AccessibleName = "보유 현금";
            Label portfolioTitle = TossLabel(portfolio, new Point(560, 8), new Size(208, 20),
                ContentAlignment.MiddleLeft, 9.5f, FontStyle.Regular);
            portfolioTitle.Text = "투자 원금";
            portfolioTitle.ForeColor = MenuMuted;
            this.balance = TossLabel(portfolio, new Point(560, 29), new Size(208, 29),
                ContentAlignment.MiddleLeft, 12.0f, FontStyle.Bold);
            this.balance.AccessibleName = "투자 원금";
            this.notice = new Label();
            this.notice.BackColor = portfolio.BackColor;
            this.notice.ForeColor = MenuMuted;
            this.notice.Font = UiFonts.Create(10.0f, FontStyle.Bold);
            this.notice.Location = new Point(16, 70);
            this.notice.Size = new Size(752, 22);
            portfolio.Controls.Add(this.notice);

            GameCardPanel watch = new GameCardPanel();
            watch.BackColor = MenuPanel;
            watch.BorderColor = MenuLine; watch.CornerRadius = 14;
            watch.Location = new Point(12, 166);
            watch.Size = new Size(250, 710);
            body.Controls.Add(watch);
            this.tossAllStocksTab = CreateQuickButton("전체");
            this.tossAllStocksTab.Location = new Point(8, 6);
            this.tossAllStocksTab.Size = new Size(52, 30);
            this.tossAllStocksTab.Click += delegate { this.SetTossStockFilter(false); };
            this.tossAllStocksTab.AccessibleName = "전체 종목 보기";
            watch.Controls.Add(this.tossAllStocksTab);
            this.tossOwnedStocksTab = CreateQuickButton("보유");
            this.tossOwnedStocksTab.Location = new Point(64, 6);
            this.tossOwnedStocksTab.Size = new Size(60, 30);
            this.tossOwnedStocksTab.Click += delegate { this.SetTossStockFilter(true); };
            this.tossOwnedStocksTab.AccessibleName = "보유 종목만 보기";
            watch.Controls.Add(this.tossOwnedStocksTab);
            Label watchPriceTitle = TossLabel(watch, new Point(154, 11), new Size(80, 20),
                ContentAlignment.MiddleRight, 9.5f, FontStyle.Bold);
            watchPriceTitle.Text = "현재가"; watchPriceTitle.ForeColor = MenuMuted;
            for (int i = 0; i < PetWorld.StockSlotCount; i++)
            {
                int rowIndex = i;
                Panel row = new KeyboardSelectionPanel();
                row.Location = new Point(6, 42 + i * 74);
                row.Size = new Size(236, 71);
                watch.Controls.Add(row);
                this.tossRows[i] = row;
                Panel accent = new Panel(); accent.Location = new Point(0, 0);
                accent.Size = new Size(4, 71); row.Controls.Add(accent);
                this.tossRowAccents[i] = accent;
                this.tossNames[i] = TossLabel(row, new Point(11, 8), new Size(125, 22),
                    ContentAlignment.MiddleLeft, 11.0f, FontStyle.Bold);
                this.tossHoldings[i] = TossLabel(row, new Point(11, 38), new Size(129, 20),
                    ContentAlignment.MiddleLeft, 10.0f, FontStyle.Regular);
                this.tossPrices[i] = TossLabel(row, new Point(140, 8), new Size(88, 22),
                    ContentAlignment.MiddleRight, 11.0f, FontStyle.Bold);
                this.tossChanges[i] = TossLabel(row, new Point(140, 38), new Size(88, 20),
                    ContentAlignment.MiddleRight, 10.0f, FontStyle.Bold);
                this.BindTossSelection(row, rowIndex);
                this.BindTossSelection(this.tossNames[i], rowIndex);
                this.BindTossSelection(this.tossHoldings[i], rowIndex);
                this.BindTossSelection(this.tossPrices[i], rowIndex);
                this.BindTossSelection(this.tossChanges[i], rowIndex);
                this.BindTossSelection(accent, rowIndex);
            }

            GameCardPanel detail = new GameCardPanel();
            detail.BackColor = MenuPanel;
            detail.BorderColor = MenuLine; detail.CornerRadius = 14;
            detail.Location = new Point(274, 166);
            detail.Size = new Size(522, 710);
            body.Controls.Add(detail);
            this.tossDetailName = TossLabel(detail, new Point(16, 10), new Size(490, 29),
                ContentAlignment.MiddleLeft, 15.0f, FontStyle.Bold);
            this.tossDetailName.AccessibleName = "선택 종목 이름";
            this.tossDetailPrice = TossLabel(detail, new Point(16, 41), new Size(280, 41),
                ContentAlignment.MiddleLeft, 22.0f, FontStyle.Bold);
            this.tossDetailPrice.AccessibleName = "현재 가격";
            this.tossDetailChange = TossLabel(detail, new Point(298, 47), new Size(208, 30),
                ContentAlignment.MiddleRight, 12.0f, FontStyle.Bold);
            this.tossDetailChange.AccessibleName = "장 시작 대비 등락률";
            this.tossDetailMeta = TossLabel(detail, new Point(16, 82), new Size(490, 44),
                ContentAlignment.MiddleLeft, 10.0f, FontStyle.Regular);
            this.tossDetailMeta.AccessibleName = "종목 성향과 위험도";
            this.tossGraph = new StockGraph();
            this.tossGraph.BackColor = MenuPanel;
            this.tossGraph.GridColor = MenuLine;
            this.tossGraph.RiseColor = MenuRise;
            this.tossGraph.FallColor = MenuBlue;
            this.tossGraph.Location = new Point(14, 128);
            this.tossGraph.Size = new Size(494, 166);
            detail.Controls.Add(this.tossGraph);
            this.tossDetailHolding = TossLabel(detail, new Point(16, 302), new Size(490, 68),
                ContentAlignment.MiddleLeft, 11.0f, FontStyle.Bold);
            this.tossDetailHolding.BackColor = MenuSoft;
            this.tossDetailProfitPercent = TossLabel(detail, new Point(414, 327), new Size(80, 24),
                ContentAlignment.MiddleRight, 11.0f, FontStyle.Bold);
            this.tossDetailProfitPercent.BackColor = MenuSoft;
            this.tossDetailProfitPercent.AccessibleName = "선택 종목 보유 수익률";
            this.tossEventCard = new GameCardPanel();
            this.tossEventCard.BackColor = Color.FromArgb(40, 61, 90);
            this.tossEventCard.BorderColor = MenuLine; this.tossEventCard.CornerRadius = 9;
            this.tossEventCard.BorderThickness = 1;
            this.tossEventCard.Location = new Point(16, 378);
            this.tossEventCard.Size = new Size(490, 62);
            detail.Controls.Add(this.tossEventCard);
            this.tossEventTitle = TossLabel(this.tossEventCard, new Point(12, 5), new Size(462, 21),
                ContentAlignment.MiddleLeft, 9.5f, FontStyle.Bold);
            this.tossEventTitle.BackColor = this.tossEventCard.BackColor;
            this.tossEventTitle.ForeColor = MenuYellow;
            this.tossDetailEvent = TossLabel(this.tossEventCard, new Point(12, 27), new Size(462, 28),
                ContentAlignment.TopLeft, 10.0f, FontStyle.Regular);
            this.tossDetailEvent.BackColor = this.tossEventCard.BackColor;
            this.tossDetailEvent.ForeColor = MenuInk;
            this.tossEventPercent = TossLabel(this.tossEventCard, new Point(390, 5), new Size(84, 21),
                ContentAlignment.MiddleRight, 10.0f, FontStyle.Bold);
            this.tossEventPercent.BackColor = this.tossEventCard.BackColor;
            this.tossEventPercent.AccessibleName = "이벤트 등락률";
            GameCardPanel orderCard = new GameCardPanel();
            orderCard.BackColor = MenuSoft; orderCard.BorderColor = MenuLine;
            orderCard.CornerRadius = 11; orderCard.BorderThickness = 1;
            orderCard.Location = new Point(16, 448); orderCard.Size = new Size(490, 210);
            detail.Controls.Add(orderCard);
            this.tossBuyTab = CreateSegmentButton("매수", true);
            this.tossBuyTab.Location = new Point(12, 10); this.tossBuyTab.Size = new Size(226, 36);
            this.tossBuyTab.Click += delegate { this.SetTossTradeMode(true); };
            orderCard.Controls.Add(this.tossBuyTab);
            this.tossSellTab = CreateSegmentButton("매도", false);
            this.tossSellTab.Location = new Point(252, 10); this.tossSellTab.Size = new Size(226, 36);
            this.tossSellTab.Click += delegate { this.SetTossTradeMode(false); };
            orderCard.Controls.Add(this.tossSellTab);
            Label quantityLabel = TossLabel(orderCard, new Point(14, 55), new Size(76, 29),
                ContentAlignment.MiddleLeft, 10.0f, FontStyle.Bold);
            quantityLabel.Text = "주문 수량";
            quantityLabel.BackColor = MenuSoft;
            this.tossQuantity = new NumericUpDown();
            this.tossQuantity.Minimum = 1;
            this.tossQuantity.Maximum = PetWorld.StockMaxOrderQuantity;
            this.tossQuantity.Value = 1;
            this.tossQuantity.Font = UiFonts.Create(11.0f, FontStyle.Bold);
            this.tossQuantity.TextAlign = HorizontalAlignment.Center;
            this.tossQuantity.BackColor = MenuPanel;
            this.tossQuantity.ForeColor = MenuInk;
            this.tossQuantity.Location = new Point(92, 57);
            this.tossQuantity.Size = new Size(100, 27);
            this.tossQuantity.ValueChanged += delegate { this.RefreshTossMarket(); };
            this.tossQuantity.AccessibleName = "주문 수량";
            this.tossQuantity.AccessibleDescription = "주문 가능한 수량을 직접 입력할 수 있습니다.";
            orderCard.Controls.Add(this.tossQuantity);
            int[] quickAmounts = { 1, 5, 10 };
            for (int i = 0; i < quickAmounts.Length; i++)
            {
                int amount = quickAmounts[i];
                Button quick = CreateQuickButton(amount.ToString());
                quick.Location = new Point(204 + i * 46, 56);
                quick.Size = new Size(42, 29);
                quick.Click += delegate { this.SetTossQuantity(amount); };
                orderCard.Controls.Add(quick);
            }
            Button maximum = CreateQuickButton("최대");
            maximum.Location = new Point(346, 56);
            maximum.Size = new Size(58, 29);
            maximum.Click += delegate { this.SetTossQuantity(this.MaximumTossQuantity()); };
            orderCard.Controls.Add(maximum);
            this.tossOrderSummary = TossLabel(orderCard, new Point(14, 91), new Size(462, 48),
                ContentAlignment.MiddleLeft, 10.0f, FontStyle.Regular);
            this.tossOrderSummary.BackColor = MenuSoft;
            this.tossAction = (GameActionButton)CreateActionButton("매수하기", MenuRed);
            this.tossAction.Location = new Point(14, 144);
            this.tossAction.Size = new Size(462, 52);
            this.tossAction.Click += delegate { this.TradeToss(this.tossBuying); };
            this.tossAction.AccessibleName = "주식 주문 실행";
            orderCard.Controls.Add(this.tossAction);
            this.tossToast = new GameCardPanel();
            this.tossToast.BackColor = Color.FromArgb(37, 72, 67);
            this.tossToast.BorderColor = MenuGreen; this.tossToast.CornerRadius = 11;
            this.tossToast.Location = new Point(294, 65);
            this.tossToast.Size = new Size(500, 72);
            this.tossToast.Visible = false;
            body.Controls.Add(this.tossToast);
            this.tossToastTitle = TossLabel(this.tossToast, new Point(14, 7), new Size(468, 24),
                ContentAlignment.MiddleLeft, 11.0f, FontStyle.Bold);
            this.tossToastTitle.BackColor = this.tossToast.BackColor;
            this.tossToastTitle.ForeColor = MenuGreen;
            this.tossToastDetail = TossLabel(this.tossToast, new Point(14, 34), new Size(468, 28),
                ContentAlignment.MiddleLeft, 10.0f, FontStyle.Regular);
            this.tossToastDetail.BackColor = this.tossToast.BackColor;
            this.tossToastTimer = new Timer(); this.tossToastTimer.Interval = 2800;
            this.tossToastTimer.Tick += delegate {
                this.tossToastTimer.Stop(); this.tossToast.Visible = false;
            };
            this.FormClosed += delegate {
                if (this.tossToastTimer != null) { this.tossToastTimer.Stop(); this.tossToastTimer.Dispose(); }
            };
            this.RefreshTossMarket();
        }

        private static Label TossLabel(Control parent, Point location, Size size,
            ContentAlignment alignment, float fontSize, FontStyle style)
        {
            Label label = new Label();
            label.BackColor = MenuPanel;
            label.ForeColor = MenuInk;
            label.Font = UiFonts.Create(fontSize, style);
            label.Location = location;
            label.Size = size;
            label.TextAlign = alignment;
            label.AutoEllipsis = true;
            parent.Controls.Add(label);
            return label;
        }

        private void BindTossSelection(Control control, int index)
        {
            control.Click += delegate {
                this.selectedStock = index;
                this.ActiveControl = null;
                this.RefreshTossMarket();
            };
            KeyboardSelectionPanel row = control as KeyboardSelectionPanel;
            if (row == null) return;
            row.AccessibleName = "주식 종목 " + (index + 1);
            row.AccessibleDescription = "Enter로 선택하고 방향키로 종목을 이동합니다.";
            row.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    this.selectedStock = index;
                    this.RefreshTossMarket();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Left
                    || e.KeyCode == Keys.Down || e.KeyCode == Keys.Right)
                {
                    int step = e.KeyCode == Keys.Up || e.KeyCode == Keys.Left ? -1 : 1;
                    this.selectedStock = (index + step + PetWorld.StockSlotCount) % PetWorld.StockSlotCount;
                    this.tossRows[this.selectedStock].Focus();
                    this.RefreshTossMarket();
                    e.Handled = true;
                }
            };
        }

        private void SetTossQuantity(int quantity)
        {
            this.tossQuantity.Value = Math.Min(PetWorld.StockMaxOrderQuantity, Math.Max(1, quantity));
            this.RefreshTossMarket();
        }

        private void SetTossTradeMode(bool buying)
        {
            this.tossBuying = buying;
            this.RefreshTossMarket();
        }

        private void SetTossStockFilter(bool ownedOnly)
        {
            if (ownedOnly && this.world.Options.StockShares[this.selectedStock] <= 0)
            {
                int firstOwned = -1;
                for (int i = 0; i < PetWorld.StockSlotCount; i++)
                    if (this.world.Options.StockShares[i] > 0) { firstOwned = i; break; }
                if (firstOwned < 0) return;
                this.selectedStock = firstOwned;
            }
            this.tossOwnedOnly = ownedOnly;
            this.RefreshTossMarket();
        }

        private void ApplyTossTradeMode()
        {
            this.tossBuyTab.BackColor = this.tossBuying ? MenuRed : MenuPanel;
            this.tossBuyTab.ForeColor = this.tossBuying ? Color.White : MenuMuted;
            this.tossBuyTab.EdgeColor = this.tossBuying ? MenuRed : MenuLine;
            this.tossSellTab.BackColor = this.tossBuying ? MenuPanel : MenuBlue;
            this.tossSellTab.ForeColor = this.tossBuying ? MenuMuted : Color.White;
            this.tossSellTab.EdgeColor = this.tossBuying ? MenuLine : MenuBlue;
            this.tossAction.BackColor = this.tossBuying ? MenuRed : MenuBlue;
            this.tossAction.DepthColor = this.tossBuying ? MenuRedDark : Color.FromArgb(48, 111, 168);
            this.tossBuyTab.Invalidate(); this.tossSellTab.Invalidate(); this.tossAction.Invalidate();
        }

        private void RefreshTossEvent(int index)
        {
            string title;
            string text;
            Color background = Color.FromArgb(40, 61, 90);
            Color border = MenuLine;
            Color titleColor = MenuMuted;
            if (!this.world.MarketIsOpen)
            {
                title = "●  시장 휴장";
                text = this.world.MarketSessionText + " · 재개 후 주문할 수 있습니다.";
            }
            else if (this.world.IsStockHalted(index))
            {
                title = "●  선택 종목 거래 정지";
                text = "변동성 완화장치 작동 중 · "
                    + this.world.Options.StockHaltSeconds[index] + "초 후 거래 재개";
                background = Color.FromArgb(67, 48, 66);
                border = MenuRise; titleColor = MenuRise;
            }
            else if (string.IsNullOrEmpty(this.world.StockEvent))
            {
                title = "●  시장 알림";
                text = "새 이벤트를 기다리는 중입니다.";
            }
            else if (this.world.StockEvent.IndexOf(this.world.StockName(index),
                StringComparison.Ordinal) >= 0)
            {
                title = "●  선택 종목 이벤트";
                text = this.world.StockEvent;
                background = Color.FromArgb(58, 55, 73);
                border = MenuYellow; titleColor = MenuYellow;
            }
            else
            {
                title = "●  전체 시장 이벤트";
                text = this.world.StockEvent + " · 선택 종목과 직접 관련 없는 소식";
                titleColor = MenuYellow;
            }
            double eventPercent;
            string eventPercentText = ExtractSignedPercent(ref text, out eventPercent);
            this.tossEventCard.BackColor = background;
            this.tossEventCard.BorderColor = border;
            this.tossEventTitle.BackColor = background;
            this.tossDetailEvent.BackColor = background;
            this.tossEventPercent.BackColor = background;
            this.tossEventTitle.ForeColor = titleColor;
            this.tossEventTitle.Text = title;
            this.tossEventPercent.Text = eventPercentText;
            this.tossEventPercent.ForeColor = string.IsNullOrEmpty(eventPercentText)
                ? MenuMuted : PercentColor(eventPercent);
            this.tossDetailEvent.Text = text;
            this.tossDetailEvent.ForeColor = MenuInk;
            this.tossEventCard.Invalidate();
        }

        private static string ExtractSignedPercent(ref string text, out double value)
        {
            value = 0.0;
            string[] tokens = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (token.Length <= 2 || token[token.Length - 1] != '%'
                    || (token[0] != '+' && token[0] != '-')) continue;
                double parsed;
                if (!double.TryParse(token.Substring(0, token.Length - 1), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out parsed)) continue;
                value = parsed;
                text = text.Replace(token, "");
                while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
                    text = text.Replace("  ", " ");
                text = text.Trim();
                return token;
            }
            return "";
        }

        private int MaximumTossQuantity()
        {
            int maximum = this.tossBuying
                ? this.world.StockMaximumBuyQuantity(this.selectedStock)
                : this.world.StockMaximumSellQuantity(this.selectedStock);
            return Math.Max(1, maximum);
        }

        private void TradeToss(bool buying)
        {
            int quantity = (int)this.tossQuantity.Value;
            int index = this.selectedStock;
            long amount = (long)(buying ? this.world.StockBuyCost(index)
                : this.world.StockSellProceeds(index)) * quantity;
            if (this.world.IsStockDelisted(index))
            {
                this.ShowTossFeedback(false, "주문할 수 없습니다", "상장폐지된 종목입니다."); return;
            }
            if (!this.world.MarketIsOpen)
            {
                this.ShowTossFeedback(false, "지금은 휴장 중입니다", this.world.MarketSessionText); return;
            }
            if (this.world.IsStockHalted(index))
            {
                this.ShowTossFeedback(false, "거래가 일시 정지됐습니다",
                    this.world.Options.StockHaltSeconds[index] + "초 후 다시 시도해 주세요."); return;
            }
            if (buying && this.world.Options.Coins < amount)
            {
                this.ShowTossFeedback(false, "보유금이 부족합니다",
                    PetWorld.FormatWon(amount - this.world.Options.Coins) + "이 더 필요합니다."); return;
            }
            if (!buying && this.world.Options.StockShares[index] < quantity)
            {
                this.ShowTossFeedback(false, "보유 수량이 부족합니다",
                    "현재 " + this.world.Options.StockShares[index] + "주를 보유하고 있습니다."); return;
            }
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
                this.RefreshTossMarket();
                this.ShowTossFeedback(true, "매수 완료 · " + this.world.StockName(index) + " " + quantity + "주",
                    PetWorld.FormatWon(amount) + " · 남은 현금 " + PetWorld.FormatWon(this.world.Options.Coins));
            }
            else
            {
                this.world.SellStock(index, quantity);
                this.RefreshTossMarket();
                this.ShowTossFeedback(true, "매도 완료 · " + this.world.StockName(index) + " " + quantity + "주",
                    PetWorld.FormatWon(amount) + " · 남은 보유 "
                    + this.world.Options.StockShares[index] + "주");
            }
        }

        private void ShowTossFeedback(bool success, string title, string detail)
        {
            Color background = success ? Color.FromArgb(37, 72, 67) : Color.FromArgb(67, 48, 66);
            Color accent = success ? MenuGreen : MenuRise;
            this.tossToast.BackColor = background;
            this.tossToast.BorderColor = accent;
            this.tossToastTitle.BackColor = background;
            this.tossToastDetail.BackColor = background;
            this.tossToastTitle.ForeColor = accent;
            this.tossToastTitle.Text = (success ? "✓  " : "!  ") + title;
            this.tossToastDetail.Text = detail;
            this.tossToast.AccessibleName = title;
            this.tossToast.AccessibleDescription = detail;
            this.tossToast.Visible = true;
            this.tossToast.BringToFront();
            this.tossToast.Invalidate();
            this.tossToastTimer.Stop(); this.tossToastTimer.Start();
        }

        private void UpdateTossActionAccessibility()
        {
            this.tossAction.AccessibleName = this.tossAction.Text.Replace("\r", " ").Replace("\n", " ");
            this.tossAction.AccessibleDescription = this.tossOrderSummary.Text
                .Replace("\r", " ").Replace("\n", " ");
        }

        private static Color PercentColor(double value)
        {
            if (value > 0.0) return MenuRise;
            if (value < 0.0) return MenuBlue;
            return MenuMuted;
        }

        private void RefreshTossMarket()
        {
            this.ApplyTossTradeMode();
            int portfolio = this.world.StockPortfolioValue();
            int portfolioProfit = this.world.StockPortfolioProfit();
            double portfolioPercent = this.world.StockPortfolioChangePercent();
            this.balance.Text = PetWorld.FormatWon(this.world.StockPortfolioCostBasis());
            this.tossCashValue.Text = PetWorld.FormatWon(this.world.Options.Coins);
            this.tossPortfolioValue.Text = PetWorld.FormatWon(portfolio);
            this.tossPortfolioProfit.Text = PetWorld.FormatSignedWon(portfolioProfit);
            this.tossPortfolioProfit.ForeColor = PercentColor(portfolioPercent);
            this.tossPortfolioPercent.Text = string.Format("{0:+0.0;-0.0;0.0}%", portfolioPercent);
            this.tossPortfolioPercent.ForeColor = PercentColor(portfolioPercent);
            this.notice.Text = this.world.MarketMoverSummary;
            int ownedCount = 0;
            for (int i = 0; i < PetWorld.StockSlotCount; i++)
                if (this.world.Options.StockShares[i] > 0) ownedCount++;
            this.tossAllStocksTab.Text = "전체 " + PetWorld.StockSlotCount;
            this.tossOwnedStocksTab.Text = "보유 " + ownedCount;
            this.tossOwnedStocksTab.Enabled = ownedCount > 0;
            this.tossAllStocksTab.BackColor = this.tossOwnedOnly ? MenuPanel : MenuSoft;
            this.tossAllStocksTab.ForeColor = this.tossOwnedOnly ? MenuMuted : MenuInk;
            this.tossOwnedStocksTab.BackColor = this.tossOwnedOnly ? MenuSoft : MenuPanel;
            this.tossOwnedStocksTab.ForeColor = this.tossOwnedOnly ? MenuInk : MenuMuted;
            this.updateHint.Text = this.world.MarketSessionText + (this.world.MarketIsOpen
                ? " · " + this.world.MarketSecondsLeft + "초 후 갱신" : "");
            int visibleRow = 0;
            for (int i = 0; i < PetWorld.StockSlotCount; i++)
            {
                bool visible = !this.tossOwnedOnly || this.world.Options.StockShares[i] > 0;
                this.tossRows[i].Visible = visible;
                if (visible)
                {
                    this.tossRows[i].Location = new Point(6, 36 + visibleRow * 66);
                    visibleRow++;
                }
                bool selected = i == this.selectedStock;
                Color background = selected ? MenuSoft : MenuPanel;
                double delta = this.world.StockChangePercent(i);
                Color trend = PercentColor(delta);
                this.tossRows[i].BackColor = background;
                this.tossNames[i].BackColor = background;
                this.tossHoldings[i].BackColor = background;
                this.tossPrices[i].BackColor = background;
                this.tossChanges[i].BackColor = background;
                this.tossRowAccents[i].BackColor = selected ? MenuRise : background;
                this.tossNames[i].Text = this.world.StockName(i);
                this.tossRows[i].AccessibleName = this.world.StockName(i) + " "
                    + (this.world.IsStockDelisted(i) ? "상장폐지" : PetWorld.FormatWon(this.world.Options.StockPrices[i])
                        + " " + string.Format(CultureInfo.InvariantCulture, "{0:+0.0;-0.0;0.0}%", delta));
                this.tossNames[i].ForeColor = MenuInk;
                this.tossHoldings[i].Text = this.world.Options.StockShares[i] > 0
                    ? "보유 " + this.world.Options.StockShares[i] + "주"
                    : this.world.StockPrimaryProfile(i);
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
            Color detailTrend = PercentColor(percent);
            Color detailPriceColor = percent == 0.0 ? MenuInk : detailTrend;
            this.tossDetailName.Text = this.world.StockName(index);
            this.tossGraph.SetValues(this.world.StockHistory(index),
                this.world.StockSessionOpeningPrice(index));
            this.RefreshTossEvent(index);
            if (this.world.IsStockDelisted(index))
            {
                this.tossDetailPrice.Text = "상장폐지";
                this.tossDetailPrice.ForeColor = Color.FromArgb(217, 52, 59);
                this.tossDetailChange.Text = "신규 상장 대기";
                this.tossDetailChange.ForeColor = MenuMuted;
                this.tossDetailMeta.Text = "신규 상장까지 " + this.world.RelistingMinutes(index) + "분";
                this.tossDetailHolding.Text = "보유 주식은 소멸했습니다. 새 종목 상장을 기다려 주세요.";
                this.tossDetailProfitPercent.Text = "";
                this.tossDetailProfitPercent.ForeColor = MenuMuted;
                this.tossQuantity.Enabled = false;
                this.tossOrderSummary.Text = "상장폐지 종목은 주문할 수 없습니다.";
                this.tossAction.Text = "주문할 수 없습니다";
                this.tossAction.Enabled = false;
                this.UpdateTossActionAccessibility();
                return;
            }
            this.tossDetailPrice.Text = PetWorld.FormatWon(price);
            this.tossDetailPrice.ForeColor = detailPriceColor;
            this.tossDetailChange.Text = string.Format("장 시작 대비  {0:+0.0;-0.0;0.0}%", percent);
            this.tossDetailChange.ForeColor = detailTrend;
            this.tossDetailMeta.Text = this.world.StockProfile(index)
                + " · 위험도 " + this.world.StockRiskLabel(index)
                + " · 기본 변동폭 ±" + this.world.StockVolatilityText(index) + "%\r\n"
                + this.world.StockProfileDescription(index);
            this.tossDetailMeta.AccessibleDescription = this.tossDetailMeta.Text
                .Replace("\r", " ").Replace("\n", " ");
            double profitPercent = this.world.StockProfitPercent(index);
            this.tossDetailHolding.Text = this.world.StockPositionText(index, false);
            this.tossDetailProfitPercent.Text = this.world.Options.StockShares[index] > 0
                ? string.Format("{0:+0.0;-0.0;0.0}%", profitPercent) : "";
            this.tossDetailProfitPercent.ForeColor = PercentColor(profitPercent);
            if (!this.world.MarketIsOpen)
            {
                this.tossQuantity.Enabled = false;
                this.tossOrderSummary.Text = "휴장 중에는 주문할 수 없습니다.";
                this.tossAction.Text = "휴장 중 · 주문 불가";
                this.tossAction.Enabled = false;
                this.UpdateTossActionAccessibility();
                return;
            }
            if (this.world.IsStockHalted(index))
            {
                this.tossQuantity.Enabled = false;
                this.tossOrderSummary.Text = "변동성 완화장치가 해제되면 주문할 수 있습니다.";
                this.tossAction.Text = "거래 정지 · 주문 불가";
                this.tossAction.Enabled = false;
                this.UpdateTossActionAccessibility();
                return;
            }
            int quantity = (int)this.tossQuantity.Value;
            this.tossQuantity.Enabled = true;
            long gross = (long)price * quantity;
            long amount = (long)(this.tossBuying ? this.world.StockBuyCost(index)
                : this.world.StockSellProceeds(index)) * quantity;
            long fee = Math.Abs(amount - gross);
            if (this.tossBuying)
            {
                int maximum = this.world.StockMaximumBuyQuantity(index);
                this.tossOrderSummary.Text = "주문금액 " + PetWorld.FormatWon(amount)
                    + "  ·  수수료 " + PetWorld.FormatWon(fee)
                    + "\r\n주문 후 현금 " + PetWorld.FormatWon(Math.Max(0, this.world.Options.Coins - amount))
                    + "  ·  이번 주문 최대 " + maximum + "주";
                bool affordable = this.world.Options.Coins >= amount;
                this.tossAction.Text = affordable
                    ? quantity + "주 매수하기\r\n" + PetWorld.FormatWon(amount)
                    : "보유금이 부족합니다\r\n" + PetWorld.FormatWon(amount) + " 필요";
                this.tossAction.Enabled = affordable;
            }
            else
            {
                int shares = this.world.Options.StockShares[index];
                this.tossOrderSummary.Text = "예상 수령액 " + PetWorld.FormatWon(amount)
                    + "  ·  수수료 " + PetWorld.FormatWon(fee)
                    + "\r\n현재 보유 " + shares + "주  ·  매도 후 " + Math.Max(0, shares - quantity)
                    + "주  ·  최대 " + this.world.StockMaximumSellQuantity(index) + "주";
                bool enough = shares >= quantity;
                this.tossAction.Text = enough
                    ? quantity + "주 매도하기\r\n" + PetWorld.FormatWon(amount)
                    : "보유 수량이 부족합니다";
                this.tossAction.Enabled = enough;
            }
            this.UpdateTossActionAccessibility();
        }


        private static Button CreateActionButton(string text, Color color)
        {
            GameActionButton button = new GameActionButton();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.EdgeColor = MenuInk;
            button.DepthColor = color == MenuRed ? MenuRedDark : Color.FromArgb(48, 111, 168);
            button.Font = UiFonts.Create(11.0f, FontStyle.Bold);
            button.Size = new Size(104, 43);
            return button;
        }

        private static GameActionButton CreateSegmentButton(string text, bool active)
        {
            GameActionButton button = new GameActionButton();
            button.Text = text;
            button.BackColor = active ? MenuRed : MenuPanel;
            button.ForeColor = active ? Color.White : MenuMuted;
            button.EdgeColor = active ? MenuRed : MenuLine;
            button.ShowDepth = false;
            button.CornerRadius = 8;
            button.Font = UiFonts.Create(11.0f, FontStyle.Bold);
            button.AccessibleName = text + " 주문 선택";
            return button;
        }

        private static Button CreateQuickButton(string text)
        {
            GameActionButton button = new GameActionButton();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = MenuSoft;
            button.ForeColor = MenuInk;
            button.EdgeColor = MenuLine;
            button.ShowDepth = false;
            button.Font = UiFonts.Create(9.0f, FontStyle.Bold);
            button.Size = new Size(23, 20);
            return button;
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
            this.RefreshTossMarket();
        }

    }

    internal static class GameUiDrawing
    {
        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>시안의 두꺼운 테두리와 둥근 모서리를 재현하는 게임 카드.</summary>
    internal class GameCardPanel : Panel
    {
        public Color BorderColor = Color.FromArgb(69, 83, 106);
        public int CornerRadius = 12;
        public int BorderThickness = 2;

        public GameCardPanel()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(this.Parent == null ? this.BackColor : this.Parent.BackColor);
            Rectangle box = new Rectangle(1, 1, Math.Max(1, this.Width - 3), Math.Max(1, this.Height - 3));
            using (GraphicsPath path = GameUiDrawing.RoundedRectangle(box, this.CornerRadius))
            using (SolidBrush fill = new SolidBrush(this.BackColor))
            using (Pen border = new Pen(this.BorderColor, this.BorderThickness)) {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    /// <summary>눌리는 깊이와 둥근 윤곽이 있는 게임식 동작 버튼.</summary>
    internal class GameActionButton : Button
    {
        private bool pointerDown;
        public Color EdgeColor = Color.FromArgb(238, 244, 255);
        public Color DepthColor = Color.Empty;
        public Color DisabledFaceColor = Color.Empty;
        public Color DisabledEdgeColor = Color.Empty;
        public int CornerRadius = 9;
        public bool ShowDepth = true;

        public GameActionButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseDown(MouseEventArgs e) { this.pointerDown = true; this.Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { this.pointerDown = false; this.Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseLeave(EventArgs e) { this.pointerDown = false; this.Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(this.Parent == null ? Color.FromArgb(32, 45, 67) : this.Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int depth = this.ShowDepth ? (this.pointerDown ? 1 : 4) : 1;
            int faceTop = this.pointerDown ? 3 : 1;
            int shapeWidth = Math.Max(1, this.Width - 4);
            int shapeHeight = Math.Max(1, this.Height - depth - faceTop - 2);
            Rectangle faceBox = new Rectangle(2, faceTop, shapeWidth, shapeHeight);
            Rectangle shadowBox = new Rectangle(2, faceTop + depth, shapeWidth, shapeHeight);
            Color face = this.Enabled ? this.BackColor : (this.DisabledFaceColor.IsEmpty
                ? Color.FromArgb(44, 57, 80) : this.DisabledFaceColor);
            Color edge = this.Enabled ? this.EdgeColor : (this.DisabledEdgeColor.IsEmpty
                ? Color.FromArgb(238, 244, 255) : this.DisabledEdgeColor);
            Color depthColor = this.DepthColor.IsEmpty ? edge : this.DepthColor;
            using (GraphicsPath shadow = GameUiDrawing.RoundedRectangle(shadowBox, this.CornerRadius))
            using (GraphicsPath front = GameUiDrawing.RoundedRectangle(faceBox, this.CornerRadius))
            using (SolidBrush shadowBrush = new SolidBrush(depthColor))
            using (SolidBrush faceBrush = new SolidBrush(face))
            using (Pen outline = new Pen(edge, 1.6f)) {
                outline.Alignment = PenAlignment.Inset;
                e.Graphics.FillPath(shadowBrush, shadow);
                e.Graphics.FillPath(faceBrush, front);
                e.Graphics.DrawPath(outline, front);
            }
            Rectangle textBox = Rectangle.FromLTRB(faceBox.Left + this.Padding.Left,
                faceBox.Top + this.Padding.Top, faceBox.Right - this.Padding.Right,
                faceBox.Bottom - this.Padding.Bottom);
            textBox.Offset(0, this.pointerDown ? 1 : -1);
            TextFormatFlags alignment = TextFormatFlags.HorizontalCenter;
            if (this.TextAlign == ContentAlignment.MiddleLeft || this.TextAlign == ContentAlignment.TopLeft
                || this.TextAlign == ContentAlignment.BottomLeft) alignment = TextFormatFlags.Left;
            else if (this.TextAlign == ContentAlignment.MiddleRight || this.TextAlign == ContentAlignment.TopRight
                || this.TextAlign == ContentAlignment.BottomRight) alignment = TextFormatFlags.Right;
            TextRenderer.DrawText(e.Graphics, this.Text, this.Font, textBox,
                this.Enabled ? this.ForeColor : Color.FromArgb(170, 184, 205),
                alignment | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
            if (this.Focused && this.ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,
                Rectangle.Inflate(faceBox, -5, -5));
        }
    }

    internal class KeyboardSelectionPanel : Panel
    {
        public Color FocusColor = Color.FromArgb(217, 52, 59);

        public KeyboardSelectionPanel()
        {
            this.SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            this.TabStop = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right
                || base.IsInputKey(keyData);
        }

        protected override void OnEnter(EventArgs e) { base.OnEnter(e); this.Invalidate(); }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); this.Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!this.Focused) return;
            using (Pen pen = new Pen(this.FocusColor, 2))
                e.Graphics.DrawRectangle(pen, 1, 1, Math.Max(1, this.Width - 3), Math.Max(1, this.Height - 3));
        }
    }

    internal class GamePillLabel : Label
    {
        public int CornerRadius = 9;
        public Color FillColor = Color.FromArgb(233, 189, 57);

        public GamePillLabel()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(this.Parent == null ? Color.FromArgb(238, 89, 96) : this.Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle box = new Rectangle(0, 0, Math.Max(1, this.Width - 1), Math.Max(1, this.Height - 1));
            using (GraphicsPath path = GameUiDrawing.RoundedRectangle(box, this.CornerRadius))
            using (SolidBrush fill = new SolidBrush(this.FillColor)) e.Graphics.FillPath(fill, path);
            TextRenderer.DrawText(e.Graphics, this.Text, this.Font, box, this.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class GameHeaderButton : Button
    {
        public GameHeaderButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); this.Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); this.Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(this.Parent == null ? Color.FromArgb(238, 89, 96) : this.Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle box = new Rectangle(1, 1, Math.Max(1, this.Width - 3), Math.Max(1, this.Height - 3));
            Color fillColor = this.ClientRectangle.Contains(this.PointToClient(Cursor.Position))
                ? Color.FromArgb(90, 71, 20, 24) : Color.FromArgb(50, 71, 20, 24);
            using (GraphicsPath path = GameUiDrawing.RoundedRectangle(box, 9))
            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(Color.FromArgb(190, Color.White), 2)) {
                e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(e.Graphics, this.Text, this.Font, box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal class GameProgressBar : Control
    {
        private int progressValue;
        public int Maximum = 1000;
        public Color BarColor = Color.FromArgb(90, 167, 243);
        public Color TrackColor = Color.FromArgb(44, 57, 80);
        public int Value
        {
            get { return this.progressValue; }
            set { this.progressValue = Math.Min(this.Maximum, Math.Max(0, value)); this.Invalidate(); }
        }

        public GameProgressBar()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle box = new Rectangle(0, 1, Math.Max(1, this.Width - 1), Math.Max(1, this.Height - 3));
            int radius = Math.Max(2, box.Height / 2);
            using (GraphicsPath track = GameUiDrawing.RoundedRectangle(box, radius))
            using (SolidBrush brush = new SolidBrush(this.TrackColor)) e.Graphics.FillPath(brush, track);
            int fillWidth = (int)Math.Round(box.Width * this.progressValue / (double)Math.Max(1, this.Maximum));
            if (fillWidth > 1) {
                Rectangle filled = new Rectangle(box.Left, box.Top, Math.Max(box.Height, fillWidth), box.Height);
                filled.Width = Math.Min(filled.Width, box.Width);
                using (GraphicsPath bar = GameUiDrawing.RoundedRectangle(filled, radius))
                using (SolidBrush brush = new SolidBrush(this.BarColor)) e.Graphics.FillPath(brush, bar);
            }
        }
    }

    internal class GameMetricLabel : Label
    {
        public string Caption = "";
        public string Metric = "";
        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle content = Rectangle.FromLTRB(this.ClientRectangle.Left + this.Padding.Left,
                this.ClientRectangle.Top + this.Padding.Top,
                this.ClientRectangle.Right - this.Padding.Right,
                this.ClientRectangle.Bottom - this.Padding.Bottom);
            TextRenderer.DrawText(e.Graphics, this.Caption, this.Font, content,
                this.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (Font bold = new Font(this.Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, this.Metric, bold, content,
                    this.ForeColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class PokemonPortrait : Control
    {
        private Bitmap sprite;
        public Bitmap Sprite
        {
            get { return this.sprite; }
            set { this.sprite = value; this.Invalidate(); }
        }

        public PokemonPortrait()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.BackColor = Color.FromArgb(32, 45, 67);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(this.BackColor);
            using (Pen grid = new Pen(Color.FromArgb(64, 68, 65), 1)) {
                for (int x = -this.Height; x < this.Width; x += 24)
                    e.Graphics.DrawLine(grid, x, this.Height, x + this.Height, 0);
            }
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(40, 238, 244, 255)))
                e.Graphics.FillEllipse(shadow, this.Width / 2 - 55, this.Height - 41, 110, 18);
            if (this.sprite == null) return;
            int maxWidth = Math.Min(150, Math.Max(1, this.Width - 24));
            int maxHeight = Math.Min(150, Math.Max(1, this.Height - 42));
            double ratio = Math.Min(maxWidth / (double)this.sprite.Width, maxHeight / (double)this.sprite.Height);
            ratio = Math.Max(0.1, ratio);
            int width = Math.Max(1, (int)Math.Floor(this.sprite.Width * ratio + 0.5));
            int height = Math.Max(1, (int)Math.Floor(this.sprite.Height * ratio + 0.5));
            Rectangle target = new Rectangle((this.Width - width) / 2, this.Height - height - 28, width, height);
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(this.sprite, target);
        }
    }

    internal class PokeballMark : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(this.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle ball = new Rectangle(1, 1, Math.Max(1, this.Width - 3), Math.Max(1, this.Height - 3));
            using (SolidBrush white = new SolidBrush(Color.White)) e.Graphics.FillEllipse(white, ball);
            using (Pen outline = new Pen(Color.FromArgb(36, 50, 73), 3)) e.Graphics.DrawEllipse(outline, ball);
            int middle = ball.Top + ball.Height / 2;
            using (Pen line = new Pen(Color.FromArgb(36, 50, 73), 6)) e.Graphics.DrawLine(line, ball.Left + 1, middle, ball.Right - 1, middle);
            Rectangle center = new Rectangle(ball.Left + ball.Width / 2 - 7, middle - 7, 14, 14);
            using (SolidBrush white = new SolidBrush(Color.White)) e.Graphics.FillEllipse(white, center);
            using (Pen outline = new Pen(Color.FromArgb(36, 50, 73), 3)) e.Graphics.DrawEllipse(outline, center);
        }
    }

    /// <summary>홈·포켓몬·상점·주식·설정을 한곳에 모은 게임형 포켓몬 센터.</summary>
    public class GameMenuForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        private const int WmNcLeftButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;
        private static readonly Color Red = Color.FromArgb(238, 89, 96);
        private static readonly Color RedDark = Color.FromArgb(183, 46, 54);
        private static readonly Color Blue = Color.FromArgb(90, 167, 243);
        private static readonly Color Yellow = Color.FromArgb(233, 189, 57);
        private static readonly Color Ink = Color.FromArgb(238, 244, 255);
        private static readonly Color Muted = Color.FromArgb(170, 184, 205);
        private static readonly Color Paper = Color.FromArgb(24, 34, 54);
        private static readonly Color PanelColor = Color.FromArgb(32, 45, 67);
        private static readonly Color Soft = Color.FromArgb(44, 57, 80);
        private static readonly Color Line = Color.FromArgb(69, 83, 106);
        private static readonly Color Green = Color.FromArgb(84, 201, 149);

        private readonly PetWorld world;
        private readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        private readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        private readonly List<Button> scaleButtons = new List<Button>();
        private readonly List<Button> speedButtons = new List<Button>();
        private readonly List<Button> rosterButtons = new List<Button>();
        private readonly Timer refreshTimer = new Timer();
        private Label wallet;
        private Label homeHeadingHint;
        private PokemonPortrait portrait;
        private Label stageBadge;
        private Label homeName;
        private Label gradeBadge;
        private Label income;
        private GameMetricLabel friendshipText;
        private GameMetricLabel walkText;
        private GameProgressBar friendshipProgress;
        private GameProgressBar walkProgress;
        private Label foodBoost;
        private Label evolutionNote;
        private Label shopInventory;
        private Label shopFoodOwned;
        private Label shopDropOwned;
        private Label shopDrawOwned;
        private Label shopFeedback;
        private Label stockHeadingHint;
        private GameMetricLabel stockPortfolio;
        private GameMetricLabel stockCash;
        private GameMetricLabel marketSummary;
        private Label stockPositionsPreview;
        private Label stockMarketPreview;
        private Label savedStatus;
        private Button homeFeed;
        private Button homeEvolve;
        private Button homeRecall;
        private Button homePetsShortcut;
        private Button homeShopShortcut;
        private Button homeStockShortcut;
        private Button petFeed;
        private Button petEvolve;
        private Button petRecall;
        private Button petRelease;
        private Button petRecruit;
        private FlowLayoutPanel petRoster;
        private Button shopFood;
        private Button shopDrop;
        private Button shopDraw;
        private Button pauseButton;
        private Button topmostButton;
        private Button autostartButton;
        private TableLayoutPanel shellLayout;
        private TableLayoutPanel homeHero;
        private Label shopHeadingTitle;
        private readonly List<Panel> menuPages = new List<Panel>();
        private readonly ToolTip buttonHints = new ToolTip();
        private Panel settingsPage;
        private int selectedIndex;

        public GameMenuForm(PetWorld world)
        {
            this.world = world;
            this.Text = "포켓몬 센터";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ClientSize = new Size(920, 660);
            this.MinimumSize = new Size(736, 620);
            this.BackColor = Ink;
            this.Padding = new Padding(3);
            this.Font = UiFonts.Create(9.0f);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.TopMost = true;
            this.KeyPreview = true;
            this.KeyDown += this.HandleMenuShortcut;
            this.BuildHeader();
            this.BuildFooter();
            this.BuildShell();
            this.ApplyResponsiveLayout();
            this.SelectPage("home");
            this.RefreshGameState();
            this.refreshTimer.Interval = 700;
            this.refreshTimer.Tick += delegate { this.RefreshGameState(); };
            this.refreshTimer.Start();
            this.UpdateWindowShape();
        }

        protected override CreateParams CreateParams
        {
            get { CreateParams parameters = base.CreateParams; parameters.ClassStyle |= 0x00020000; return parameters; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.UpdateWindowShape();
            this.ApplyResponsiveLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = GameUiDrawing.RoundedRectangle(
                new Rectangle(1, 1, Math.Max(1, this.ClientSize.Width - 3), Math.Max(1, this.ClientSize.Height - 3)), 18))
            using (Pen border = new Pen(Ink, 3)) e.Graphics.DrawPath(border, path);
        }

        private void UpdateWindowShape()
        {
            if (this.ClientSize.Width < 10 || this.ClientSize.Height < 10) return;
            using (GraphicsPath path = GameUiDrawing.RoundedRectangle(
                new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height), 18))
                this.Region = new Region(path);
        }

        private void BeginMoveWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(this.Handle, WmNcLeftButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
        }

        private void HandleMenuShortcut(object sender, KeyEventArgs e)
        {
            if (!e.Control) return;
            string[] keys = { "home", "pets", "shop", "stock", "settings" };
            int index = e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D5 ? (int)e.KeyCode - (int)Keys.D1
                : e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad5
                    ? (int)e.KeyCode - (int)Keys.NumPad1 : -1;
            if (index < 0 || index >= keys.Length) return;
            this.SelectPage(keys[index]);
            this.navigation[keys[index]].Focus();
            e.Handled = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            this.refreshTimer.Stop();
            this.refreshTimer.Dispose();
            this.buttonHints.Dispose();
            base.OnFormClosed(e);
        }

        private void BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 62;
            header.BackColor = Red;
            header.MouseDown += this.BeginMoveWindow;
            header.Paint += delegate(object sender, PaintEventArgs e) {
                for (int x = 2; x < header.Width; x += 10)
                    using (Pen stripe = new Pen(Color.FromArgb(26, Color.White), 2))
                        e.Graphics.DrawLine(stripe, x, 0, x, header.Height);
            };
            this.Controls.Add(header);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Red;
            layout.Paint += delegate(object sender, PaintEventArgs e) {
                for (int x = 2; x < layout.Width; x += 10)
                    using (Pen stripe = new Pen(Color.FromArgb(26, Color.White), 2))
                        e.Graphics.DrawLine(stripe, x, 0, x, layout.Height);
            };
            layout.ColumnCount = 4;
            layout.RowCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 91));
            header.Controls.Add(layout);

            PokeballMark ball = new PokeballMark();
            ball.BackColor = Red;
            ball.Dock = DockStyle.Fill;
            ball.Margin = new Padding(12);
            ball.MouseDown += this.BeginMoveWindow;
            layout.Controls.Add(ball, 0, 0);

            Panel titlePanel = new Panel();
            titlePanel.Dock = DockStyle.Fill;
            titlePanel.BackColor = Red;
            titlePanel.Margin = new Padding(0);
            titlePanel.MouseDown += this.BeginMoveWindow;
            layout.Controls.Add(titlePanel, 1, 0);
            Label title = NewLabel("포켓몬 센터", titlePanel, Ink, 14.0f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(4, 7);
            title.Size = new Size(330, 25);
            title.MouseDown += this.BeginMoveWindow;
            Label subtitle = NewLabel("함께 걷고, 성장하고, 새로운 친구를 만나세요", titlePanel,
                Color.FromArgb(252, 225, 226), 8.5f, FontStyle.Regular);
            subtitle.Location = new Point(5, 33);
            subtitle.Size = new Size(360, 18);
            subtitle.MouseDown += this.BeginMoveWindow;

            GameCardPanel walletPanel = new GameCardPanel();
            walletPanel.Dock = DockStyle.Fill;
            walletPanel.Margin = new Padding(2, 9, 5, 9);
            walletPanel.Padding = new Padding(8, 6, 8, 6);
            walletPanel.BackColor = Color.FromArgb(201, 74, 80);
            walletPanel.BorderColor = Color.FromArgb(190, Color.White);
            walletPanel.CornerRadius = 10;
            layout.Controls.Add(walletPanel, 2, 0);
            this.wallet = NewLabel("", walletPanel, Color.White, 10.0f, FontStyle.Bold);
            this.wallet.BackColor = Color.FromArgb(201, 74, 80);
            this.wallet.TextAlign = ContentAlignment.MiddleCenter;
            this.wallet.Dock = DockStyle.Fill;

            FlowLayoutPanel controls = new FlowLayoutPanel();
            controls.Dock = DockStyle.Fill; controls.BackColor = Red;
            controls.WrapContents = false; controls.FlowDirection = FlowDirection.LeftToRight;
            controls.Padding = new Padding(0, 10, 0, 0); controls.Margin = new Padding(0);
            layout.Controls.Add(controls, 3, 0);
            GameHeaderButton minimize = new GameHeaderButton();
            minimize.Text = "—"; minimize.Font = UiFonts.Create(12.0f, FontStyle.Bold);
            minimize.Size = new Size(38, 38); minimize.Margin = new Padding(0, 0, 5, 0);
            minimize.Click += delegate { this.WindowState = FormWindowState.Minimized; };
            minimize.AccessibleName = "최소화";
            GameHeaderButton close = new GameHeaderButton();
            close.Text = "×"; close.Font = UiFonts.Create(14.0f, FontStyle.Bold);
            close.Size = new Size(38, 38); close.Margin = new Padding(0);
            close.Click += delegate { this.Close(); };
            close.AccessibleName = "닫기";
            controls.Controls.Add(minimize); controls.Controls.Add(close);

            Panel headerDivider = new Panel();
            headerDivider.Height = 3;
            headerDivider.Dock = DockStyle.Bottom;
            headerDivider.BackColor = Ink;
            header.Controls.Add(headerDivider);
            headerDivider.BringToFront();
        }

        private void BuildShell()
        {
            TableLayoutPanel shell = new TableLayoutPanel();
            this.shellLayout = shell;
            shell.Location = new Point(this.Padding.Left, this.Padding.Top + 62);
            shell.Size = new Size(this.ClientSize.Width - this.Padding.Horizontal,
                this.ClientSize.Height - this.Padding.Vertical - 62 - 36);
            shell.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            shell.BackColor = Paper;
            shell.ColumnCount = 2;
            shell.RowCount = 1;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            this.Controls.Add(shell);

            Panel navigationPanel = new Panel();
            navigationPanel.Dock = DockStyle.Fill;
            navigationPanel.BackColor = Soft;
            navigationPanel.Padding = new Padding(10, 14, 10, 8);
            navigationPanel.Paint += delegate(object sender, PaintEventArgs e) {
                using (Pen border = new Pen(Ink, 3))
                    e.Graphics.DrawLine(border, navigationPanel.Width - 2, 0,
                        navigationPanel.Width - 2, navigationPanel.Height);
            };
            shell.Controls.Add(navigationPanel, 0, 0);
            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.BackColor = Soft;
            navigationPanel.Controls.Add(flow);
            this.AddNavigation(flow, "home", "⌂  홈");
            this.AddNavigation(flow, "pets", "◉  포켓몬");
            this.AddNavigation(flow, "shop", "◆  상점");
            this.AddNavigation(flow, "stock", "↗  주식");
            this.AddNavigation(flow, "settings", "⚙  설정");

            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Paper;
            shell.Controls.Add(content, 1, 0);
            this.BuildHomePage(content);
            this.BuildPetsPage(content);
            this.BuildShopPage(content);
            this.BuildStockPage(content);
            this.BuildSettingsPage(content);
        }

        private void BuildFooter()
        {
            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 36;
            footer.BackColor = PanelColor;
            footer.Paint += delegate(object sender, PaintEventArgs e) {
                using (Pen pen = new Pen(Line, 2)) e.Graphics.DrawLine(pen, 0, 1, footer.Width, 1);
            };
            Label left = NewLabel("● 메뉴는 자유롭게 이동하고 최소화할 수 있습니다", footer,
                Muted, 9.0f, FontStyle.Regular);
            left.Dock = DockStyle.Left;
            left.Width = 390;
            left.TextAlign = ContentAlignment.MiddleLeft;
            left.Padding = new Padding(14, 0, 0, 0);
            Label right = NewLabel("최근 저장됨 · 방금 전", footer, Muted, 9.0f, FontStyle.Regular);
            this.savedStatus = right;
            right.Dock = DockStyle.Right;
            right.Width = 180;
            right.TextAlign = ContentAlignment.MiddleRight;
            right.Padding = new Padding(0, 0, 14, 0);
            Panel divider = new Panel();
            divider.Location = new Point(0, 0);
            divider.Size = new Size(footer.Width, 2);
            divider.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            divider.BackColor = Line;
            footer.Controls.Add(divider);
            divider.BringToFront();
            this.Controls.Add(footer);
            footer.BringToFront();
        }

        private void AddNavigation(FlowLayoutPanel parent, string key, string text)
        {
            Button button = NewButton(text, Soft, delegate { this.SelectPage(key); });
            button.ForeColor = Ink;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(11, 0, 0, 0);
            button.Width = 148;
            button.Height = 46;
            button.Margin = new Padding(0, 0, 8, 7);
            GameActionButton gameButton = button as GameActionButton;
            if (gameButton != null) { gameButton.ShowDepth = false; gameButton.EdgeColor = Color.Transparent; }
            parent.Controls.Add(button);
            this.navigation[key] = button;
        }

        private Panel NewPage(Panel content, string key)
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = Paper;
            page.Padding = new Padding(16);
            page.AutoScroll = true;
            page.AutoScrollMinSize = new Size(520, 470);
            content.Controls.Add(page);
            this.pages[key] = page;
            this.menuPages.Add(page);
            return page;
        }

        private static Label AddHeading(Control parent, string title, string hint)
        {
            Panel row = new Panel();
            row.Dock = DockStyle.Top;
            row.Height = 43;
            row.BackColor = Paper;
            parent.Controls.Add(row);
            Label heading = NewLabel(title, row, Ink, 14.0f, FontStyle.Bold);
            heading.Dock = DockStyle.Left;
            heading.Width = 250;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            Label description = NewLabel(hint, row, Muted, 9.0f, FontStyle.Regular);
            description.Dock = DockStyle.Right;
            description.Width = 310;
            description.TextAlign = ContentAlignment.MiddleRight;
            return heading;
        }

        private void BuildHomePage(Panel content)
        {
            Panel page = this.NewPage(content, "home");
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 295));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(layout);
            Panel headingHost = new Panel(); headingHost.Dock = DockStyle.Fill; headingHost.BackColor = Paper;
            headingHost.Margin = new Padding(0);
            Label heading = NewLabel("오늘의 파트너", headingHost, Ink, 14.0f, FontStyle.Bold);
            heading.Dock = DockStyle.Left; heading.Width = 250; heading.TextAlign = ContentAlignment.MiddleLeft;
            this.homeHeadingHint = NewLabel("산책 중 · 수입 x1.0", headingHost, Muted, 9.0f, FontStyle.Regular);
            this.homeHeadingHint.Dock = DockStyle.Right; this.homeHeadingHint.Width = 310;
            this.homeHeadingHint.TextAlign = ContentAlignment.MiddleRight;
            layout.Controls.Add(headingHost, 0, 0);

            // 파트너 전환은 '내 포켓몬' 화면에서 한다. 홈은 시안처럼 상태 확인에 집중한다.
            TableLayoutPanel hero = new TableLayoutPanel();
            this.homeHero = hero;
            hero.Dock = DockStyle.Fill; hero.ColumnCount = 2; hero.Padding = new Padding(0);
            hero.Margin = new Padding(0);
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 264));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Panel portraitCard = Card(); portraitCard.Dock = DockStyle.Fill; portraitCard.Margin = new Padding(0, 0, 12, 0);
            this.portrait = new PokemonPortrait();
            this.portrait.Dock = DockStyle.Fill;
            portraitCard.Controls.Add(this.portrait);
            GamePillLabel level = new GamePillLabel(); this.stageBadge = level;
            level.Text = "1단계"; level.ForeColor = Color.White; level.FillColor = Blue;
            level.Font = UiFonts.Create(8.0f, FontStyle.Bold); level.Location = new Point(11, 11);
            level.Size = new Size(64, 25); level.TextAlign = ContentAlignment.MiddleCenter;
            portraitCard.Controls.Add(level);
            this.stageBadge.BringToFront();
            hero.Controls.Add(portraitCard, 0, 0);

            Panel statusCard = Card(); statusCard.Dock = DockStyle.Fill; statusCard.Margin = new Padding(0);
            TableLayoutPanel status = new TableLayoutPanel(); status.Dock = DockStyle.Fill;
            status.Padding = new Padding(14, 10, 14, 10); status.ColumnCount = 1; status.RowCount = 8;
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            statusCard.Controls.Add(status);
            Panel nameRow = new Panel(); nameRow.Dock = DockStyle.Fill; nameRow.BackColor = PanelColor;
            this.homeName = NewLabel("", nameRow, Ink, 14.0f, FontStyle.Bold);
            this.homeName.AutoSize = true; this.homeName.Location = new Point(0, 3);
            GamePillLabel grade = new GamePillLabel(); this.gradeBadge = grade;
            grade.ForeColor = Color.FromArgb(75,57,0); grade.FillColor = Yellow; grade.CornerRadius = 12;
            grade.Font = UiFonts.Create(8.0f, FontStyle.Bold); grade.Location = new Point(6, 5);
            grade.Size = new Size(62, 24); grade.TextAlign = ContentAlignment.MiddleCenter; nameRow.Controls.Add(grade);
            this.income = NewLabel("", nameRow, Green, 9.0f, FontStyle.Bold);
            this.income.Dock = DockStyle.Right; this.income.Width = 160; this.income.TextAlign = ContentAlignment.MiddleRight;
            status.Controls.Add(nameRow, 0, 0);
            this.friendshipText = new GameMetricLabel(); this.friendshipText.Caption = "친밀도";
            this.friendshipText.ForeColor = Ink; this.friendshipText.BackColor = PanelColor;
            this.friendshipText.Font = UiFonts.Create(9.0f, FontStyle.Regular);
            this.friendshipText.Dock = DockStyle.Fill; status.Controls.Add(this.friendshipText, 0, 1);
            this.friendshipProgress = new GameProgressBar(); this.friendshipProgress.Maximum = 1000;
            this.friendshipProgress.BarColor = Blue;
            this.friendshipProgress.Dock = DockStyle.Fill; status.Controls.Add(this.friendshipProgress, 0, 2);
            this.walkText = new GameMetricLabel(); this.walkText.Caption = "진화 산책 거리";
            this.walkText.ForeColor = Ink; this.walkText.BackColor = PanelColor;
            this.walkText.Font = UiFonts.Create(9.0f, FontStyle.Regular);
            this.walkText.Dock = DockStyle.Fill; status.Controls.Add(this.walkText, 0, 3);
            this.walkProgress = new GameProgressBar(); this.walkProgress.Maximum = 1000;
            this.walkProgress.BarColor = Green;
            this.walkProgress.Dock = DockStyle.Fill; status.Controls.Add(this.walkProgress, 0, 4);
            GamePillLabel buff = new GamePillLabel(); this.foodBoost = buff;
            buff.FillColor = Color.FromArgb(40, 61, 90); buff.ForeColor = Ink; buff.CornerRadius = 9;
            buff.Font = UiFonts.Create(9.0f, FontStyle.Bold); buff.Dock = DockStyle.Fill;
            buff.TextAlign = ContentAlignment.MiddleCenter; buff.Margin = new Padding(0, 4, 0, 4);
            status.Controls.Add(buff, 0, 5);
            TableLayoutPanel actions = new TableLayoutPanel(); actions.Dock = DockStyle.Fill;
            actions.BackColor = PanelColor; actions.ColumnCount = 3; actions.RowCount = 1;
            for (int i = 0; i < 3; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            this.homeFeed = NewButton("먹이 주기", Red, delegate { this.FeedSelected(); });
            this.homeEvolve = NewButton("진화", Blue, delegate { this.EvolveSelected(); });
            this.homeRecall = NewButton("위치 찾기", Blue, delegate { this.RecallSelected(); });
            Button[] actionButtons = { this.homeFeed, this.homeEvolve, this.homeRecall };
            for (int i = 0; i < actionButtons.Length; i++) {
                actionButtons[i].Dock = DockStyle.Fill;
                actionButtons[i].Margin = new Padding(i == 0 ? 0 : 3, 2, i == 2 ? 0 : 3, 2);
                actions.Controls.Add(actionButtons[i], i, 0);
            }
            status.Controls.Add(actions, 0, 6);
            this.evolutionNote = NewLabel("", status, Muted, 8.5f, FontStyle.Regular);
            this.evolutionNote.Dock = DockStyle.Fill; this.evolutionNote.TextAlign = ContentAlignment.BottomRight;
            status.Controls.Add(this.evolutionNote, 0, 7);
            hero.Controls.Add(statusCard, 1, 0);
            layout.Controls.Add(hero, 0, 1);

            TableLayoutPanel shortcuts = new TableLayoutPanel(); shortcuts.Dock = DockStyle.Fill;
            shortcuts.Margin = new Padding(0);
            shortcuts.BackColor = Paper; shortcuts.ColumnCount = 3; shortcuts.RowCount = 1;
            for (int i = 0; i < 3; i++) shortcuts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            this.homePetsShortcut = this.AddShortcut(shortcuts, 0, "내 포켓몬\r\n목록과 상태 관리     ›", "pets");
            this.homeShopShortcut = this.AddShortcut(shortcuts, 1, "포켓몬 상점\r\n먹이와 진화 아이템     ›", "shop");
            this.homeStockShortcut = this.AddShortcut(shortcuts, 2, "주식시장\r\n내 평가액 확인     ›", "stock");
            layout.Controls.Add(shortcuts, 0, 2);
        }

        private Button AddShortcut(TableLayoutPanel parent, int column, string text, string page)
        {
            Button button = NewButton(text, PanelColor, delegate { this.SelectPage(page); });
            button.ForeColor = Ink; button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(11, 0, 6, 0); button.Dock = DockStyle.Fill;
            button.Margin = new Padding(column == 0 ? 0 : 5, 12, column == 2 ? 0 : 5, 0);
            GameActionButton gameButton = button as GameActionButton;
            if (gameButton != null) { gameButton.ShowDepth = false; gameButton.EdgeColor = Line; gameButton.CornerRadius = 12; }
            parent.Controls.Add(button, column, 0);
            return button;
        }

        private void BuildPetsPage(Panel content)
        {
            Panel page = this.NewPage(content, "pets");
            TableLayoutPanel layout = new TableLayoutPanel(); layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1; layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.Controls.Add(layout);
            Panel heading = new Panel(); heading.Dock = DockStyle.Fill; heading.Margin = new Padding(0);
            heading.BackColor = Paper;
            AddHeading(heading, "내 포켓몬", "선택한 포켓몬의 상태와 행동을 관리합니다");
            layout.Controls.Add(heading, 0, 0);
            TableLayoutPanel rosterSection = new TableLayoutPanel(); rosterSection.Dock = DockStyle.Fill;
            rosterSection.ColumnCount = 1; rosterSection.RowCount = 2; rosterSection.Margin = new Padding(0);
            rosterSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rosterSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            this.petRoster = new FlowLayoutPanel(); this.petRoster.Dock = DockStyle.Fill;
            this.petRoster.BackColor = Paper; this.petRoster.AutoScroll = true;
            this.petRoster.WrapContents = true; this.petRoster.FlowDirection = FlowDirection.LeftToRight;
            this.petRoster.Margin = new Padding(0); this.petRoster.Padding = new Padding(0, 0, 0, 4);
            this.petRoster.Resize += delegate { this.LayoutRosterButtons(); };
            rosterSection.Controls.Add(this.petRoster, 0, 0);
            this.petRecruit = NewButton("＋  새 포켓몬 영입", PanelColor, delegate { this.BuyRandom(); });
            this.petRecruit.ForeColor = Ink; this.petRecruit.TextAlign = ContentAlignment.MiddleLeft;
            this.petRecruit.Padding = new Padding(14, 0, 8, 0); this.petRecruit.Dock = DockStyle.Fill;
            this.petRecruit.Margin = new Padding(0, 6, 0, 0);
            GameActionButton recruitGame = this.petRecruit as GameActionButton;
            if (recruitGame != null) { recruitGame.ShowDepth = false; recruitGame.EdgeColor = Line; recruitGame.CornerRadius = 12; }
            rosterSection.Controls.Add(this.petRecruit, 0, 1);
            layout.Controls.Add(rosterSection, 0, 1);
            Panel detailCard = Card(); detailCard.Dock = DockStyle.Fill; detailCard.Margin = new Padding(0, 10, 0, 0);
            Label manageTitle = NewLabel("선택 포켓몬 관리", detailCard, Ink, 10.0f, FontStyle.Bold);
            manageTitle.Dock = DockStyle.Top; manageTitle.Height = 32; manageTitle.Padding = new Padding(12, 4, 0, 0);
            FlowLayoutPanel actions = ActionRow(); actions.Dock = DockStyle.Fill;
            this.petFeed = NewButton("먹이 주기", Red, delegate { this.FeedSelected(); });
            this.petEvolve = NewButton("진화", Blue, delegate { this.EvolveSelected(); });
            this.petRecall = NewButton("화면 가운데로", Green, delegate { this.RecallSelected(); });
            this.petRelease = NewButton("보내주기…", Color.FromArgb(107,114,128), delegate { this.ReleaseSelected(); });
            foreach (Button button in new Button[] { this.petFeed, this.petEvolve, this.petRecall, this.petRelease }) {
                button.Width = 145; button.Height = 56; actions.Controls.Add(button);
            }
            actions.Padding = new Padding(12, 2, 0, 0); detailCard.Controls.Add(actions); actions.BringToFront();
            layout.Controls.Add(detailCard, 0, 2);
        }

        private void BuildShopPage(Panel content)
        {
            Panel page = this.NewPage(content, "shop");
            TableLayoutPanel layout = new TableLayoutPanel(); layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1; layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 368));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.Controls.Add(layout);
            Panel heading = new Panel(); heading.Dock = DockStyle.Fill; heading.BackColor = Paper;
            Label title = NewLabel("프렌들리 상점", heading, Ink, 14.0f, FontStyle.Bold);
            this.shopHeadingTitle = title;
            title.Dock = DockStyle.Left; title.Width = 250; title.TextAlign = ContentAlignment.MiddleLeft;
            this.shopInventory = NewLabel("", heading, Muted, 9.0f, FontStyle.Regular);
            this.shopInventory.Dock = DockStyle.Right; this.shopInventory.Width = 410;
            this.shopInventory.TextAlign = ContentAlignment.MiddleRight; layout.Controls.Add(heading, 0, 0);
            this.shopFeedback = NewLabel("상품을 구매하면 결과와 남은 잔액을 알려드립니다.", page,
                Muted, 8.5f, FontStyle.Regular);
            this.shopFeedback.Dock = DockStyle.Fill; this.shopFeedback.TextAlign = ContentAlignment.MiddleLeft;
            this.shopFeedback.Padding = new Padding(10, 0, 10, 0); this.shopFeedback.BackColor = Color.FromArgb(40, 61, 90);
            layout.Controls.Add(this.shopFeedback, 0, 1);
            TableLayoutPanel grid = new TableLayoutPanel(); grid.Dock = DockStyle.Top; grid.Height = 368;
            grid.ColumnCount = 2; grid.RowCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
            this.shopFood = this.AddShopTile(grid, 0, 0, "●", "포켓푸드",
                "5분 동안 산책 속도가 2배가 되고 친밀도가 2 올라갑니다.", PetWorld.FoodCost,
                delegate { this.BuyFoodFromShop(); }, out this.shopFoodOwned);
            this.shopDrop = this.AddShopTile(grid, 1, 0, "◆", "성장의 물방울",
                "진화 조건을 모두 채운 포켓몬이 진화할 때 사용합니다.", PetWorld.GrowthDropCost,
                delegate { this.BuyDropFromShop(); }, out this.shopDropOwned);
            this.shopDraw = this.AddShopTile(grid, 0, 1, "◉", "랜덤 포켓볼",
                "새로운 포켓몬 한 마리를 무작위로 영입합니다.", PetWorld.PokemonPrice,
                delegate { this.BuyRandom(); }, out this.shopDrawOwned);
            layout.Controls.Add(grid, 0, 2);
        }

        private Button AddShopTile(TableLayoutPanel parent, int column, int row, string icon,
            string name, string detail, int price, EventHandler action, out Label ownedLabel)
        {
            Panel card = Card(); card.Dock = DockStyle.Fill; card.Padding = new Padding(14);
            card.Margin = new Padding(column == 0 ? 0 : 5, row == 0 ? 0 : 5,
                column == 1 ? 0 : 5, row == 1 ? 0 : 5); parent.Controls.Add(card, column, row);
            if (row == 1 && column == 0) parent.SetColumnSpan(card, 2);

            TableLayoutPanel tile = new TableLayoutPanel(); tile.Dock = DockStyle.Fill;
            tile.BackColor = PanelColor; tile.ColumnCount = 1; tile.RowCount = 3;
            tile.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            tile.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tile.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            card.Controls.Add(tile);

            TableLayoutPanel top = new TableLayoutPanel(); top.Dock = DockStyle.Fill;
            top.BackColor = PanelColor; top.ColumnCount = 3; top.RowCount = 1;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            GamePillLabel iconLabel = new GamePillLabel(); iconLabel.Text = icon; iconLabel.FillColor = Soft;
            iconLabel.ForeColor = Red; iconLabel.CornerRadius = 12;
            iconLabel.Font = UiFonts.Create(17.0f, FontStyle.Bold);
            iconLabel.Dock = DockStyle.Fill; iconLabel.Margin = new Padding(0, 0, 2, 2);
            top.Controls.Add(iconLabel, 0, 0);
            Label title = NewLabel(name, top, Ink, 11.0f, FontStyle.Bold);
            title.Dock = DockStyle.Fill; title.Padding = new Padding(6, 0, 4, 0);
            title.TextAlign = ContentAlignment.MiddleLeft;
            top.Controls.Add(title, 1, 0);
            ownedLabel = NewLabel("", top, Muted, 8.0f, FontStyle.Bold);
            ownedLabel.Dock = DockStyle.Fill; ownedLabel.Margin = new Padding(0);
            ownedLabel.TextAlign = ContentAlignment.MiddleRight;
            top.Controls.Add(ownedLabel, 2, 0);
            Label description = NewLabel(detail, card, Muted, 8.0f, FontStyle.Regular);
            description.Font = UiFonts.Create(8.5f, FontStyle.Regular);
            description.Dock = DockStyle.Fill; description.Padding = new Padding(0, 7, 0, 4);
            description.TextAlign = ContentAlignment.TopLeft;
            Panel bottom = new Panel(); bottom.Dock = DockStyle.Bottom; bottom.Height = 48; bottom.BackColor = PanelColor;
            Label priceLabel = NewLabel(PetWorld.FormatWon(price), bottom, Ink, 10.0f, FontStyle.Bold);
            priceLabel.Dock = DockStyle.Left; priceLabel.Width = 120; priceLabel.TextAlign = ContentAlignment.MiddleLeft;
            Button button = NewButton(name == "랜덤 포켓볼" ? "영입하기" : "구매", Red, action);
            button.Dock = DockStyle.Right; button.Width = 100;
            bottom.Controls.Add(button);
            tile.Controls.Add(top, 0, 0); tile.Controls.Add(description, 0, 1); tile.Controls.Add(bottom, 0, 2);
            return button;
        }

        private void BuildStockPage(Panel content)
        {
            Panel page = this.NewPage(content, "stock");
            TableLayoutPanel layout = new TableLayoutPanel(); layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1; layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 216));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(layout);

            Panel heading = new Panel(); heading.Dock = DockStyle.Fill; heading.Margin = new Padding(0);
            heading.BackColor = Paper;
            Label headingTitle = NewLabel("포켓몬 주식시장", heading, Ink, 14.0f, FontStyle.Bold);
            headingTitle.Dock = DockStyle.Left; headingTitle.Width = 260;
            headingTitle.TextAlign = ContentAlignment.MiddleLeft;
            this.stockHeadingHint = NewLabel("", heading, Muted, 9.0f, FontStyle.Regular);
            this.stockHeadingHint.Dock = DockStyle.Right; this.stockHeadingHint.Width = 310;
            this.stockHeadingHint.TextAlign = ContentAlignment.MiddleRight;
            layout.Controls.Add(heading, 0, 0);

            Panel card = Card(); card.Dock = DockStyle.Fill; card.Margin = new Padding(0);
            card.Padding = new Padding(14); layout.Controls.Add(card, 0, 1);
            TableLayoutPanel body = new TableLayoutPanel(); body.Dock = DockStyle.Fill;
            body.BackColor = PanelColor; body.ColumnCount = 1; body.RowCount = 5;
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            card.Controls.Add(body);

            Panel titleRow = new Panel(); titleRow.Dock = DockStyle.Fill; titleRow.BackColor = PanelColor;
            Label title = NewLabel("내 투자 현황", titleRow, Ink, 13.0f, FontStyle.Bold);
            title.Dock = DockStyle.Left; title.Width = 130; title.TextAlign = ContentAlignment.MiddleLeft;
            GamePillLabel badge = new GamePillLabel(); badge.Text = "게임 머니 전용";
            badge.FillColor = Yellow; badge.ForeColor = Color.FromArgb(75, 57, 0);
            badge.Font = UiFonts.Create(8.0f, FontStyle.Bold);
            badge.Location = new Point(124, 5); badge.Size = new Size(100, 24);
            titleRow.Controls.Add(badge); badge.BringToFront(); body.Controls.Add(titleRow, 0, 0);

            this.stockPortfolio = new GameMetricLabel(); this.stockPortfolio.Caption = "주식 평가액";
            this.stockPortfolio.BackColor = PanelColor; this.stockPortfolio.ForeColor = Ink;
            this.stockPortfolio.Font = UiFonts.Create(9.0f); this.stockPortfolio.Dock = DockStyle.Fill;
            body.Controls.Add(this.stockPortfolio, 0, 1);
            this.stockCash = new GameMetricLabel(); this.stockCash.Caption = "현금";
            this.stockCash.BackColor = PanelColor; this.stockCash.ForeColor = Ink;
            this.stockCash.Font = UiFonts.Create(9.0f); this.stockCash.Dock = DockStyle.Fill;
            body.Controls.Add(this.stockCash, 0, 2);
            this.marketSummary = new GameMetricLabel(); this.marketSummary.Caption = "시장 국면";
            this.marketSummary.BackColor = Color.FromArgb(40,61,90); this.marketSummary.ForeColor = Ink;
            this.marketSummary.Font = UiFonts.Create(9.0f); this.marketSummary.Dock = DockStyle.Fill;
            this.marketSummary.Padding = new Padding(10, 0, 10, 0); this.marketSummary.Margin = new Padding(0, 4, 0, 4);
            body.Controls.Add(this.marketSummary, 0, 3);
            FlowLayoutPanel openRow = new FlowLayoutPanel(); openRow.Dock = DockStyle.Fill;
            openRow.BackColor = PanelColor; openRow.WrapContents = false;
            Button open = NewButton("전체 주식창 열기", Red, delegate { this.world.OpenStockOverlay(); });
            open.Size = new Size(148, 46); open.Margin = new Padding(0, 6, 0, 0);
            openRow.Controls.Add(open); body.Controls.Add(openRow, 0, 4);

            TableLayoutPanel previews = new TableLayoutPanel(); previews.Dock = DockStyle.Fill;
            previews.ColumnCount = 2; previews.RowCount = 1; previews.Margin = new Padding(0, 10, 0, 0);
            previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            this.stockPositionsPreview = this.AddStockPreview(previews, 0, "내 보유 종목");
            this.stockMarketPreview = this.AddStockPreview(previews, 1, "시장 한눈에");
            layout.Controls.Add(previews, 0, 2);
        }

        private Label AddStockPreview(TableLayoutPanel parent, int column, string titleText)
        {
            Panel preview = Card(); preview.Dock = DockStyle.Fill;
            preview.Margin = new Padding(column == 0 ? 0 : 5, 0, column == 1 ? 0 : 5, 0);
            preview.Padding = new Padding(14, 10, 14, 10); parent.Controls.Add(preview, column, 0);
            TableLayoutPanel layout = new TableLayoutPanel(); layout.Dock = DockStyle.Fill;
            layout.BackColor = PanelColor; layout.ColumnCount = 1; layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); preview.Controls.Add(layout);
            Label title = NewLabel(titleText, layout, Ink, 10.0f, FontStyle.Bold);
            title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(title, 0, 0);
            Label body = NewLabel("", layout, Muted, 8.5f, FontStyle.Regular);
            body.Dock = DockStyle.Fill; body.TextAlign = ContentAlignment.TopLeft;
            body.Padding = new Padding(0, 4, 0, 0); body.AutoEllipsis = true;
            layout.Controls.Add(body, 0, 1); return body;
        }

        private void BuildSettingsPage(Panel content)
        {
            Panel page = this.NewPage(content, "settings");
            this.settingsPage = page;
            AddHeading(page, "게임 설정", "변경사항은 자동으로 저장됩니다");
            this.AddChoiceCard(page, "포켓몬 크기",
                new string[] { "작게", "보통", "크게", "아주 크게" },
                new double[] { 3.0, 4.5, 6.0, 9.0 }, true);
            this.AddChoiceCard(page, "산책 속도",
                new string[] { "느리게", "보통", "빠르게" },
                new double[] { 30.0, 55.0, 95.0 }, false);
            this.topmostButton = NewButton("항상 위 켜짐", PanelColor, delegate {
                this.TopMost = !this.TopMost; this.RefreshGameState();
            });
            this.pauseButton = NewButton("전체 일시정지", PanelColor, delegate { this.world.TogglePause(); this.RefreshGameState(); });
            this.autostartButton = NewButton("윈도우 시작 시 실행", PanelColor, delegate {
                AutoStart.Set(!AutoStart.Enabled()); this.RefreshGameState();
            });
            Button quit = NewButton("게임 종료…", Red, delegate { this.ConfirmQuit(); });
            Button back = NewButton("뒤로 보내기", PanelColor, delegate {
                this.TopMost = false; this.SendToBack(); this.RefreshGameState();
            });
            this.topmostButton.Width = 150; this.pauseButton.Width = 145;
            this.autostartButton.Width = 190; quit.Width = 130; back.Width = 145;
            foreach (Button button in new Button[] { this.topmostButton, this.pauseButton, this.autostartButton, quit, back }) {
                button.Height = 46; button.ForeColor = button == quit ? Color.White : Ink;
                GameActionButton gameButton = button as GameActionButton;
                if (gameButton != null && button != quit) gameButton.EdgeColor = Line;
            }
            this.AddSettingsActionCard(page, "창 표시", 104,
                new Button[] { this.topmostButton, back });
            this.AddSettingsActionCard(page, "게임 동작", 104,
                new Button[] { this.pauseButton, this.autostartButton });
            this.AddSettingsActionCard(page, "위험 작업", 104, new Button[] { quit });
        }

        private void AddSettingsActionCard(Control parent, string titleText, int height, Button[] buttons)
        {
            Panel card = Card(); card.Dock = DockStyle.Top; card.Height = height;
            card.Padding = new Padding(12); card.Margin = new Padding(0, 3, 0, 3);
            parent.Controls.Add(card); card.BringToFront();
            TableLayoutPanel layout = new TableLayoutPanel(); layout.Dock = DockStyle.Fill;
            layout.BackColor = PanelColor; layout.ColumnCount = 1; layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); card.Controls.Add(layout);
            Label title = NewLabel(titleText, layout, titleText == "위험 작업" ? Red : Ink,
                10.0f, FontStyle.Bold);
            title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(title, 0, 0);
            FlowLayoutPanel row = ActionRow(); row.Dock = DockStyle.Fill; row.Padding = new Padding(0);
            foreach (Button button in buttons) row.Controls.Add(button);
            layout.Controls.Add(row, 0, 1);
        }

        private void AddChoiceCard(Control parent, string title, string[] names, double[] values, bool scale)
        {
            Panel card = Card(); card.Dock = DockStyle.Top; card.Height = 96; card.Padding = new Padding(12);
            parent.Controls.Add(card); card.BringToFront();
            Label label = NewLabel(title, card, Ink, 10.0f, FontStyle.Bold); label.Dock = DockStyle.Top; label.Height = 28;
            FlowLayoutPanel row = ActionRow(); row.Dock = DockStyle.Fill;
            for (int i = 0; i < names.Length; i++) {
                double value = values[i];
                Button button = NewButton(names[i], Blue, delegate {
                    if (scale) this.world.SetScale(value); else this.world.SetSpeed(value);
                    this.RefreshGameState();
                });
                button.Tag = value; button.Width = 110; button.Height = 42; row.Controls.Add(button);
                if (scale) this.scaleButtons.Add(button); else this.speedButtons.Add(button);
            }
            card.Controls.Add(row); card.Controls.Add(label);
        }

        private static TableLayoutPanel PageLayout(Control page, int rows, float[] heights)
        {
            TableLayoutPanel layout = new TableLayoutPanel(); layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1; layout.RowCount = rows; page.Controls.Add(layout);
            for (int i = 0; i < rows; i++) {
                layout.RowStyles.Add(i == 2 && heights[i] == 100
                    ? new RowStyle(SizeType.Percent, 100)
                    : new RowStyle(SizeType.Absolute, heights[i]));
            }
            return layout;
        }

        private static FlowLayoutPanel ActionRow()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel(); panel.BackColor = PanelColor;
            panel.FlowDirection = FlowDirection.LeftToRight; panel.WrapContents = true;
            panel.Padding = new Padding(0, 4, 0, 0); return panel;
        }

        private static Panel Card()
        {
            GameCardPanel panel = new GameCardPanel(); panel.BackColor = PanelColor;
            panel.BorderColor = Line; panel.CornerRadius = 14; panel.Padding = new Padding(4); return panel;
        }

        private static Label NewLabel(string text, Control parent, Color color, float size, FontStyle style)
        {
            Label label = new Label(); label.Text = text; label.ForeColor = color;
            label.BackColor = parent.BackColor; label.Font = UiFonts.Create(size, style);
            parent.Controls.Add(label); return label;
        }

        private static Button NewButton(string text, Color color, EventHandler action)
        {
            GameActionButton button = new GameActionButton(); button.Text = text; button.BackColor = color;
            button.ForeColor = Color.White; button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0; button.Font = UiFonts.Create(9.0f, FontStyle.Bold);
            button.Cursor = Cursors.Hand; button.Click += action; return button;
        }

        private void ApplyResponsiveLayout()
        {
            if (this.shellLayout == null || this.homeHero == null) return;
            bool compact = this.ClientSize.Width < 850;
            this.shellLayout.ColumnStyles[0].Width = compact ? 138 : 176;
            this.homeHero.ColumnStyles[0].Width = compact ? 190 : 264;
            foreach (Panel page in this.menuPages)
                page.Padding = new Padding(compact ? 12 : 16);
            if (this.settingsPage != null)
                this.settingsPage.Padding = new Padding(compact ? 12 : 16, 8, compact ? 12 : 16, 8);
            foreach (Button button in this.navigation.Values)
            {
                button.Width = compact ? 110 : 148;
                button.Padding = new Padding(compact ? 7 : 11, 0, 0, 0);
            }
            if (this.income != null) this.income.Width = compact ? 126 : 160;
            foreach (Button button in new Button[] { this.petFeed, this.petEvolve, this.petRecall, this.petRelease })
                if (button != null) button.Width = compact ? 126 : 145;
            if (this.shopHeadingTitle != null) this.shopHeadingTitle.Width = compact ? 185 : 250;
            if (this.shopInventory != null) this.shopInventory.Width = compact ? 350 : 410;
            if (this.topmostButton != null) this.topmostButton.Width = compact ? 108 : 125;
            if (this.pauseButton != null) this.pauseButton.Width = compact ? 118 : 135;
            if (this.autostartButton != null) this.autostartButton.Width = compact ? 160 : 185;
            this.LayoutRosterButtons();
            this.PerformLayout();
        }

        private void EnsureRosterButtons(int count)
        {
            if (this.petRoster == null || this.rosterButtons.Count == count) return;
            this.petRoster.SuspendLayout();
            this.petRoster.Controls.Clear();
            this.rosterButtons.Clear();
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Button pokemon = NewButton("", PanelColor, delegate { this.SetSelected(index); });
                pokemon.ForeColor = Ink; pokemon.TextAlign = ContentAlignment.MiddleLeft;
                pokemon.Padding = new Padding(14, 0, 8, 0); pokemon.Height = 78;
                pokemon.Margin = new Padding(0, 0, 6, 6);
                GameActionButton gameButton = pokemon as GameActionButton;
                if (gameButton != null)
                {
                    gameButton.ShowDepth = false; gameButton.EdgeColor = Line; gameButton.CornerRadius = 12;
                }
                this.petRoster.Controls.Add(pokemon);
                this.rosterButtons.Add(pokemon);
            }
            this.petRoster.ResumeLayout();
            this.LayoutRosterButtons();
        }

        private void LayoutRosterButtons()
        {
            if (this.petRoster == null || this.petRoster.ClientSize.Width <= 0) return;
            int available = Math.Max(150, this.petRoster.ClientSize.Width - 22);
            int columns = available < 430 ? 1 : 2;
            int width = columns == 1 ? available : Math.Max(150, (available - 6) / 2);
            foreach (Button button in this.rosterButtons) button.Width = width;
        }

        private void SelectPage(string key)
        {
            foreach (KeyValuePair<string, Panel> item in this.pages) item.Value.Visible = item.Key == key;
            foreach (KeyValuePair<string, Button> item in this.navigation) {
                bool selected = item.Key == key;
                item.Value.BackColor = selected ? Red : Soft;
                item.Value.ForeColor = selected ? Color.White : Ink;
                GameActionButton gameButton = item.Value as GameActionButton;
                if (gameButton != null) {
                    gameButton.ShowDepth = selected;
                    gameButton.EdgeColor = selected ? Ink : Color.Transparent;
                    gameButton.DepthColor = selected ? RedDark : Color.Transparent;
                    gameButton.Invalidate();
                }
            }
            this.pages[key].BringToFront();
            this.RefreshGameState();
        }

        private PetForm SelectedPet()
        {
            PetForm[] pets = this.world.PetsSnapshot();
            return this.selectedIndex >= 0 && this.selectedIndex < pets.Length ? pets[this.selectedIndex] : null;
        }

        private void SetSelected(int index)
        {
            PetForm[] pets = this.world.PetsSnapshot();
            this.selectedIndex = Math.Min(Math.Max(0, index), Math.Max(0, pets.Length - 1));
            this.RefreshGameState();
        }

        public void RefreshGameState()
        {
            if (this.IsDisposed) return;
            PetForm[] pets = this.world.PetsSnapshot();
            this.selectedIndex = Math.Min(Math.Max(0, this.selectedIndex), Math.Max(0, pets.Length - 1));
            this.wallet.Text = "◉  " + PetWorld.FormatWon(this.world.Options.Coins);
            this.homePetsShortcut.Text = "내 포켓몬\r\n" + pets.Length + "마리 관리하기  ›";
            this.homeShopShortcut.Text = "포켓몬 상점\r\n먹이와 진화 아이템  ›";
            this.shopInventory.Text = "보유 아이템  ·  포켓푸드 " + this.world.Options.Food
                + "개  ·  성장의 물방울 " + this.world.Options.GrowthDrops + "개";
            this.shopFoodOwned.Text = "보유 " + this.world.Options.Food + "개";
            this.shopDropOwned.Text = "보유 " + this.world.Options.GrowthDrops + "개";
            this.shopDrawOwned.Text = "보유 " + pets.Length + "마리";
            this.SetPurchaseButton(this.shopFood, this.world.Options.Coins >= PetWorld.FoodCost, "구매");
            this.SetPurchaseButton(this.shopDrop, this.world.Options.Coins >= PetWorld.GrowthDropCost, "구매");
            this.SetPurchaseButton(this.shopDraw, this.world.Options.Coins >= PetWorld.PokemonPrice, "영입하기");
            PetForm pet = this.SelectedPet();
            this.EnsureRosterButtons(pets.Length);
            for (int i = 0; i < this.rosterButtons.Count; i++) {
                Button roster = this.rosterButtons[i];
                PokemonSprite rosterSprite = Sprites.Find(pets[i].SpriteKey);
                string rosterName = rosterSprite == null ? pets[i].SpriteKey : rosterSprite.NameKo;
                roster.Visible = true; roster.Enabled = true;
                roster.Text = "●  " + rosterName + "  · " + PetWorld.PokemonGrade(pets[i].SpriteKey)
                    + "\r\n     " + (pets[i].EvolutionStageValue + 1) + "단계 · 수입 x"
                    + pets[i].IncomeMultiplierValue.ToString("0.##", CultureInfo.InvariantCulture);
                GameActionButton rosterGame = roster as GameActionButton;
                if (rosterGame != null) rosterGame.EdgeColor = i == this.selectedIndex ? Red : Line;
                roster.Invalidate();
            }
            this.petRecruit.Enabled = this.world.Options.Coins >= PetWorld.PokemonPrice;
            this.petRecruit.Text = "＋  새 포켓몬 영입\r\n     " + PetWorld.FormatWon(PetWorld.PokemonPrice)
                + " · 일반 88% · 준전설 10% · 초전설 2%"
                + (this.petRecruit.Enabled ? "" : " · 잔액 부족");
            this.buttonHints.SetToolTip(this.petRecruit, this.petRecruit.Enabled
                ? "랜덤 확률로 새로운 포켓몬을 영입합니다."
                : "영입하려면 " + PetWorld.FormatWon(PetWorld.PokemonPrice - this.world.Options.Coins) + "이 더 필요합니다.");
            if (pet != null) {
                PokemonSprite sprite = Sprites.Find(pet.SpriteKey);
                string name = sprite == null ? pet.SpriteKey : sprite.NameKo;
                string grade = PetWorld.PokemonGrade(pet.SpriteKey);
                int stage = pet.EvolutionStageValue + 1;
                this.homeName.Text = name;
                this.gradeBadge.Text = grade;
                this.gradeBadge.Left = this.homeName.Right + 6;
                this.stageBadge.Text = stage + "단계";
                this.portrait.Sprite = pet.MenuImage;
                this.homeHeadingHint.Text = (this.world.Paused ? "산책 일시정지 · 수입 x" : "산책 중 · 수입 x")
                    + pet.IncomeMultiplierValue.ToString("0.##", CultureInfo.InvariantCulture);
                this.income.Text = "+" + ((int)Math.Round(PetWorld.CoinsPerWalk * pet.IncomeMultiplierValue))
                    .ToString("N0", CultureInfo.InvariantCulture) + "원 / 100px";
                double displayedFriendship = pet.DisplayedFriendshipValue;
                this.friendshipText.Metric = displayedFriendship.ToString("0", CultureInfo.InvariantCulture)
                    + " / " + pet.FriendshipNeed.ToString("0", CultureInfo.InvariantCulture);
                this.friendshipText.Invalidate();
                this.walkText.Metric = ((int)pet.WalkedValue).ToString("N0", CultureInfo.InvariantCulture)
                    + " / " + ((int)pet.WalkNeed).ToString("N0", CultureInfo.InvariantCulture) + "px";
                this.walkText.Invalidate();
                this.friendshipProgress.Value = Math.Min(1000, Math.Max(0,
                    (int)Math.Round(displayedFriendship * 1000.0 / Math.Max(1.0, pet.FriendshipNeed))));
                this.walkProgress.Value = Math.Min(1000, Math.Max(0,
                    (int)Math.Round(pet.WalkedValue * 1000.0 / Math.Max(1.0, pet.WalkNeed))));
                this.foodBoost.Text = "● 포켓푸드 효과  ·  " + pet.FoodBoostLabel();
                this.evolutionNote.Text = this.EvolutionStatus(pet);
            }
            bool canFeed = pet != null && this.world.Options.Food > 0 && !pet.IsEvolving;
            bool canEvolve = pet != null && pet.CanEvolve();
            string feedReason = pet == null ? "포켓몬 없음" : pet.IsEvolving ? "진화 중" : "포켓푸드 없음";
            string evolveReason = pet == null ? "포켓몬 없음" : pet.NextKey == null
                ? "다음 진화 없음" : pet.IsEvolving ? "진화 중" : "조건 미달";
            foreach (Button button in new Button[] { this.homeFeed, this.petFeed })
                this.SetActionButton(button, canFeed, "먹이 주기", feedReason,
                    canFeed ? "포켓푸드 한 개를 사용합니다." : feedReason);
            foreach (Button button in new Button[] { this.homeEvolve, this.petEvolve })
                this.SetActionButton(button, canEvolve, "진화", evolveReason,
                    canEvolve ? "준비된 다음 단계로 진화합니다." : (pet == null ? evolveReason : this.EvolutionStatus(pet)));
            this.petRelease.Enabled = pet != null && pets.Length > 1;
            this.petRelease.Text = this.petRelease.Enabled ? "보내주기…" : "보내주기\r\n마지막 포켓몬";
            this.buttonHints.SetToolTip(this.petRelease, this.petRelease.Enabled
                ? "선택한 포켓몬을 목록에서 보냅니다." : "마지막 포켓몬은 보낼 수 없습니다.");
            this.homeRecall.Enabled = this.petRecall.Enabled = pet != null;
            this.pauseButton.Text = this.world.Paused ? "산책 재개" : "전체 일시정지";
            this.topmostButton.Text = "항상 위 " + (this.TopMost ? "켜짐" : "꺼짐");
            this.autostartButton.Text = "윈도우 시작 시 실행";
            foreach (Button button in this.scaleButtons)
                this.RefreshChoiceButton(button, Math.Abs(this.world.Options.Scale - (double)button.Tag) < 0.01);
            foreach (Button button in this.speedButtons)
                this.RefreshChoiceButton(button, Math.Abs(this.world.Options.Speed - (double)button.Tag) < 0.01);
            this.RefreshChoiceButton(this.topmostButton, this.TopMost);
            this.RefreshChoiceButton(this.pauseButton, this.world.Paused);
            this.RefreshChoiceButton(this.autostartButton, AutoStart.Enabled());
            int portfolio = this.world.StockPortfolioValue();
            this.homeStockShortcut.Text = "주식시장\r\n" + PetWorld.FormatWon(portfolio) + " · "
                + string.Format(CultureInfo.InvariantCulture, "{0:+0.0;-0.0;0.0}%  ›",
                    this.world.StockPortfolioChangePercent());
            this.stockHeadingHint.Text = this.world.MarketSessionText;
            this.stockPortfolio.Metric = PetWorld.FormatWon(portfolio) + " "
                + string.Format(CultureInfo.InvariantCulture, "({0:+0.0;-0.0;0.0}%)", this.world.StockPortfolioChangePercent());
            this.stockCash.Metric = PetWorld.FormatWon(this.world.Options.Coins);
            this.marketSummary.Caption = "시장 국면 · " + this.world.MarketRegimeLabel;
            this.marketSummary.Metric = this.world.MarketIsOpen
                ? this.world.MarketSecondsLeft + "초 후 갱신" : "휴장 중";
            this.stockPositionsPreview.Text = this.StockPositionPreview();
            this.stockMarketPreview.Text = this.StockMarketPreview();
            this.savedStatus.Text = "최근 저장됨 · " + this.RelativeSaveTime(this.world.LastSaveTime);
            this.stockPortfolio.Invalidate(); this.stockCash.Invalidate(); this.marketSummary.Invalidate();
        }

        private string RelativeSaveTime(DateTime savedAt)
        {
            int seconds = Math.Max(0, (int)(DateTime.Now - savedAt).TotalSeconds);
            if (seconds < 10) return "방금 전";
            if (seconds < 60) return seconds + "초 전";
            int minutes = seconds / 60;
            if (minutes < 60) return minutes + "분 전";
            return savedAt.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        private string StockPositionPreview()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < PetWorld.StockSlotCount && lines.Count < 3; i++)
            {
                int shares = this.world.Options.StockShares[i];
                if (shares <= 0) continue;
                lines.Add(this.world.StockName(i) + "  " + shares + "주  ·  "
                    + PetWorld.FormatWon(shares * this.world.Options.StockPrices[i]));
            }
            return lines.Count == 0 ? "보유 종목이 없습니다.\r\n전체 주식창에서 종목을 살펴보세요."
                : string.Join("\r\n", lines.ToArray());
        }

        private string StockMarketPreview()
        {
            int[] indexes = new int[PetWorld.StockSlotCount];
            for (int i = 0; i < indexes.Length; i++) indexes[i] = i;
            Array.Sort(indexes, delegate(int left, int right) {
                return Math.Abs(this.world.StockChangePercent(right)).CompareTo(
                    Math.Abs(this.world.StockChangePercent(left)));
            });
            List<string> lines = new List<string>();
            for (int i = 0; i < Math.Min(2, indexes.Length); i++)
            {
                int index = indexes[i];
                lines.Add(this.world.StockName(index) + "  "
                    + string.Format(CultureInfo.InvariantCulture, "{0:+0.0;-0.0;0.0}%", this.world.StockChangePercent(index)));
            }
            string news = string.IsNullOrEmpty(this.world.StockEvent) ? "새 소식을 기다리는 중"
                : this.world.StockEvent;
            lines.Add("최근 소식 · " + news);
            return string.Join("\r\n", lines.ToArray());
        }

        private void RefreshChoiceButton(Button button, bool selected)
        {
            if (button == null) return;
            string label = button.Text.StartsWith("✓ ", StringComparison.Ordinal)
                ? button.Text.Substring(2) : button.Text;
            button.Text = selected ? "✓ " + label : label;
            button.BackColor = selected ? Blue : PanelColor;
            button.ForeColor = selected ? Color.White : Ink;
            button.AccessibleDescription = selected ? "선택됨" : "선택되지 않음";
            GameActionButton gameButton = button as GameActionButton;
            if (gameButton != null) gameButton.EdgeColor = selected ? Ink : Line;
            button.Invalidate();
        }

        private void SetActionButton(Button button, bool enabled, string action, string reason, string hint)
        {
            if (button == null) return;
            button.Enabled = enabled;
            button.Text = enabled ? action : action + "\r\n" + reason;
            this.buttonHints.SetToolTip(button, hint);
        }

        private void SetPurchaseButton(Button button, bool affordable, string action)
        {
            if (button == null) return;
            button.Enabled = affordable;
            button.Text = affordable ? action : "잔액 부족";
            this.buttonHints.SetToolTip(button, affordable ? action + "할 수 있습니다."
                : "산책으로 돈을 더 모아야 합니다.");
        }

        private string EvolutionStatus(PetForm pet)
        {
            if (pet.NextKey == null) return "현재 등록된 다음 진화가 없습니다.";
            PokemonSprite next = Sprites.Find(pet.NextKey);
            string name = next == null ? pet.NextKey : next.NameKo;
            if (pet.IsEvolving) return "진화하는 중입니다…";
            if (pet.CanEvolve()) return name + "로 진화할 준비가 완료되었습니다!";
            List<string> needs = new List<string>();
            if (pet.FoodsLeft() > 0) needs.Add("포켓푸드 " + pet.FoodsLeft() + "개");
            if (pet.WalkLeft() > 0) needs.Add("산책 " + pet.WalkLeft().ToString("N0", CultureInfo.InvariantCulture) + "px");
            int dropLeft = Math.Max(0, pet.GrowthDropsNeed - this.world.Options.GrowthDrops);
            if (dropLeft > 0) needs.Add("성장의 물방울 " + dropLeft + "개");
            return "진화까지 " + string.Join(" · ", needs.ToArray());
        }

        private void FeedSelected() { PetForm pet = this.SelectedPet(); if (pet != null) this.world.Feed(pet); this.RefreshGameState(); }
        private void EvolveSelected() { PetForm pet = this.SelectedPet(); if (pet != null) pet.StartEvolving(); this.RefreshGameState(); }
        private void RecallSelected() { PetForm pet = this.SelectedPet(); if (pet != null) pet.Recall(); }

        private void ReleaseSelected()
        {
            PetForm pet = this.SelectedPet(); if (pet == null) return;
            PokemonSprite sprite = Sprites.Find(pet.SpriteKey);
            string name = sprite == null ? pet.SpriteKey : sprite.NameKo;
            if (MessageBox.Show(name + "을(를) 정말 보내줄까요?", "포켓몬 보내주기",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                this.world.Remove(pet); this.RefreshGameState();
            }
        }

        private void BuyRandom()
        {
            if (MessageBox.Show(PetWorld.FormatWon(PetWorld.PokemonPrice)
                + "을 사용해 새 포켓몬을 영입할까요?", "랜덤 영입",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int before = this.world.PetsSnapshot().Length;
            this.world.BuyRandomPet();
            PetForm[] after = this.world.PetsSnapshot();
            if (after.Length > before) {
                this.selectedIndex = after.Length - 1;
                PokemonSprite sprite = Sprites.Find(after[this.selectedIndex].SpriteKey);
                MessageBox.Show((sprite == null ? after[this.selectedIndex].SpriteKey : sprite.NameKo)
                    + "이(가) 새로운 친구가 되었습니다!", "영입 성공",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.SetShopFeedback("새 포켓몬 영입 완료 · 남은 잔액 "
                    + PetWorld.FormatWon(this.world.Options.Coins), true);
            }
            this.RefreshGameState();
        }

        private void BuyFoodFromShop()
        {
            int before = this.world.Options.Food;
            this.world.BuyFood();
            this.SetShopFeedback(this.world.Options.Food > before
                ? "포켓푸드 1개 구매 완료 · 남은 잔액 " + PetWorld.FormatWon(this.world.Options.Coins)
                : "포켓푸드를 구매할 잔액이 부족합니다.", this.world.Options.Food > before);
            this.RefreshGameState();
        }

        private void BuyDropFromShop()
        {
            int before = this.world.Options.GrowthDrops;
            this.world.BuyGrowthDrop();
            this.SetShopFeedback(this.world.Options.GrowthDrops > before
                ? "성장의 물방울 1개 구매 완료 · 남은 잔액 " + PetWorld.FormatWon(this.world.Options.Coins)
                : "성장의 물방울을 구매할 잔액이 부족합니다.", this.world.Options.GrowthDrops > before);
            this.RefreshGameState();
        }

        private void SetShopFeedback(string text, bool success)
        {
            if (this.shopFeedback == null) return;
            this.shopFeedback.Text = (success ? "●  " : "!  ") + text;
            this.shopFeedback.ForeColor = success ? Green : Red;
        }

        private void ConfirmQuit()
        {
            if (MessageBox.Show("포켓몬 센터와 모든 포켓몬을 종료할까요?", "게임 종료",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) this.world.QuitAll();
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
        public const int StockMaxOrderQuantity = int.MaxValue;
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
        // Python판과 같은 주 성향 12종. 각 배열의 같은 위치가 하나의 성향이다.
        private static readonly string[] StockPrimaryTraitNames = {
            "안정형", "성장형", "가치형", "추세형", "반전형", "뉴스형",
            "시장추종형", "역행형", "박스권형", "개장형", "마감형", "투기형"
        };
        private static readonly string[] StockPrimaryTraitDescriptions = {
            "낮은 변동성과 강한 기준가 회귀", "완만한 상승 기대와 보통 수준의 조정",
            "가격이 낮아질수록 반등력이 강해짐", "상승·하락 방향이 비교적 오래 지속",
            "한 방향으로 움직인 뒤 되돌림이 잦음", "평소에는 조용하지만 이벤트에 크게 반응",
            "전체 시장의 상승·하락 국면을 강하게 추종", "전체 시장과 반대로 움직일 가능성이 큼",
            "기준가 주변의 일정 범위를 반복해서 오감", "개장 직후 10분 동안 움직임이 커짐",
            "마감 전 10분 동안 움직임이 커짐", "급등락이 잦고 상장폐지 위험이 큼"
        };
        private static readonly double[] StockPrimaryNoise =
            { .55, .80, .70, .85, .75, .45, .70, .70, .50, .75, .75, 1.05 };
        private static readonly double[] StockPrimaryDrift =
            { 0, .22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly double[] StockPrimaryTrendChange =
            { .28, .18, .26, .08, .32, .20, .18, .20, .30, .18, .18, .12 };
        private static readonly double[] StockPrimaryTrend =
            { .50, 1.00, .65, 1.70, -.90, .60, .80, .70, .40, 1.00, 1.00, 1.30 };
        private static readonly double[] StockPrimaryReversion =
            { 1.50, .85, 2.00, .50, 1.50, 1.00, .75, 1.00, 2.50, .80, .80, .25 };
        private static readonly double[] StockPrimaryMarket =
            { .55, 1.00, .80, 1.00, .75, .55, 1.80, -.80, .40, 1.00, 1.00, 1.25 };
        private static readonly double[] StockPrimaryEvent =
            { .70, 1.00, .90, .95, .90, 1.70, .90, .90, .65, 1.00, 1.00, 1.35 };
        private static readonly int[] StockPrimaryPhase =
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 0 }; // 1=개장, 2=마감
        private static readonly double[] StockPrimaryBurst =
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, .03, .03, .08 };

        // 보조 성향 8종. 주 성향과 조합해 최대 96가지 움직임을 만든다.
        private static readonly string[] StockSecondaryTraitNames = {
            "낙관적", "비관적", "민첩함", "둔감함", "회복력", "취약함", "과열주의", "이벤트저항"
        };
        private static readonly string[] StockSecondaryTraitDescriptions = {
            "상승 쪽으로 아주 약한 힘을 받음", "하락 쪽으로 아주 약한 힘을 받음",
            "추세와 시장 변화에 빠르게 반응", "시장과 뉴스의 영향이 천천히 반영",
            "급락 뒤 기준가를 향한 반등력이 강함", "악재와 공포장에 더 크게 흔들림",
            "연속 상승 뒤 조정 압력이 커짐", "호재와 악재 모두 비교적 작게 반영"
        };
        private static readonly double[] StockSecondaryDrift =
            { .12, -.12, 0, 0, 0, 0, 0, 0 };
        private static readonly double[] StockSecondaryNoise =
            { 1, 1, 1.10, .80, 1, 1.08, 1, 1 };
        private static readonly double[] StockSecondaryMarket =
            { 1, 1, 1.20, .70, 1, 1, 1, 1 };
        private static readonly double[] StockSecondaryEvent =
            { 1, 1, 1.10, .75, .90, 1, 1, .55 };
        private static readonly double[] StockSecondaryTrendChange =
            { 1, 1, 1.25, .75, 1, 1, 1, 1 };
        private static readonly double[] StockSecondaryRecovery =
            { 0, 0, 0, 0, .10, 0, 0, 0 };
        private static readonly double[] StockSecondaryOverheat =
            { 0, 0, 0, 0, 0, 0, .12, 0 };
        private static readonly double[] StockSecondaryNegative =
            { 1, 1, 1, 1, .82, 1.25, 1, 1 };
        private static readonly string[] MarketRegimeNames = {
            "횡보장", "상승장", "하락장", "과열장", "공포장"
        };
        private static readonly double[] MarketRegimeDrifts = { 0.0, 2.0, -2.0, 4.0, -4.0 };
        private static readonly int[] MarketRegimeWeights = { 3, 2, 2, 1, 1 };
        public static readonly ToolStripRenderer PokemonMenuRenderer =
            new PokemonMenuRenderer();
        private static readonly Font PokemonMenuTitleFont =
            UiFonts.Create(10.0f, FontStyle.Bold);

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
            return FormatWon((long)amount);
        }

        public static string FormatWon(long amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture) + "원";
        }

        public static string FormatSignedWon(int amount)
        {
            return FormatSignedWon((long)amount);
        }

        public static string FormatSignedWon(long amount)
        {
            return amount.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture) + "원";
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
        public DateTime LastSaveTime { get; private set; }

        private NotifyIcon tray;

        public PetWorld(Options options)
        {
            this.Options = options;
            this.LastSaveTime = DateTime.Now;
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
                menu.Font = UiFonts.Create(9.0f);
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

        /// <summary>게임 센터가 현재 포켓몬 목록을 안전하게 표시할 수 있게 복사본을 준다.</summary>
        public PetForm[] PetsSnapshot()
        {
            return this.pets.ToArray();
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
            this.LastSaveTime = DateTime.Now;
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
            double eventPercent = 0.0;
            string eventText = "";
            if (active.Count > 0 && this.Random.NextDouble() < StockEventChance)
            {
                eventIndex = active[this.Random.Next(active.Count)];
                bool positive = this.Random.Next(2) == 0;
                eventPercent = this.StockEventPercent(eventIndex, positive);
                eventPercent = this.StockEventChange(eventIndex, eventPercent);
                eventText = this.StockName(eventIndex) + " "
                    + this.StockEventText(eventIndex, positive) + "  "
                    + string.Format(CultureInfo.InvariantCulture,
                        "{0:+0;-0;0}%", eventPercent);
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
            int primary = this.StockPrimaryTraitId(index);
            int secondary = this.StockSecondaryTraitId(index);
            double trendChange = StockPrimaryTrendChange[primary]
                * StockSecondaryTrendChange[secondary];
            if (this.Random.NextDouble() < Math.Min(.75, trendChange))
            {
                this.stockTrends[index] = this.Random.Next(-1, 2);
            }
            int listing = this.Options.StockListingIds[index] % StockStartingPrices.Length;
            double priceGap = (StockStartingPrices[listing] - this.Options.StockPrices[index])
                * 100.0 / StockStartingPrices[listing];
            double pullRate = volatility <= 10 ? 0.20 : volatility <= 18 ? 0.12 : 0.06;
            double meanReversion = this.Options.StockPrices[index] < StockCrisisPrice ? 0.0
                : Math.Max(-5.0, Math.Min(5.0,
                    priceGap * pullRate * StockPrimaryReversion[primary]));
            double trend = this.stockTrends[index] * Math.Max(1.0, volatility * 0.16)
                * StockPrimaryTrend[primary];
            double noise = this.Random.Next(-volatility, volatility + 1)
                * StockPrimaryNoise[primary] * StockSecondaryNoise[secondary];
            double market = MarketRegimeDrifts[this.marketRegime]
                * StockPrimaryMarket[primary] * StockSecondaryMarket[secondary];
            if (market < 0.0)
            {
                market *= StockSecondaryNegative[secondary];
            }
            double change = noise + market + trend + meanReversion
                + StockPrimaryDrift[primary] + StockSecondaryDrift[secondary];
            if (priceGap > 0.0 && StockPrimaryTraitNames[primary] == "가치형")
            {
                change += Math.Min(4.0, priceGap * .08);
            }
            if (priceGap > 0.0 && StockSecondaryRecovery[secondary] > 0.0)
            {
                change += Math.Min(3.0, priceGap * StockSecondaryRecovery[secondary]);
            }
            double sessionChange = this.StockChangePercent(index);
            if (StockPrimaryTraitNames[primary] == "반전형" && Math.Abs(sessionChange) > 8.0)
            {
                change -= (sessionChange >= 0.0 ? 1.0 : -1.0)
                    * Math.Min(4.0, Math.Abs(sessionChange) * .08);
            }
            if (sessionChange > 12.0 && StockSecondaryOverheat[secondary] > 0.0)
            {
                change -= Math.Min(4.0,
                    (sessionChange - 12.0) * StockSecondaryOverheat[secondary]);
            }
            int elapsed = MarketOpenSeconds - this.marketSessionSecondsLeft;
            if (StockPrimaryPhase[primary] == 1)
            {
                change *= elapsed <= 10 * 60 ? 1.65 : .75;
            }
            else if (StockPrimaryPhase[primary] == 2)
            {
                change *= this.marketSessionSecondsLeft <= 10 * 60 ? 1.65 : .75;
            }
            if (StockPrimaryBurst[primary] > 0.0
                && this.Random.NextDouble() < StockPrimaryBurst[primary])
            {
                change += (this.Random.NextDouble() < .5 ? -1.0 : 1.0) * volatility * .90;
            }
            return change * MarketTickScale;
        }

        private double StockEventChange(int index, double eventPercent)
        {
            int primary = this.StockPrimaryTraitId(index);
            int secondary = this.StockSecondaryTraitId(index);
            double multiplier = StockPrimaryEvent[primary] * StockSecondaryEvent[secondary];
            if (eventPercent < 0.0)
            {
                multiplier *= StockSecondaryNegative[secondary];
            }
            return eventPercent * multiplier;
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
            return StockPrimaryTraitNames[this.StockPrimaryTraitId(index)] + " · "
                + StockSecondaryTraitNames[this.StockSecondaryTraitId(index)];
        }

        public string StockPrimaryProfile(int index)
        {
            return StockPrimaryTraitNames[this.StockPrimaryTraitId(index)];
        }

        public string StockProfileDescription(int index)
        {
            return StockPrimaryTraitDescriptions[this.StockPrimaryTraitId(index)] + " · "
                + StockSecondaryTraitDescriptions[this.StockSecondaryTraitId(index)];
        }

        public string StockRiskLabel(int index)
        {
            int primary = this.StockPrimaryTraitId(index);
            int secondary = this.StockSecondaryTraitId(index);
            double effective = this.StockVolatility(index)
                * StockPrimaryNoise[primary] * StockSecondaryNoise[secondary];
            if (StockPrimaryTraitNames[primary] == "투기형"
                || StockSecondaryTraitNames[secondary] == "취약함" || effective > 19.0)
            {
                return "매우 높음";
            }
            if (effective > 13.0 || StockPrimaryBurst[primary] > 0.0)
            {
                return "높음";
            }
            return effective > 8.0 ? "보통" : "낮음";
        }

        public int StockBuyCost(int index)
        {
            int price = this.Options.StockPrices[index];
            return price + (int)Math.Ceiling(price * StockFeeRate);
        }

        public int StockMaximumBuyQuantity(int index)
        {
            int affordable = this.Options.Coins / Math.Max(1, this.StockBuyCost(index));
            int remainingCapacity = Math.Max(0, StockMaxOrderQuantity - this.Options.StockShares[index]);
            return Math.Max(0, Math.Min(affordable, remainingCapacity));
        }

        public int StockMaximumSellQuantity(int index)
        {
            return Math.Max(0, this.Options.StockShares[index]);
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
            return this.StockPositionText(index, true);
        }

        public string StockPositionText(int index, bool includePercent)
        {
            string trend = this.stockTrends[index] < 0 ? "하락 추세"
                : this.stockTrends[index] > 0 ? "상승 추세" : "횡보";
            if (this.Options.StockShares[index] <= 0)
            {
                return "보유 주식 없음\n매수 후 평균 매입가·평가액·손익이 표시됩니다.";
            }
            string percentText = includePercent
                ? string.Format(" ({0:+0.0;-0.0;0.0}%)", this.StockProfitPercent(index)) : "";
            return string.Format("보유 {0}주  ·  평균 매입가 {1}\n평가액 {2}  ·  손익 {3:+#,0;-#,0;0}원{4}\n{5}",
                this.Options.StockShares[index], FormatWon(this.Options.StockAveragePrices[index]),
                FormatWon(this.StockHoldingValue(index)), this.StockHoldingProfit(index),
                percentText, trend);
        }

        private int StockVolatility(int index)
        {
            return StockVolatilities[this.Options.StockListingIds[index] % StockVolatilities.Length];
        }

        private int StockPrimaryTraitId(int index)
        {
            return this.Options.StockPrimaryTraitIds[index] % StockPrimaryTraitNames.Length;
        }

        private int StockSecondaryTraitId(int index)
        {
            return this.Options.StockSecondaryTraitIds[index] % StockSecondaryTraitNames.Length;
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

        public int StockPortfolioProfit()
        {
            return this.StockPortfolioValue() - this.StockPortfolioCostBasis();
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

        private int StockEventPercent(int index, bool positive)
        {
            int listing = this.Options.StockListingIds[index] % StockNames.Length;
            int[] good = { 18, 11, 15, 24, 30, 38, 20, 26, 14, 29, 17, 34 };
            int[] bad = { -16, -12, -14, -22, -28, -35, -18, -23, -13, -27, -16, -31 };
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
            List<int> primaryCandidates = new List<int>();
            for (int trait = 0; trait < StockPrimaryTraitNames.Length; trait++)
            {
                bool used = false;
                for (int other = 0; other < StockSlotCount; other++)
                {
                    if (other != index && !this.IsStockDelisted(other)
                        && this.StockPrimaryTraitId(other) == trait)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used)
                {
                    primaryCandidates.Add(trait);
                }
            }
            if (primaryCandidates.Count == 0)
            {
                for (int trait = 0; trait < StockPrimaryTraitNames.Length; trait++)
                {
                    primaryCandidates.Add(trait);
                }
            }
            this.Options.StockPrimaryTraitIds[index] =
                primaryCandidates[this.Random.Next(primaryCandidates.Count)];
            this.Options.StockSecondaryTraitIds[index] =
                this.Random.Next(StockSecondaryTraitNames.Length);
            this.Options.StockPrices[index] = StockStartingPrices[next];
            this.Options.StockShares[index] = 0;
            this.Options.StockAveragePrices[index] = 0;
            this.Options.StockDelisted[index] = 0;
            this.Options.StockRelistSeconds[index] = 0;
            this.Options.StockHaltSeconds[index] = 0;
            this.stockHistory[index].Clear();
            this.stockHistory[index].Add(this.Options.StockPrices[index]);
            this.stockSessionOpeningPrices[index] = this.Options.StockPrices[index];
            this.AnnounceStockEvent(this.StockName(index) + " 신규 상장! "
                + this.StockProfile(index));
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
                GameMenuForm existing = this.gameMenu as GameMenuForm;
                if (existing != null)
                {
                    existing.RefreshGameState();
                }
                return;
            }
            GameMenuForm form = new GameMenuForm(this);
            form.FormClosed += delegate { this.gameMenu = null; };
            this.gameMenu = form;
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

        /// <summary>지금 있는 포켓몬들. 테스트가 들여다본다.</summary>
        internal List<PetForm> Pets
        {
            get { return this.pets; }
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
                Log.Write("UI font: " + UiFonts.Description);
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
