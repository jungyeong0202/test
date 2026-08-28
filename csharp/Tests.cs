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
                Movement();
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
            Check.Equal(PokemonTaskbar.Sprites.Find("pikachu").EvolvesTo, null,
                "피카츄는 진화하지 않는다");
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
                Check.Equal(grown.NextKey, null, "더 진화하지 않는다");
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
