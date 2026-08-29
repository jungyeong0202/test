// 배포하는 프로그램(C# 판)을 직접 검사한다.
//
// 예전에는 테스트가 파이썬 판에만 있었다. 정작 사용자가 실행하는 것은 이 exe 인데,
// 이쪽을 지키는 테스트가 하나도 없어서 윈도우에서만 죽는 문제를 오래 못 잡았다.
//
//     sh tools/run_tests.sh          (리눅스/맥, Mono 필요)
//     csharp\run_tests.bat           (윈도우)
//
// 외부 라이브러리를 쓰지 않는다. 윈도우에 기본으로 있는 csc.exe 만으로 돌아야 하고,
// 이 저장소는 패키지 관리자를 쓰지 않기 때문이다.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PokemonTaskbar.Tests
{
    /// <summary>검사 하나하나의 결과를 모아 둔다.</summary>
    public static class Check
    {
        private static readonly List<string> Failures = new List<string>();
        private static int passed;
        private static string section = "";

        public static void Section(string name)
        {
            section = name;
            Console.WriteLine("[" + name + "]");
        }

        public static void That(bool ok, string what)
        {
            if (ok)
            {
                passed++;
                Console.WriteLine("  ok   " + what);
            }
            else
            {
                Failures.Add(section + " / " + what);
                Console.WriteLine("  X    " + what);
            }
        }

        public static void Equal(object got, object want, string what)
        {
            bool ok = got == null ? want == null : got.Equals(want);
            That(ok, ok ? what : what + " (기대 " + Show(want) + ", 실제 " + Show(got) + ")");
        }

        public static void Near(double got, double want, double slack, string what)
        {
            bool ok = Math.Abs(got - want) <= slack;
            That(ok, ok ? what : what + string.Format(" (기대 {0}±{1}, 실제 {2})", want, slack, got));
        }

        private static string Show(object value)
        {
            return value == null ? "null" : value.ToString();
        }

        public static int Report()
        {
            Console.WriteLine();
            if (Failures.Count == 0)
            {
                Console.WriteLine(passed + "개 모두 통과");
                return 0;
            }
            Console.WriteLine(passed + "개 통과, " + Failures.Count + "개 실패:");
            foreach (string failure in Failures)
            {
                Console.WriteLine("  - " + failure);
            }
            return 1;
        }
    }

    /// <summary>검사가 끝나면 창을 확실히 닫아 주는 얇은 껍데기.</summary>
    internal sealed class TestWorld : IDisposable
    {
        public readonly PetWorld World;

        public TestWorld(Options options)
        {
            this.World = new PetWorld(options);
        }

        public List<PetForm> Pets
        {
            get { return this.World.Pets; }
        }

        public void Dispose()
        {
            foreach (PetForm pet in this.Pets.ToArray())
            {
                pet.Close();
                pet.Dispose();
            }
            this.Pets.Clear();
        }
    }

    public static class Program
    {
        /// <summary>테스트마다 깨끗한 설정에서 시작한다.
        ///
        /// 앱은 여러 상황에서 설정을 파일에 저장한다. 한 테스트가 남긴 값을 다음
        /// 테스트가 물려받으면(예: 큰 --offset 이 남아 창이 화면 밖으로 밀린다)
        /// 엉뚱한 곳에서 실패한다.
        /// </summary>
        private static string settingsPath;

        private static void FreshSettings()
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }

        private static Options Parse(params string[] argv)
        {
            FreshSettings();
            return PokemonTaskbar.Program.Parse(argv);
        }

        private static TestWorld World(params string[] argv)
        {
            return new TestWorld(Parse(argv));
        }

        [STAThread]
        public static int Main()
        {
            string folder = Path.Combine(Path.GetTempPath(),
                "pokemon-taskbar-cs-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(folder);
            settingsPath = Path.Combine(folder, "settings.txt");
            Environment.SetEnvironmentVariable(SettingsFile.EnvOverride, settingsPath);

            Application.EnableVisualStyles();
            try
            {
                Options();
                Settings();
                Sprites();
                Images();
                Ground();
                Movement();
                Dragging();
                Poses();
                IdleAnimation();
                Income();
                Economy();
                StockText();
                StockNews();
                StockSpecialEvents();
                Relisting();
                EventSpread();
                ShortSelling();
                DangerousActions();
                Lifecycle();
                Evolution();
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
            return Check.Report();
        }

        // --- 명령줄 옵션 -------------------------------------------------

        private static void Options()
        {
            Check.Section("명령줄 옵션");

            Options basic = Parse();
            Check.Equal(basic.Error, null, "인자가 없어도 오류가 없다");
            Check.Equal(basic.Species.Count, 1, "기본은 한 마리");
            Check.Equal(basic.Species[0], "pikachu", "기본은 피카츄");

            Check.Equal(Parse("-p", "squirtle").Species[0], "squirtle", "-p 로 고른 포켓몬");
            Check.That(Parse("-p", "mudkip").Error != null, "모르는 포켓몬은 거부한다");
            Check.That(Parse("--scale", "0").Error != null, "--scale 0 은 거부한다");
            Check.That(Parse("--scale", "숫자아님").Error != null, "--scale 이 숫자가 아니면 거부한다");
            Check.That(Parse("-p").Error != null, "값이 빠진 -p 는 거부한다");

            Check.Equal(Parse("--count", "4").Species.Count, 4, "--count 만큼 늘어난다");
            Check.That(Parse("--on-taskbar").OnTaskbar, "--on-taskbar 가 켜진다");
            Check.Equal(Parse("--offset", "40").Offset, 40, "--offset 이 그대로 들어간다");

            // 진화체는 진화로만 만나야 한다.
            bool leaked = false;
            for (int round = 0; round < 60; round++)
            {
                foreach (string key in Parse("--count", "5").Species)
                {
                    if (PokemonTaskbar.Sprites.IsEvolvedOnly(key))
                    {
                        leaked = true;
                    }
                }
            }
            Check.That(!leaked, "--count 는 진화체를 나눠 주지 않는다");
            Check.Equal(Parse("-p", "wartortle").Species[0], "wartortle",
                "이름을 직접 대면 진화체도 쓸 수 있다");
        }

        // --- 설정 파일 ---------------------------------------------------

        private static void Settings()
        {
            Check.Section("설정 파일");
            FreshSettings();

            Options options = PokemonTaskbar.Program.Parse(new string[0]);
            options.Scale = 6.0;
            options.Speed = 95.0;
            options.Offset = 12;
            options.Coins = 4242;
            SettingsFile.Save(options, new List<string>(new string[] { "ditto", "pikachu" }));
            Check.That(File.Exists(settingsPath), "설정 파일이 만들어진다");

            Options loaded = PokemonTaskbar.Program.Parse(new string[0]);
            Check.Equal(loaded.Scale, 6.0, "크기를 다시 읽는다");
            Check.Equal(loaded.Speed, 95.0, "속도를 다시 읽는다");
            Check.Equal(loaded.Offset, 12, "띄울 높이를 다시 읽는다");
            Check.Equal(loaded.Coins, 4242, "돈을 다시 읽는다");
            Check.Equal(string.Join(",", loaded.Species.ToArray()), "ditto,pikachu",
                "포켓몬 목록을 다시 읽는다");

            // 명령줄이 저장값을 이긴다.
            Options given = PokemonTaskbar.Program.Parse(new string[] { "--scale", "3" });
            Check.Equal(given.Scale, 3.0, "명령줄이 저장된 값보다 우선한다");

            File.WriteAllText(settingsPath,
                "scale = 헬로\nspeed = -5\nspecies = 없는놈, ditto\n쓰레기줄\n");
            Options broken = PokemonTaskbar.Program.Parse(new string[0]);
            Check.Equal(broken.Scale, 4.5, "이상한 크기는 기본값으로 되돌린다");
            Check.Equal(broken.Speed, 55.0, "이상한 속도는 기본값으로 되돌린다");
            Check.Equal(string.Join(",", broken.Species.ToArray()), "ditto",
                "모르는 포켓몬은 걸러 낸다");
            FreshSettings();
        }

        // --- 도트 데이터 -------------------------------------------------

        private static void Sprites()
        {
            Check.Section("도트 데이터");

            Check.That(PokemonTaskbar.Sprites.All.Count >= 6, "포켓몬이 여섯 마리 이상 있다");
            foreach (PokemonSprite sprite in PokemonTaskbar.Sprites.All)
            {
                List<Color?[][]> frames = SpriteFactory.Frames(sprite);
                Check.That(frames.Count >= 2, sprite.Key + ": 프레임이 두 장 이상");

                int width = frames[0][0].Length;
                int height = frames[0].Length;
                bool rectangular = true;
                int filled = 0;
                foreach (Color?[][] frame in frames)
                {
                    if (frame.Length != height)
                    {
                        rectangular = false;
                    }
                    foreach (Color?[] row in frame)
                    {
                        if (row.Length != width)
                        {
                            rectangular = false;
                        }
                    }
                }
                foreach (Color?[] row in frames[0])
                {
                    foreach (Color? cell in row)
                    {
                        if (cell != null)
                        {
                            filled++;
                        }
                    }
                }
                Check.That(rectangular, sprite.Key + ": 프레임 크기가 모두 같다");
                Check.That(filled > 50, sprite.Key + ": 보이는 도트가 충분히 있다");

                if (sprite.Hops)
                {
                    Check.That(frames.Count >= 3,
                        sprite.Key + ": 뛰는 포켓몬은 [평소/웅크림/늘어남] 세 장이 필요하다");
                }
                if (sprite.Floats)
                {
                    Check.That(!sprite.Bounce,
                        sprite.Key + ": 떠다니는 포켓몬은 프레임이 흔들림을 담당한다");
                }
            }

            // 투명 처리에 쓰는 색을 스프라이트가 쓰면 그 부분이 뚫린다.
            bool usesColorKey = false;
            foreach (PokemonSprite sprite in PokemonTaskbar.Sprites.All)
            {
                foreach (KeyValuePair<char, string> pair in sprite.Palette)
                {
                    if (pair.Value.ToLowerInvariant() == "#ff00ff")
                    {
                        usesColorKey = true;
                    }
                }
            }
            Check.That(!usesColorKey, "팔레트에 투명색(#ff00ff)을 쓰지 않는다");

            Check.That(PokemonTaskbar.Sprites.IsEvolvedOnly("wartortle"),
                "어니부기는 진화로만 만난다");
            Check.That(!PokemonTaskbar.Sprites.BaseSpecies().Exists(
                delegate(PokemonSprite s) { return s.Key == "wartortle"; }),
                "진화체는 처음 고를 수 있는 목록에 없다");
            Check.Equal(PokemonTaskbar.Sprites.Find("squirtle").EvolvesTo, "wartortle",
                "꼬부기는 어니부기가 된다");
            Check.Equal(PokemonTaskbar.Sprites.Find("pikachu").EvolvesTo, "raichu",
                "피카츄는 라이츄가 된다");
            // 세 단계짜리 줄기. 마지막만 더 갈 곳이 없다.
            Check.Equal(PokemonTaskbar.Sprites.Find("bulbasaur").EvolvesTo, "ivysaur",
                "이상해씨는 이상해풀이 된다");
            Check.Equal(PokemonTaskbar.Sprites.Find("ivysaur").EvolvesTo, "venusaur",
                "이상해풀은 이상해꽃이 된다");
            Check.Equal(PokemonTaskbar.Sprites.Find("venusaur").EvolvesTo, null,
                "이상해꽃은 더 진화하지 않는다");
            Check.Equal(PokemonTaskbar.Sprites.Find("charmander").EvolvesTo, "charmeleon",
                "파이리는 리자드가 된다");
            Check.Equal(PokemonTaskbar.Sprites.Find("charmeleon").EvolvesTo, "charizard",
                "리자드는 리자몽이 된다");
            Check.Equal(PokemonTaskbar.Sprites.Find("wartortle").EvolvesTo, "blastoise",
                "어니부기는 거북왕이 된다");
        }

        // --- 움직임 -------------------------------------------------------

        private static void Movement()
        {
            Check.Section("움직임");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetForm pet = world.Pets[0];
                Check.That(pet.Bounds.Bottom <= Screen.PrimaryScreen.Bounds.Bottom,
                    "창이 화면 아래로 넘어가지 않는다");
                Check.That(pet.Bounds.Top >= Screen.PrimaryScreen.Bounds.Top,
                    "창이 화면 위로 넘어가지 않는다");

                double start = pet.Position;
                double farthest = 0.0;
                for (int i = 0; i < 150; i++)
                {
                    pet.Tick();
                    farthest = Math.Max(farthest, Math.Abs(pet.Position - start));
                }
                Check.That(farthest > 1.0, "시간이 지나면 걸어서 이동한다");
            }

            using (TestWorld world = World("-p", "mew"))
            {
                PetForm pet = world.Pets[0];
                Check.That(pet.Lift > 0.0, "뮤는 처음부터 떠 있다");
                double lowest = pet.Lift;
                for (int i = 0; i < 400; i++)
                {
                    pet.Tick();
                    lowest = Math.Min(lowest, pet.Lift);
                }
                Check.That(lowest > 0.0, "뮤는 바닥에 내려앉지 않는다");
            }

            using (TestWorld world = World("-p", "ditto"))
            {
                PetForm pet = world.Pets[0];
                double highest = 0.0;
                bool touched = false;
                for (int i = 0; i < 300; i++)
                {
                    pet.Tick();
                    highest = Math.Max(highest, pet.Lift);
                    if (pet.Lift == 0.0)
                    {
                        touched = true;
                    }
                }
                Check.That(highest > 5.0, "메타몽은 뛰어오른다");
                Check.That(touched, "메타몽은 다시 바닥에 닿는다");
            }
        }

        // --- 그림 그리기 ---------------------------------------------------

        private static void Images()
        {
            Check.Section("그림 그리기");

            Color?[][] grid = new Color?[][] {
                new Color?[] { Color.Red, null, null },
                new Color?[] { null, Color.Blue, null },
            };
            using (Bitmap plain = SpriteFactory.Render(grid, 1.0, false))
            using (Bitmap flipped = SpriteFactory.Render(grid, 1.0, true))
            {
                Check.Equal(plain.Width, 3, "배율 1이면 폭이 그대로다");
                Check.Equal(plain.Height, 2, "배율 1이면 높이가 그대로다");
                Check.That(plain.GetPixel(0, 0).R > 200 && plain.GetPixel(0, 0).A == 255,
                    "첫 도트가 제 색으로 찍힌다");
                Check.That(plain.GetPixel(2, 0).A == 0, "빈 칸은 투명하다");
                Check.That(flipped.GetPixel(2, 0).R > 200, "뒤집으면 좌우가 바뀐다");
            }

            // 소수 배율에서도 가로세로 비율이 크게 어긋나면 안 된다.
            Color?[][] square = new Color?[20][];
            for (int y = 0; y < 20; y++)
            {
                square[y] = new Color?[20];
                for (int x = 0; x < 20; x++)
                {
                    square[y][x] = Color.Red;
                }
            }
            using (Bitmap scaled = SpriteFactory.Render(square, 1.5, false))
            {
                Check.Equal(scaled.Width, 30, "소수 배율에서 폭이 맞는다");
                Check.Equal(scaled.Height, 30, "소수 배율에서 높이가 맞는다");
                Check.Equal(scaled.Width, scaled.Height, "정사각형은 정사각형으로 남는다");
            }

            // 걸을 때 몸 전체가 움직인다. 프레임과 자세가 같은 캔버스에 놓여야
            // 갈아 끼울 때 튀지 않는다.
            PokemonSprite pikachu = PokemonTaskbar.Sprites.Find("pikachu");
            List<Color?[][]> frames = SpriteFactory.Frames(pikachu);
            List<Color?[][]> walking = SpriteFactory.WholeWalkFrames(frames);
            Check.Equal(walking.Count, frames.Count, "걷기 프레임 수가 그대로다");
            int wide = walking[0][0].Length;
            int tall = walking[0].Length;
            bool sameCanvas = true;
            foreach (Color?[][] frame in walking)
            {
                if (frame.Length != tall || frame[0].Length != wide)
                {
                    sameCanvas = false;
                }
            }
            Check.That(sameCanvas, "걷기 프레임이 모두 같은 캔버스에 놓인다");
            foreach (KeyValuePair<string, Color?[][]> pose in SpriteFactory.Poses(pikachu))
            {
                Color?[][] padded = SpriteFactory.PadOnGround(pose.Value, wide, tall);
                Check.That(padded.Length == tall && padded[0].Length == wide,
                    pose.Key + " 자세도 같은 캔버스에 놓인다");
            }
        }

        // --- 바닥선과 크기 --------------------------------------------------

        private static void Ground()
        {
            Check.Section("바닥선과 크기");

            Rectangle screen = Screen.PrimaryScreen.Bounds;
            using (TestWorld world = World("-p", "pikachu"))
            {
                PetForm pet = world.Pets[0];
                Check.That(pet.BaseY + pet.WindowH <= screen.Bottom,
                    "창 아래가 화면을 넘지 않는다");
                Check.That(pet.BaseY >= screen.Top, "창 위가 화면을 넘지 않는다");
                Check.That(pet.WindowW > pet.SpriteW, "진화 번쩍임이 들어갈 가로 여백이 있다");
                Check.That(pet.WindowH > pet.SpriteH, "진화 번쩍임이 들어갈 세로 여백이 있다");
            }

            // --offset 을 아무리 크게 줘도 창은 화면 안에 있어야 한다.
            foreach (int offset in new int[] { 0, 500, 5000, -500 })
            {
                using (TestWorld world = World("--offset", offset.ToString()))
                {
                    PetForm pet = world.Pets[0];
                    Check.That(pet.BaseY >= screen.Top && pet.BaseY + pet.WindowH <= screen.Bottom,
                        "--offset " + offset + " 에서도 화면 안에 있다");
                }
            }

            using (TestWorld plain = World("-p", "pikachu"))
            using (TestWorld lifted = World("-p", "pikachu", "--offset", "40"))
            {
                Check.Equal(lifted.Pets[0].BaseY, plain.Pets[0].BaseY - 40,
                    "--offset 만큼 위로 올라간다");
            }
        }

        // --- 끌기 -----------------------------------------------------------

        private static void Dragging()
        {
            Check.Section("끌기");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetForm pet = world.Pets[0];
                pet.Position = 100;
                pet.Press(100, pet.BaseY);
                Check.That(pet.IsDragging, "누르면 끌기가 시작된다");
                pet.DragTo(300, pet.BaseY - 120);
                Check.Near(pet.Position, 300, 1.0, "손을 따라 가로로 움직인다");
                Check.Near(pet.Lift, 120, 1.0, "손을 따라 위로 올라간다");

                double before = pet.Position;
                for (int i = 0; i < 20; i++)
                {
                    pet.Tick();
                }
                Check.Equal(pet.Position, before, "들려 있는 동안에는 스스로 걷지 않는다");

                pet.Release(300, pet.BaseY - 120);
                Check.That(!pet.IsDragging, "놓으면 끌기가 끝난다");
                bool landed = false;
                for (int i = 0; i < 120 && !landed; i++)
                {
                    pet.Tick();
                    landed = pet.Lift == 0.0;
                }
                Check.That(landed, "놓으면 바닥으로 떨어진다");
            }

            // 화면 밖으로 끌어도 붙잡아 둔다.
            using (TestWorld world = World("-p", "pikachu"))
            {
                PetForm pet = world.Pets[0];
                pet.Press(100, pet.BaseY);
                pet.DragTo(-5000, pet.BaseY + 5000);
                Check.That(pet.Position >= 0, "왼쪽으로 넘어가지 않는다");
                Check.That(pet.Lift >= 0.0, "바닥 아래로 내려가지 않는다");
                pet.DragTo(99999, -99999);
                Check.That(pet.Lift <= pet.BaseY, "화면 위로 넘어가지 않는다");
            }

            // 거의 움직이지 않았으면 클릭으로 보고 폴짝 뛴다.
            using (TestWorld world = World("-p", "pikachu"))
            {
                PetForm pet = world.Pets[0];
                pet.Press(100, pet.BaseY);
                pet.Release(100, pet.BaseY);
                pet.Tick();
                Check.That(pet.Lift > 0.0, "짧게 누르면 폴짝 뛴다");
            }
        }

        // --- 벌이 ------------------------------------------------------------

        private static void Income()
        {
            Check.Section("벌이");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetForm pet = world.Pets[0];
                PetWorld app = world.World;

                // 걸은 거리가 아니라 흐른 시간으로 번다. 서 있어도 들어와야 한다.
                pet.StandStillForTest();
                int before = app.Options.Coins;
                double standX = pet.Position;
                for (int i = 0; i < 250; i++)     // 10초
                {
                    pet.Tick();
                    pet.StandStillForTest();
                }
                int earned = app.Options.Coins - before;
                Check.That(earned > 0, "서 있어도 돈이 들어온다");
                Check.Near(earned, PetWorld.CoinsPerSecond * 10, PetWorld.CoinsPerSecond,
                    "10초면 10초치가 들어온다");
                Check.Near(pet.Position, standX, 1.0, "서 있는 동안 자리를 지켰다");
            }

            using (TestWorld world = World("-p", "pikachu"))
            {
                // 일시정지 중에는 벌지 않는다.
                PetForm pet = world.Pets[0];
                PetWorld app = world.World;
                app.TogglePause();
                int before = app.Options.Coins;
                for (int i = 0; i < 250; i++) { pet.Tick(); }
                Check.Equal(app.Options.Coins, before, "멈춰 두면 벌지 않는다");
            }

            // 등급과 진화 단계에 따른 배수가 시간 벌이에도 그대로 곱해져야 한다.
            // 걸은 거리로 주던 시절의 배수가 그대로 살아 있는지 확인한다.
            CheckEarnsPerSecond("pikachu", 1.0, "일반은 그대로");
            CheckEarnsPerSecond("ditto", 3.0, "준전설은 3배");
            CheckEarnsPerSecond("mew", 5.0, "초전설은 5배");
            CheckEarnsPerSecond("wartortle", 1.5, "2단계는 1.5배");
            CheckEarnsPerSecond("blastoise", 2.25, "3단계는 2.25배");

            // 뽑기로만 만나는 등급이 열심히 키운 3단계보다는 나아야 한다.
            // 초전설은 2% 로 뽑히는데 2.5배이던 시절에는 3단계(2.25배)와
            // 거의 같았다.
            Check.That(PetWorld.PokemonIncomeMultiplier("mew")
                    > PetWorld.PokemonIncomeMultiplier("ditto"),
                "초전설이 준전설보다 많이 번다");
            Check.That(PetWorld.PokemonIncomeMultiplier("mew") > 2.25,
                "초전설이 3단계 진화체보다 많이 번다");
            // 먹이는 등급에 곱하지 않고 기본 벌이만큼을 더한다. 곱하면 귀한
            // 포켓몬일수록 먹이가 남는 장사가 되어 돈을 찍어낼 수 있다.
            using (TestWorld world = World("-p", "mew"))
            {
                PetForm pet = world.Pets[0];
                PetWorld app = world.World;
                int before = app.Options.Coins;
                for (int i = 0; i < 250; i++) { pet.Tick(); }
                int plain = app.Options.Coins - before;

                pet.Fed();
                before = app.Options.Coins;
                for (int i = 0; i < 250; i++) { pet.Tick(); }
                int fed = app.Options.Coins - before;

                Check.Near(fed - plain, PetWorld.CoinsPerSecond * 10,
                    PetWorld.CoinsPerSecond,
                    "먹이는 등급과 상관없이 10초에 " + (PetWorld.CoinsPerSecond * 10) + "원을 더 준다");
                Check.That(fed - plain < PetWorld.FoodCost,
                    "먹이 하나로 그 값만큼을 10초 만에 벌지는 못한다");
            }

            // 먹이는 걷는 속도를 바꾸지 않는다. 벌이가 시간 기준이 된 뒤로는
            // 빨리 걸어도 얻는 것이 없으면서 보기만 부산해진다.
            using (TestWorld world = World("-p", "pikachu"))
            {
                // 걸은 거리로 재면 무작위로 멈추고 돌아서는 탓에 흔들린다.
                // 걸음 속도가 설정한 최고 속도를 넘는지로 본다.
                PetForm pet = world.Pets[0];
                pet.Fed();
                double fastest = 0.0;
                for (int i = 0; i < 500; i++)
                {
                    pet.Tick();
                    fastest = System.Math.Max(fastest, pet.WalkSpeedForTest);
                }
                Check.That(fastest <= pet.TopSpeedForTest + 0.001,
                    "먹이를 먹어도 설정한 속도를 넘지 않는다 ("
                        + (int)fastest + " ≤ " + (int)pet.TopSpeedForTest + ")");
                Check.That(fastest > pet.TopSpeedForTest * 0.5,
                    "그래도 제 속도로는 걷는다");
            }

            // 준전설은 진화를 못 한다. 일반 포켓몬을 끝까지 키운 것보다는
            // 나아야 10% 를 뚫고 뽑은 값을 한다.
            Check.That(PetWorld.PokemonIncomeMultiplier("ditto") > 2.25,
                "준전설이 3단계 진화체보다 많이 번다");

            using (TestWorld world = World("-p", "pikachu"))
            {
                // 서 있는 시간이 걷는 시간보다 길어야 원본 애니메이션을 볼 수 있다.
                PetForm pet = world.Pets[0];
                int walking = 0;
                int total = 25 * 60 * 5;          // 5분
                for (int i = 0; i < total; i++)
                {
                    pet.Tick();
                    if (pet.WalkingForTest) { walking++; }
                }
                Check.That(walking * 2 < total,
                    "서 있는 시간이 걷는 시간보다 길다 (걷기 "
                        + (walking * 100 / total) + "%)");
            }
        }

        /// <summary>10초 동안 번 돈이 배수만큼인지 본다.</summary>
        private static void CheckEarnsPerSecond(string key, double multiplier, string what)
        {
            using (TestWorld world = World("-p", key))
            {
                PetForm pet = world.Pets[0];
                PetWorld app = world.World;
                Check.Near(pet.IncomeMultiplierValue, multiplier, 0.001, what + " (배수)");
                int before = app.Options.Coins;
                for (int i = 0; i < 250; i++) { pet.Tick(); }
                double want = PetWorld.CoinsPerSecond * 10 * multiplier;
                Check.Near(app.Options.Coins - before, want, PetWorld.CoinsPerSecond,
                    what + " (10초치)");
            }
        }

        // --- 가만히 있을 때 --------------------------------------------------

        private static void IdleAnimation()
        {
            Check.Section("가만히 있을 때");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetForm pet = world.Pets[0];
                Check.That(pet.IdleFrameCount > 1,
                    "원본에서 가져온 대기 장이 여럿 있다");

                pet.StandStillForTest();
                string first = pet.PoseForTest;
                Check.That(first != null && first.StartsWith("idle"),
                    "서 있으면 대기 장을 쓴다");

                // 시간이 지나면 다음 장으로 넘어간다.
                bool moved = false;
                for (int i = 0; i < 200 && !moved; i++)
                {
                    pet.Tick();
                    pet.StandStillForTest();
                    string now = pet.PoseForTest;
                    moved = now != null && now != first;
                }
                Check.That(moved, "가만히 두면 장이 넘어간다");
            }

            // 같은 그림이 이어지는 장은 하나로 합쳐 담으므로, 원본에서 한 바퀴
            // 돌던 시간을 함께 들여와야 원래 속도로 돈다. 없으면 장 수만 보고
            // 돌려 원본보다 두 배까지 빨라진다.
            foreach (PokemonSprite sprite in PokemonTaskbar.Sprites.All)
            {
                Dictionary<string, Color?[][]> poses = SpriteFactory.Poses(sprite);
                if (!poses.ContainsKey("idle0"))
                {
                    continue;
                }
                Check.That(sprite.IdleMs > 0,
                    sprite.Key + ": 대기 장이 있으면 원본 길이도 있다");
                Check.That(sprite.IdleMs >= 1000 && sprite.IdleMs <= 20000,
                    sprite.Key + ": 원본 길이가 그럴듯하다 (" + sprite.IdleMs + "ms)");
            }

            using (TestWorld world = World("-p", "pikachu"))
            {
                // 걷는 중에는 걷기 프레임이 움직임을 담고 있으므로 쓰지 않는다.
                PetForm pet = world.Pets[0];
                string pose = pet.PoseForTest;
                Check.That(pose == null || !pose.StartsWith("idle"),
                    "걷는 중에는 대기 장을 쓰지 않는다");
            }
        }

        // --- 자세 -----------------------------------------------------------

        private static void Poses()
        {
            Check.Section("자세");

            foreach (PokemonSprite sprite in PokemonTaskbar.Sprites.All)
            {
                Dictionary<string, Color?[][]> poses = SpriteFactory.Poses(sprite);
                // 눈 깜빡임은 없앴다. 도트 자료에는 blink 자세가 남아 있지만
                // 프로그램이 고르지 않으므로, 있는지 없는지 따지지 않는다.
                if (!sprite.Hops)
                {
                    Check.That(poses.ContainsKey("squash") && poses.ContainsKey("stretch"),
                        sprite.Key + ": 눌림/늘어남 자세가 있다");
                }

                Color?[][] frame = SpriteFactory.Frames(sprite)[0];
                foreach (KeyValuePair<string, Color?[][]> pose in poses)
                {
                    Check.That(pose.Value.Length == frame.Length
                            && pose.Value[0].Length == frame[0].Length,
                        sprite.Key + ": " + pose.Key + " 자세가 프레임과 같은 크기다");
                }
            }
        }

        // --- 돈과 메뉴 -------------------------------------------------------

        private static void Economy()
        {
            Check.Section("돈과 메뉴");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetWorld app = world.World;
                int price = app.NextPetPrice();
                Check.Equal(price, PetWorld.PokemonPrice,
                    "한 마리만 있을 때는 첫 영입 값이다");

                app.Options.Coins = price - 1;
                int before = world.Pets.Count;
                app.BuyRandomPet();
                Check.Equal(world.Pets.Count, before, "돈이 모자라면 영입하지 못한다");

                app.Options.Coins = price;
                app.BuyRandomPet();
                Check.Equal(world.Pets.Count, before + 1, "값을 치르면 한 마리 늘어난다");
                Check.That(app.Options.Coins < price, "값을 치른다");

                // 마리 수가 늘면 다음 한 마리가 비싸진다. 값이 고정이면 벌이는
                // 마리 수에 비례해 느는데 값은 그대로라, 살수록 빨라져 끝이 없다.
                int second = app.NextPetPrice();
                Check.Equal(second,
                    (int)System.Math.Round(PetWorld.PokemonPrice * PetWorld.PokemonPriceGrowth),
                    "두 마리째부터는 1.5배가 된다");
                Check.That(second > price, "살수록 비싸진다");

                app.Options.Coins = second - 1;
                int had = world.Pets.Count;
                app.BuyRandomPet();
                Check.Equal(world.Pets.Count, had, "오른 값에 모자라면 못 산다");
                app.Options.Coins = second;
                app.BuyRandomPet();
                Check.Equal(world.Pets.Count, had + 1, "오른 값을 치르면 늘어난다");
                Check.That(app.NextPetPrice() > second, "그 다음은 더 비싸다");

                bool evolvedLeaked = false;
                foreach (PetForm pet in world.Pets)
                {
                    if (PokemonTaskbar.Sprites.IsEvolvedOnly(pet.SpriteKey))
                    {
                        evolvedLeaked = true;
                    }
                }
                Check.That(!evolvedLeaked, "뽑기로 진화체가 나오지 않는다");
            }

            using (TestWorld world = World("-p", "pikachu", "-p", "charmander"))
            {
                PetWorld app = world.World;
                app.SetSpeed(95.0);
                Check.Equal(app.Options.Speed, 95.0, "속도를 바꾸면 설정에 남는다");

                app.TogglePause();
                Check.That(app.Paused, "잠시 멈춤이 켜진다");
                PetForm pet = world.Pets[0];
                double where = pet.Position;
                for (int i = 0; i < 40; i++)
                {
                    pet.Tick();
                }
                Check.Equal(pet.Position, where, "멈춰 있는 동안에는 움직이지 않는다");
                app.TogglePause();
                Check.That(!app.Paused, "다시 누르면 풀린다");
            }
        }

        // --- 주식창 표시 ------------------------------------------------------

        private static void StockText()
        {
            Check.Section("주식창 표시");

            // 수익률은 손익 금액 바로 옆에, 괄호 안에 적는다.
            Check.Equal(StockOverlayForm.PortfolioChangeText(330, 1.9),
                "+330원 (+1.9%)", "이익일 때");
            Check.Equal(StockOverlayForm.PortfolioChangeText(-1200, -4.25),
                "-1,200원 (-4.3%)", "손실일 때");
            Check.Equal(StockOverlayForm.PortfolioChangeText(0, 0.0),
                "0원 (0.0%)", "변동이 없을 때");
            Check.That(StockOverlayForm.PortfolioChangeText(330, 1.9).EndsWith(")"),
                "수익률이 괄호로 닫힌다");

            // 시장 이벤트 카드는 새 소식이 왔을 때만 깜빡인다. 갱신할 때마다
            // 번쩍이면 "새 소식" 이라는 뜻이 없어진다.
            Check.That(StockOverlayForm.ShouldFlashEvent("", "꼬부기워터 급등 +7.2%"),
                "새 소식이 오면 깜빡인다");
            Check.That(StockOverlayForm.ShouldFlashEvent("옛 소식", "새 소식"),
                "다른 소식으로 바뀌면 깜빡인다");
            Check.That(!StockOverlayForm.ShouldFlashEvent("같은 소식", "같은 소식"),
                "같은 소식으로는 다시 깜빡이지 않는다");
            Check.That(!StockOverlayForm.ShouldFlashEvent("옛 소식", ""),
                "소식이 없어질 때는 깜빡이지 않는다");
            Check.That(!StockOverlayForm.ShouldFlashEvent(null, null),
                "소식이 아예 없으면 깜빡이지 않는다");
        }

        // --- 주식 소식 --------------------------------------------------------

        private static void StockNews()
        {
            Check.Section("주식 소식");

            // 종목마다 소식이 여러 개 있어야 한다. 하나뿐이면 몇 분 만에 다 보고
            // 나서 "또 그 소식" 이 된다.
            bool enough = true;
            bool distinct = true;
            for (int listing = 0; listing < PetWorld.StockNames.Length; listing++)
            {
                foreach (bool positive in new bool[] { true, false })
                {
                    string[] table = PetWorld.StockNewsForTest(listing, positive);
                    if (table.Length < 3)
                    {
                        enough = false;
                    }
                    for (int a = 0; a < table.Length; a++)
                    {
                        for (int b = a + 1; b < table.Length; b++)
                        {
                            if (table[a] == table[b])
                            {
                                distinct = false;
                            }
                        }
                    }
                }
            }
            Check.That(enough, "종목마다 호재·악재가 세 개 이상 있다");
            Check.That(distinct, "한 종목 안에 같은 소식이 겹치지 않는다");

            // 업종 표
            int[] sectors = PetWorld.StockSectorsForTest;
            Check.Equal(sectors.Length, PetWorld.StockNames.Length,
                "모든 종목에 업종이 있다");
            int[] members = new int[PetWorld.SectorNamesForTest.Length];
            bool known = true;
            foreach (int sector in sectors)
            {
                if (sector < 0 || sector >= members.Length)
                {
                    known = false;
                    continue;
                }
                members[sector]++;
            }
            Check.That(known, "업종 번호가 이름표 안에 있다");
            bool twoEach = true;
            foreach (int count in members)
            {
                if (count < 2)
                {
                    twoEach = false;
                }
            }
            // 한 종목뿐인 업종이 있으면 "업종 사건" 이 개별 사건과 구별되지 않는다.
            Check.That(twoEach, "업종마다 종목이 둘 이상이다");

            // 소식표의 등락 범위: 호재는 오르고 악재는 내린다.
            bool signs = true;
            for (int sector = -1; sector < PetWorld.SectorNamesForTest.Length; sector++)
            {
                foreach (bool positive in new bool[] { true, false })
                {
                    foreach (int bound in PetWorld.BroadNewsRangeForTest(sector, positive))
                    {
                        if (positive ? bound <= 0 : bound >= 0)
                        {
                            signs = false;
                        }
                    }
                }
            }
            Check.That(signs, "호재는 오르고 악재는 내리는 폭만 담고 있다");

            TestWorld world = World("-p", "pikachu");
            PetWorld app = world.World;

            // 같은 종목·같은 방향이라도 폭이 매번 같으면 안 된다.
            bool varies = false;
            int first = app.StockEventPercentForTest(0, true);
            for (int i = 0; i < 60; i++)
            {
                if (app.StockEventPercentForTest(0, true) != first)
                {
                    varies = true;
                }
            }
            Check.That(varies, "같은 소식도 등락 폭이 매번 달라진다");

            bool up = true;
            bool down = true;
            for (int i = 0; i < 60; i++)
            {
                if (app.StockEventPercentForTest(0, true) <= 0)
                {
                    up = false;
                }
                if (app.StockEventPercentForTest(0, false) >= 0)
                {
                    down = false;
                }
            }
            Check.That(up, "호재는 언제나 오른다");
            Check.That(down, "악재는 언제나 내린다");

            // 국면이 그대로면 "전환" 이라고 알리지 않는다. 알리면 소식창이
            // 바뀌지도 않은 국면 이야기로 가득 찬다.
            bool quiet = true;
            for (int i = 0; i < 200; i++)
            {
                int before = app.MarketRegime;
                int announced = app.StockEventCount;
                app.RollMarketRegimeForTest();
                if (app.MarketRegime == before && app.StockEventCount != announced)
                {
                    quiet = false;
                }
            }
            Check.That(quiet, "국면이 그대로면 전환을 알리지 않는다");

            // 국면 알림은 한 시간에 열일곱 번쯤 나온다. 문구가 하나뿐이면 소식창에서
            // 가장 자주 보이는 줄이 된다.
            bool manyLines = true;
            bool named = true;
            for (int regime = 0; regime < PetWorld.RegimeNamesForTest.Length; regime++)
            {
                string[] lines = PetWorld.RegimeNewsForTest(regime);
                if (lines.Length < 3)
                {
                    manyLines = false;
                }
                foreach (string line in lines)
                {
                    if (line.IndexOf(PetWorld.RegimeNamesForTest[regime]) >= 0)
                    {
                        // 문구 안에 국면 이름이 또 들어가면 "상승장 · 상승장" 이 된다
                        named = false;
                    }
                }
            }
            Check.That(manyLines, "국면마다 알림 문구가 셋 이상 있다");
            Check.That(named, "알림 문구에 국면 이름이 겹쳐 들어가지 않는다");

            Check.That(PetWorld.RumourTextsForTest.Length >= 10,
                "루머 문구가 열 개 이상이다");

            world.Dispose();
        }

        // --- 업종과 소식 고르게 나오기 ---------------------------------------

        private static void EventSpread()
        {
            Check.Section("업종과 소식 고르게 나오기");

            // 업종 사건은 그 업종에 둘 이상 있어야 난다. 자리가 넷의 배수가 아니면
            // 어느 때든 잠기는 업종이 생겨, 그 업종 소식은 영영 안 나온다.
            Check.Equal(PetWorld.StockSlotCount % PetWorld.SectorNamesForTest.Length, 0,
                "자리 수가 업종 수로 나누어떨어진다");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetWorld app = world.World;
                int[] count = new int[PetWorld.SectorNamesForTest.Length];
                for (int i = 0; i < PetWorld.StockSlotCount; i++)
                {
                    count[app.StockSector(i)]++;
                }
                bool everySectorLive = true;
                for (int i = 0; i < count.Length; i++)
                {
                    if (count[i] < 2) { everySectorLive = false; }
                }
                Check.That(everySectorLive, "처음부터 네 업종이 모두 둘 이상 상장돼 있다");

                // 같은 회사가 두 줄에 나오면 안 된다.
                bool distinct = true;
                for (int a = 0; a < PetWorld.StockSlotCount; a++)
                {
                    for (int b = a + 1; b < PetWorld.StockSlotCount; b++)
                    {
                        if (app.StockName(a) == app.StockName(b)) { distinct = false; }
                    }
                }
                Check.That(distinct, "같은 이름이 두 번 상장되지 않는다");

                // 자리 하나를 비우면 그 자리는 가장 비어 있는 업종으로 채워진다.
                app.Options.StockDelisted[0] = 1;
                int[] live = new int[PetWorld.SectorNamesForTest.Length];
                for (int i = 1; i < PetWorld.StockSlotCount; i++)
                {
                    live[app.StockSector(i)]++;
                }
                int fewest = int.MaxValue;
                foreach (int n in live) { if (n < fewest) { fewest = n; } }
                int intoTheGap = 0;
                Dictionary<int, int> picks = new Dictionary<int, int>();
                for (int attempt = 0; attempt < 600; attempt++)
                {
                    int picked = app.PickRelistingForTest(0);
                    picks[picked] = 1;
                    int sector = PetWorld.StockSectorsForTest[
                        picked % PetWorld.StockSectorsForTest.Length];
                    if (live[sector] == fewest) { intoTheGap++; }
                }
                Check.That(intoTheGap > 300, "새 종목은 대개 가장 비어 있는 업종으로 들어온다");

                // 업종만 보고 채우면 조용한 업종의 명단이 굳는다. 흔들림이 작은
                // 둘이 자리를 잡으면 그 둘은 여간해서 상장폐지되지 않아, 남은
                // 하나는 영영 상장되지 못한다 — 파이리화력이 144시간 동안
                // 한 번도 안 나온 적이 있다. 가끔은 업종을 건너뛰어야 한다.
                Check.That(intoTheGap < 600, "가끔은 업종을 따지지 않고 고른다");
                bool everyBenchReachable = true;
                for (int listing = 0; listing < PetWorld.StockNames.Length; listing++)
                {
                    bool listed = false;
                    for (int slot = 1; slot < PetWorld.StockSlotCount; slot++)
                    {
                        if (app.Options.StockListingIds[slot] == listing) { listed = true; }
                    }
                    if (!listed && listing != app.Options.StockListingIds[0]
                        && !picks.ContainsKey(listing))
                    {
                        everyBenchReachable = false;
                    }
                }
                Check.That(everyBenchReachable, "쉬고 있는 종목은 모두 상장될 수 있다");
                app.Options.StockDelisted[0] = 0;
            }

            // 종목마다 소식이 넉넉해야 한다. 개별 소식이 전체 사건의 절반쯤을
            // 차지하므로, 종목당 몇 개뿐이면 같은 문구만 되풀이해서 보게 된다.
            bool enough = true;
            for (int listing = 0; listing < PetWorld.StockNames.Length; listing++)
            {
                foreach (bool positive in new bool[] { true, false })
                {
                    if (PetWorld.StockNewsForTest(listing, positive).Length < 6)
                    {
                        enough = false;
                    }
                }
            }
            Check.That(enough, "종목마다 호재·악재가 여섯 개씩 있다");
        }

        // --- 상장폐지와 재상장 -----------------------------------------------

        private static void Relisting()
        {
            Check.Section("상장폐지와 재상장");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetWorld app = world.World;
                Check.Equal(PetWorld.StockRelistSeconds, 10 * 60,
                    "재상장까지 10분이다");

                // 값을 폐지선 밑으로 밀어 놓고 갱신하면 상장폐지된다.
                app.Options.StockPrices[0] = 1;
                app.OpenMarketForTest();
                app.UpdateMarket();
                Check.That(app.IsStockDelisted(0), "값이 무너지면 상장폐지된다");
                Check.Equal(app.Options.StockRelistSeconds[0], PetWorld.StockRelistSeconds,
                    "재상장 시계가 10분으로 걸린다");
                Check.Equal(app.RelistingMinutes(0), 10, "남은 시간을 10분으로 보여 준다");
                Check.Equal(app.Options.StockShares[0], 0, "보유 주식이 사라진다");

                // 시간이 다 가면 새 종목이 들어온다.
                app.Options.StockRelistSeconds[0] = 5;
                app.OpenMarketForTest();
                app.UpdateMarket();
                Check.That(!app.IsStockDelisted(0), "시간이 지나면 새 종목이 들어온다");
                Check.That(app.Options.StockPrices[0] > 0, "새 종목에 값이 붙는다");
            }
        }

        // --- 공매도 ------------------------------------------------------------

        private static void ShortSelling()
        {
            Check.Section("공매도");

            // 손익은 진입가를 기준으로 대칭이다. 값이 내린 만큼 벌고 오른 만큼 잃는다.
            Check.Equal(PetWorld.ShortPayout(1000, 1000), 980,
                "값이 그대로면 담보에서 수수료만 빠진다");
            Check.That(PetWorld.ShortPayout(1000, 800) > PetWorld.ShortPayout(1000, 1000),
                "값이 내리면 더 받는다");
            Check.That(PetWorld.ShortPayout(1000, 1200) < PetWorld.ShortPayout(1000, 1000),
                "값이 오르면 덜 받는다");

            // 손실 상한. 진입가의 두 배가 되면 담보를 다 잃고, 그 위로는 더 잃지 않는다.
            Check.Equal(PetWorld.ShortPayout(1000, 2000), 0,
                "두 배가 되면 담보를 다 잃는다");
            Check.Equal(PetWorld.ShortPayout(1000, 5000), 0,
                "그 위로 아무리 올라도 빚이 되지는 않는다");
            Check.Equal(PetWorld.ShortPayout(0, 1000), 0, "공매도가 없으면 받을 것도 없다");

            using (TestWorld world = World("-p", "pikachu"))
            {
                PetWorld app = world.World;
                app.OpenMarketForTest();
                app.Options.Coins = 100000;
                app.Options.StockPrices[0] = 1000;

                // 공매도하면 담보와 수수료가 현금에서 빠진다.
                int before = app.Options.Coins;
                app.ShortStock(0, 5);
                Check.Equal(app.Options.StockShorts[0], 5, "공매도 수량이 잡힌다");
                Check.Equal(app.Options.StockShortPrices[0], 1000, "그때의 값이 진입가가 된다");
                Check.Equal(before - app.Options.Coins, app.StockShortCost(0) * 5,
                    "담보와 수수료만큼 현금이 빠진다");
                Check.Equal(app.StockShortWipePrice(0), 2000, "강제 청산가는 진입가의 두 배다");

                // 값이 내리면 청산해서 번다.
                app.Options.StockPrices[0] = 700;
                Check.That(app.StockShortProfit(0) > 0, "값이 내리면 평가 손익이 이익이 된다");
                int payout = app.StockCoverProceeds(0);
                int cash = app.Options.Coins;
                app.CoverStock(0, 5);
                Check.Equal(app.Options.StockShorts[0], 0, "청산하면 공매도가 사라진다");
                Check.Equal(app.Options.StockShortPrices[0], 0, "진입가도 지워진다");
                Check.Equal(app.Options.Coins - cash, payout * 5, "청산한 만큼 현금이 들어온다");

                // 진입가의 두 배가 되면 담보를 잃고 저절로 정리된다.
                app.Options.StockPrices[0] = 1000;
                app.ShortStock(0, 2);
                app.Options.StockPrices[0] = 2500;
                cash = app.Options.Coins;
                app.TickShortMarginForTest();
                Check.Equal(app.Options.StockShorts[0], 0, "두 배가 되면 강제 청산된다");
                Check.Equal(app.Options.Coins, cash, "강제 청산에서는 한 푼도 못 돌려받는다");

                // 휴장 중에는 열지도 닫지도 못한다.
                app.CloseMarketForTest();
                app.Options.StockPrices[0] = 1000;
                app.ShortStock(0, 1);
                Check.Equal(app.Options.StockShorts[0], 0, "휴장 중에는 공매도할 수 없다");

                // 담보보다 현금이 적으면 열리지 않는다.
                app.OpenMarketForTest();
                app.Options.Coins = 100;
                app.ShortStock(0, 1);
                Check.Equal(app.Options.StockShorts[0], 0, "현금이 모자라면 공매도할 수 없다");
            }

            // 상장폐지는 공매도의 최대 이익이다. 종목이 사라지기 전에 정산해 준다.
            using (TestWorld world = World("-p", "pikachu"))
            {
                PetWorld app = world.World;
                app.OpenMarketForTest();
                app.Options.Coins = 100000;
                app.Options.StockPrices[0] = 1000;
                app.ShortStock(0, 3);
                int cash = app.Options.Coins;
                app.Options.StockPrices[0] = 1;
                app.OpenMarketForTest();
                app.UpdateMarket();
                Check.That(app.IsStockDelisted(0), "값이 무너지면 상장폐지된다");
                Check.Equal(app.Options.StockShorts[0], 0, "상장폐지되면 공매도도 정리된다");
                Check.That(app.Options.Coins > cash + 3 * 1000,
                    "상장폐지된 값으로 정산해 담보보다 많이 돌려받는다");
            }

            // 오래 굴려도 값이 깨지지 않아야 한다. 상장폐지·강제 청산·거래정지가
            // 겹치는 자리가 많아, 눈으로 짚은 몇 가지만으로는 안심할 수 없다.
            bool broken = false;
            for (int seed = 0; seed < 5 && !broken; seed++)
            {
                using (TestWorld world = World("-p", "pikachu"))
                {
                    PetWorld app = world.World;
                    app.Options.Coins = 5000000;
                    for (int i = 0; i < PetWorld.StockSlotCount; i++)
                    {
                        app.Options.StockBasePrices[i] = app.Options.StockPrices[i];
                    }
                    for (int tick = 0; tick < 300 && !broken; tick++)
                    {
                        app.OpenMarketForTest();
                        for (int i = 0; i < PetWorld.StockSlotCount; i++)
                        {
                            if ((tick + i + seed) % 7 == 0) app.ShortStock(i, 1 + tick % 3);
                            if ((tick + i + seed) % 11 == 0) app.CoverStock(i, 1);
                        }
                        app.UpdateMarket();
                        if (app.Options.Coins < 0) broken = true;
                        for (int i = 0; i < PetWorld.StockSlotCount; i++)
                        {
                            int shorts = app.Options.StockShorts[i];
                            int entry = app.Options.StockShortPrices[i];
                            // 수량과 진입가는 늘 함께 있거나 함께 없다.
                            if (shorts < 0 || (shorts > 0) != (entry > 0)) broken = true;
                            // 상장폐지된 자리에 공매도가 남으면 담보가 갇힌다.
                            if (app.IsStockDelisted(i) && shorts > 0) broken = true;
                            // 담보가 녹았으면 그 자리에서 정리돼 있어야 한다.
                            if (shorts > 0 && app.Options.StockPrices[i] >= app.StockShortWipePrice(i))
                                broken = true;
                            // 아무리 잘돼도 담보의 두 배를 넘겨 받지는 못한다.
                            if (entry > 0 && PetWorld.ShortPayout(entry, app.Options.StockPrices[i])
                                > entry * 2) broken = true;
                        }
                    }
                }
            }
            Check.That(!broken, "장을 오래 굴려도 공매도 값이 깨지지 않는다");

            // 목록 한 줄에 보유와 공매도가 함께 나온다.
            using (TestWorld world = World("-p", "pikachu"))
            {
                PetWorld app = world.World;
                app.Options.StockShares[0] = 2;
                app.Options.StockShorts[0] = 3;
                Check.Equal(StockOverlayForm.HoldingRowText(app, 0, false),
                    "보유 2주 · 공매도 3주", "둘 다 있으면 둘 다 적는다");
                app.Options.StockShares[0] = 0;
                Check.Equal(StockOverlayForm.HoldingRowText(app, 0, false),
                    "공매도 3주", "공매도만 있으면 그것만 적는다");
                Check.Equal(StockOverlayForm.HoldingRowText(app, 0, true),
                    "위험 · 공매도 3주", "위기 종목이면 앞에 표시가 붙는다");
                app.Options.StockShorts[0] = 0;
                Check.That(app.HasStockPosition(0) == false,
                    "아무것도 없으면 보유 종목으로 세지 않는다");
            }
        }

        // --- 주식 특별 사건 ---------------------------------------------------

        private static void StockSpecialEvents()
        {
            Check.Section("주식 특별 사건");

            TestWorld world = World("-p", "pikachu");
            PetWorld app = world.World;

            // 폐지선·위기선은 금액이 아니라 기준가의 비율이다. 종목마다 뜻이
            // 같아야 하고, 위기선은 폐지선보다 위에 있어야 한다.
            Check.That(PetWorld.StockCrisisRatio > PetWorld.StockDelistRatio,
                "위기선이 상장폐지선보다 위에 있다");
            bool sameMeaning = true;
            for (int i = 0; i < PetWorld.StockSlotCount; i++)
            {
                if (app.StockDelistPrice(i) * 1000 / app.StockBasePrice(i)
                    != app.StockDelistPrice(0) * 1000 / app.StockBasePrice(0))
                {
                    sameMeaning = false;
                }
            }
            Check.That(sameMeaning, "모든 종목이 같은 낙폭에서 상장폐지된다");

            // 투자경고: 값은 그대로, 흔들림만 커진다.
            int plain = app.StockVolatilityForTest(1);
            int price = app.Options.StockPrices[1];
            app.AlertStock(1, 2);
            Check.That(app.IsStockAlerted(1), "투자경고 종목이 된다");
            Check.Equal(app.Options.StockPrices[1], price, "투자경고는 값을 건드리지 않는다");
            Check.That(app.StockVolatilityForTest(1) > plain, "투자경고 동안 더 흔들린다");

            // 2분이면 시장 갱신 열두 번이다. 그 뒤에는 풀려 있어야 한다.
            for (int i = 0; i < 12; i++)
            {
                app.TickStockAlertsForTest();
            }
            Check.That(!app.IsStockAlerted(1), "시간이 지나면 투자경고가 풀린다");
            Check.Equal(app.StockVolatilityForTest(1), plain, "풀리면 흔들림도 돌아온다");

            // 상장폐지 위험. 보유 주식이 전부 사라지는 일이라 예고가 있어야 한다.
            app.Options.StockBasePrices[2] = 2000;
            app.Options.StockPrices[2] = 2000;
            Check.That(!app.IsStockInCrisis(2), "제 값에서는 위험이 아니다");
            Check.Equal(app.StockCrisisPrice(2), 880, "위기선은 기준가의 44%다");
            Check.Equal(app.StockDelistPrice(2), 320, "폐지선은 기준가의 16%다");

            app.Options.StockPrices[2] = 879;
            Check.That(app.IsStockInCrisis(2), "위기선 밑이면 위험이다");
            Check.That(app.StockCrisisText(2).IndexOf("320원") >= 0,
                "안내가 폐지선을 알려 준다");
            Check.That(app.StockCrisisText(2).IndexOf("사라집니다") >= 0,
                "안내가 보유 주식이 없어진다고 알려 준다");
            // 카드의 문구 자리는 462px 한 줄이다. 넘치면 뒤가 잘려 정작 보유 주식이
            // 사라진다는 말이 안 보인다 — 실제로 그렇게 잘려 있었다.
            Check.That(TextRenderer.MeasureText(app.StockCrisisText(2),
                    new Font("Arial", 10.0f)).Width <= 462,
                "안내가 카드 한 줄에 들어간다");

            // 위기 구간에서는 오르는 쪽 폭만 좁아진다. 아래쪽은 그대로여야
            // "내릴 확률이 높아진다" 가 된다.
            Check.That(PetWorld.StockCrisisUpsideRatio < 1.0,
                "위기 구간에서는 오르는 폭이 줄어든다");
            Check.That(PetWorld.StockCrisisUpsideRatio > 0.0,
                "그래도 오를 수는 있다");

            app.Options.StockPrices[2] = 1200;
            Check.That(!app.IsStockInCrisis(2), "값이 올라오면 위험이 풀린다");

            // 폐지된 종목은 "위험" 이 아니라 이미 끝난 것이다.
            app.Options.StockPrices[2] = 100;
            app.Options.StockDelisted[2] = 1;
            Check.That(!app.IsStockInCrisis(2), "이미 폐지된 종목은 위험이 아니다");

            world.Dispose();
        }

        // --- 되돌릴 수 없는 동작 ---------------------------------------------

        private static void DangerousActions()
        {
            Check.Section("되돌릴 수 없는 동작");

            TestWorld world = World("-p", "pikachu", "-p", "squirtle");
            PetWorld app = world.World;
            PetForm[] pets = app.PetsSnapshot();

            // 포켓몬 한 마리는 396,000원이다. 묻지 않고 지우면 안 된다.
            string text = app.ReleaseConfirmText(pets[0]);
            Check.That(text.IndexOf("피카츄") >= 0, "누구를 보내는지 이름을 댄다");
            Check.That(text.IndexOf("정말") >= 0, "정말 보낼지 묻는다");
            Check.That(text.IndexOf("종료") < 0,
                "두 마리 있을 때는 종료 이야기를 하지 않는다");

            // 마지막 한 마리를 보내면 앱이 통째로 닫힌다. 그것까지 말해야 한다.
            app.Remove(pets[1]);
            Check.Equal(world.Pets.Count, 1, "한 마리가 남았다");
            string last = app.ReleaseConfirmText(world.Pets[0]);
            Check.That(last.IndexOf("마지막") >= 0, "마지막 한 마리라고 알린다");
            Check.That(last.IndexOf("종료") >= 0, "함께 종료된다고 알린다");

            world.Dispose();
        }

        // --- 뒷정리 -----------------------------------------------------------

        private static void Lifecycle()
        {
            Check.Section("뒷정리");

            TestWorld world = World("-p", "pikachu", "-p", "ditto");
            PetWorld app = world.World;
            Check.Equal(world.Pets.Count, 2, "두 마리로 시작한다");
            app.QuitAll();
            Check.Equal(world.Pets.Count, 0, "끝내면 한 마리도 남지 않는다");
            app.QuitAll();
            Check.That(true, "두 번 끝내도 터지지 않는다");
            world.Dispose();
        }

        // --- 진화 ---------------------------------------------------------

        private static void Evolution()
        {
            Check.Section("진화");

            using (TestWorld world = World("-p", "squirtle"))
            {
                PetForm pet = world.Pets[0];
                Check.Equal(pet.NextKey, "wartortle", "꼬부기는 어니부기가 된다");
                Check.That(!pet.CanEvolve(), "처음에는 진화할 수 없다");

                // 먹이만으로는 부족하다.
                while (pet.FriendshipValue < pet.FriendshipNeed)
                {
                    pet.Fed();
                }
                Check.That(!pet.CanEvolve(), "먹이만으로는 진화하지 않는다");

                // 걷기까지 채워도 성장의 물방울이 없으면 안 된다.
                pet.SetWalked(pet.WalkNeedForTest);
                world.World.Options.GrowthDrops = 0;
                Check.That(!pet.CanEvolve(), "성장의 물방울이 없으면 진화하지 않는다");

                world.World.Options.GrowthDrops = 1;
                Check.That(pet.CanEvolve(), "먹이·걷기·물방울이 모두 차면 진화할 수 있다");
                Check.That(!pet.IsEvolving, "조건이 차도 스스로 진화하지는 않는다");

                pet.StartEvolving();
                Check.That(pet.IsEvolving, "직접 고르면 진화가 시작된다");
                Check.Equal(world.World.Options.GrowthDrops, 0, "물방울을 하나 쓴다");

                double where = pet.Position;
                for (int i = 0; i < 400 && world.Pets[0].SpriteKey != "wartortle"; i++)
                {
                    world.Pets[0].Tick();
                }
                Check.Equal(world.Pets.Count, 1, "진화한 뒤에도 한 마리다");
                PetForm grown = world.Pets[0];
                Check.Equal(grown.SpriteKey, "wartortle", "어니부기가 됐다");
                Check.Equal(grown.NextKey, "blastoise", "다음은 거북왕이다");
                Check.Near(grown.Position, where, 2.0, "있던 자리를 지킨다");
            }

            // 시간이 흘렀다고 저절로 진화하지는 않는다.
            using (TestWorld world = World("-p", "squirtle"))
            {
                PetForm pet = world.Pets[0];
                for (int i = 0; i < 750; i++)      // 30초
                {
                    pet.Tick();
                }
                Check.That(!pet.IsEvolving, "가만히 두면 진화하지 않는다");
            }
        }
    }
}
