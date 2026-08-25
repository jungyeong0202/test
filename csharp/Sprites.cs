// 이 파일은 자동 생성됩니다. 직접 고치지 말고 sprites.py 를 고친 뒤
//     python tools/gen_sprites_cs.py
// 를 실행하세요.
using System.Collections.Generic;

namespace PokemonTaskbar
{
    public class PokemonSprite
    {
        public string Key;
        public string NameKo;
        public double ScaleFactor;
        public Dictionary<char, string> Palette;
        public string[][] Frames;   // 걷기 프레임마다 도트 줄 묶음
    }

    public static class Sprites
    {
        public static readonly List<PokemonSprite> All = new List<PokemonSprite>
        {
            new PokemonSprite
            {
                Key = "pikachu",
                NameKo = "피카츄",
                ScaleFactor = 0.3333333333333333,
                Palette = new Dictionary<char, string>
                {
                    { 'K', "#feba11" },
                    { 'Y', "#feed43" },
                    { 'O', "#000000" },
                    { 'W', "#542100" },
                    { 'R', "#984300" },
                    { 'B', "#dd9800" },
                    { 'G', "#fefeaa" },
                    { 'T', "#101010" },
                    { 'D', "#444355" },
                    { 'E', "#fdee42" },
                    { 'C', "#cb2110" },
                    { 'S', "#ed5443" },
                    { 'L', "#552000" },
                    { 'M', "#fdfefd" },
                },
                Frames = new string[][]
                {
                    new string[]
                    {
                        ".....OO",
                        "....OTO",
                        "....TTO",
                        "...OTTO",
                        "...OTTO",
                        "...WKBW",
                        "...WKKW..............OOOOOO",
                        "..WBKKW..........WWOOTDDDTO",
                        "..WKKKW........WWKYBTTDTTO",
                        "..WKKRRRRRRWWWOKYYYYBTTTO",
                        "..WREGGGGYYKBYYYYYYYKBOO",
                        "..RYGGGGGGYYYYYYYYYKWO..........RRRR",
                        "..KEGGGGGYYYYYYYKWOO.........RRRKYYW",
                        ".RYYGGGYYYYYYYYYWO........RRRKYYYYYW",
                        ".ODKYYYYYKDOKYYYW......RRRKYYYYYYYEW",
                        "ROMRYYYYYRMORYYKW.....RRYYYYYYYYYYYKW",
                        "RODREYYYYRDORYYKKW....RBKKKKYYYYYYYYW",
                        "RDOEYYYYYKODKYYKKW....RKKKKKKKKKYYYYW",
                        "RCYYRKYYYYYYYSSKKW....RKKKKKKKKKKKKKW",
                        "WCYBBYYYBYYYSSSCKW....WKKKKKKKKBOOOOO",
                        "WCKBKRRBYYYYSSCCKWO...WBKKKBWWWO",
                        ".WRKKKKKKKKKKCCKKWO....LKBWW",
                        "..WRBBKKKKKKKKKKKKO....WBKO",
                        "...LKKKKKKKKKKKKKKO.....OKKWW",
                        "...RKKKKKKKKKKKKYYRO.....OKKW",
                        "..WKBKKKKKKBKKWEYEWO....LBKO",
                        "..OKRKKKKKKRKKOEYYWO...WBBBOO",
                        "..OYYRYYYERYYOYYYYKRO...OBBBO",
                        "..OKYYRYYRYYKWYYYYYWOOOORRWO",
                        "..WLYYWYYWEYWYYYYYYWOWLLWOO",
                        ".RKYWWYYYYWWYYYYYYKWROWOO",
                        ".RKKYYYYYYYYYYYKKKKRBOO",
                        ".WKKKKYYYYYYKKKKKKKKKO",
                        ".WBKKKKKKKKKKKKKKKKKKO",
                        "..WBKKKKKKKKKKKKKKKKBO",
                        "..ORBBRWWWWWBBKKKKKBO",
                        ".OKKBOO.....WWWWRBRL",
                        ".OOOOO.........OKRKBO",
                        "................OOOOO",
                    },
                    new string[]
                    {
                        ".....OO",
                        "....OTO",
                        "....TTO",
                        "...OTTO",
                        "...OTTO",
                        "...WKBW",
                        "...WKKW..............OOOOOO",
                        "..WBKKW..........WWOOTDDDTO",
                        "..WKKKW........WWKYBTTDTTO",
                        "..WKKRRRRRRWWWOKYYYYBTTTO",
                        "..WREGGGGYYKBYYYYYYYKBOO",
                        "..RYGGGGGGYYYYYYYYYKWO..........RRRR",
                        "..KEGGGGGYYYYYYYKWOO.........RRRKYYW",
                        ".RYYGGGYYYYYYYYYWO........RRRKYYYYYW",
                        ".ODKYYYYYKDOKYYYW......RRRKYYYYYYYEW",
                        "ROMRYYYYYRMORYYKW.....RRYYYYYYYYYYYKW",
                        "RODREYYYYRDORYYKKW....RBKKKKYYYYYYYYW",
                        "RDOEYYYYYKODKYYKKW....RKKKKKKKKKYYYYW",
                        "RCYYRKYYYYYYYSSKKW....RKKKKKKKKKKKKKW",
                        "WCYBBYYYBYYYSSSCKW....WKKKKKKKKBOOOOO",
                        "WCKBKRRBYYYYSSCCKWO...WBKKKBWWWO",
                        ".WRKKKKKKKKKKCCKKWO....LKBWW",
                        "..WRBBKKKKKKKKKKKKO....WBKO",
                        "...LKKKKKKKKKKKKKKO.....OKKWW",
                        "...RKKKKKKKKKKKKYYRO.....OKKW",
                        "..WKBKKKKKKBKKWEYEWO....LBKO",
                        "..OKRKKKKKKRKKOEYYWO...WBBBOO",
                        "..OYYRYYYERYYOYYYYKRO...OBBBO",
                        "..OKYYRYYRYYKWYYYYYWOOOORRWO",
                        "..WLYYWYYWEYWYYYYYYWOWLLWOO",
                        ".RKYWWYYYYWWYYYYYYKWROWOO",
                        ".RKKYYYYYYYYYYYKKKKRBOO",
                        ".WKKKKYYYYYYKKKKKKKKKO",
                        ".WBKORBBRKKKKKKKKKKKKO",
                        "..WOKKBOOKKKKKKKKKKKBO",
                        "...OOOOOWWWWBBKWWRBRL",
                        "............WW..OKRKBO",
                        ".................OOOOO",
                        ".",
                    },
                    new string[]
                    {
                        ".....OO",
                        "....OTO",
                        "....TTO",
                        "...OTTO",
                        "...OTTO",
                        "...WKBW",
                        "...WKKW..............OOOOOO",
                        "..WBKKW..........WWOOTDDDTO",
                        "..WKKKW........WWKYBTTDTTO",
                        "..WKKRRRRRRWWWOKYYYYBTTTO",
                        "..WREGGGGYYKBYYYYYYYKBOO",
                        "..RYGGGGGGYYYYYYYYYKWO..........RRRR",
                        "..KEGGGGGYYYYYYYKWOO.........RRRKYYW",
                        ".RYYGGGYYYYYYYYYWO........RRRKYYYYYW",
                        ".ODKYYYYYKDOKYYYW......RRRKYYYYYYYEW",
                        "ROMRYYYYYRMORYYKW.....RRYYYYYYYYYYYKW",
                        "RODREYYYYRDORYYKKW....RBKKKKYYYYYYYYW",
                        "RDOEYYYYYKODKYYKKW....RKKKKKKKKKYYYYW",
                        "RCYYRKYYYYYYYSSKKW....RKKKKKKKKKKKKKW",
                        "WCYBBYYYBYYYSSSCKW....WKKKKKKKKBOOOOO",
                        "WCKBKRRBYYYYSSCCKWO...WBKKKBWWWO",
                        ".WRKKKKKKKKKKCCKKWO....LKBWW",
                        "..WRBBKKKKKKKKKKKKO....WBKO",
                        "...LKKKKKKKKKKKKKKO.....OKKWW",
                        "...RKKKKKKKKKKKKYYRO.....OKKW",
                        "..WKBKKKKKKBKKWEYEWO....LBKO",
                        "..OKRKKKKKKRKKOEYYWO...WBBBOO",
                        "..OYYRYYYERYYOYYYYKRO...OBBBO",
                        "..OKYYRYYRYYKWYYYYYWOOOORRWO",
                        "..WLYYWYYWEYWYYYYYYWOWLLWOO",
                        ".RKYWWYYYYWWYYYYYYKWROWOO",
                        ".RKKYYYYYYYYYYYKKKKRBOO",
                        ".WKKKKYYYYYYKKKKKKKKKO",
                        ".WBKKKKKKKKKKKKKKKKKKO",
                        "..WBKKKKKKKKKKKKKKKKBO",
                        "..ORBBRWWWWWBBKKKKKBO",
                        ".OKKBOO.....WWWWRBRL",
                        ".OOOOO.........OKRKBO",
                        "................OOOOO",
                    },
                    new string[]
                    {
                        ".....OO",
                        "....OTO",
                        "....TTO",
                        "...OTTO",
                        "...OTTO",
                        "...WKBW",
                        "...WKKW..............OOOOOO",
                        "..WBKKW..........WWOOTDDDTO",
                        "..WKKKW........WWKYBTTDTTO",
                        "..WKKRRRRRRWWWOKYYYYBTTTO",
                        "..WREGGGGYYKBYYYYYYYKBOO",
                        "..RYGGGGGGYYYYYYYYYKWO..........RRRR",
                        "..KEGGGGGYYYYYYYKWOO.........RRRKYYW",
                        ".RYYGGGYYYYYYYYYWO........RRRKYYYYYW",
                        ".ODKYYYYYKDOKYYYW......RRRKYYYYYYYEW",
                        "ROMRYYYYYRMORYYKW.....RRYYYYYYYYYYYKW",
                        "RODREYYYYRDORYYKKW....RBKKKKYYYYYYYYW",
                        "RDOEYYYYYKODKYYKKW....RKKKKKKKKKYYYYW",
                        "RCYYRKYYYYYYYSSKKW....RKKKKKKKKKKKKKW",
                        "WCYBBYYYBYYYSSSCKW....WKKKKKKKKBOOOOO",
                        "WCKBKRRBYYYYSSCCKWO...WBKKKBWWWO",
                        ".WRKKKKKKKKKKCCKKWO....LKBWW",
                        "..WRBBKKKKKKKKKKKKO....WBKO",
                        "...LKKKKKKKKKKKKKKO.....OKKWW",
                        "...RKKKKKKKKKKKKYYRO.....OKKW",
                        "..WKBKKKKKKBKKWEYEWO....LBKO",
                        "..OKRKKKKKKRKKOEYYWO...WBBBOO",
                        "..OYYRYYYERYYOYYYYKRO...OBBBO",
                        "..OKYYRYYRYYKWYYYYYWOOOORRWO",
                        "..WLYYWYYWEYWYYYYYYWOWLLWOO",
                        ".RKYWWYYYYWWYYYYYYKWROWOO",
                        ".RKKYYYYYYYYYYYKKKKRBOO",
                        ".WKKKKYYYYYYKKKKKKKKKO",
                        ".WBKKKKKKKKKKKKKKKKKKO",
                        "..WORBBRKKKKKKKKWWRBRL",
                        "..OKKBOOWWWWBBKKKOKRKBO",
                        "..OOOOO.....WW....OOOOO",
                        ".",
                        ".",
                    },
                },
            },
            new PokemonSprite
            {
                Key = "charmander",
                NameKo = "파이리",
                ScaleFactor = 1.0,
                Palette = new Dictionary<char, string>
                {
                    { 'K', "#2b2b2b" },
                    { 'O', "#f0803c" },
                    { 'S', "#c85a1e" },
                    { 'C', "#f8dcae" },
                    { 'W', "#ffffff" },
                    { 'R', "#e8482c" },
                    { 'Y', "#ffd24a" },
                },
                Frames = new string[][]
                {
                    new string[]
                    {
                        ".......KKKKKK",
                        "......KOOOOOOK",
                        ".....KOOOOOOOOK",
                        ".....KOKWKOOKWKOK",
                        ".....KOKKKOOKKKOK",
                        ".....KOOOOOOOOOOK...KRK",
                        "......KOOSSSSOOK..KRYRK",
                        "......KKOOOOOOKK..KRYYRK",
                        ".....KOOOOOOOOOOK.KRRYRK",
                        "....KOOCCCCCCCCOK..KRRK",
                        "....KOCCCCCCCCCOKKKSOK",
                        "....KOCCCCCCCCCOKKSOK",
                        "....KOCCCCCCCCCOK",
                        ".....KOCCCCCCCOK",
                        ".....KOOKKKKOOK",
                        "....KKKK..KKKK",
                    },
                    new string[]
                    {
                        ".......KKKKKK",
                        "......KOOOOOOK",
                        ".....KOOOOOOOOK",
                        ".....KOKWKOOKWKOK",
                        ".....KOKKKOOKKKOK",
                        ".....KOOOOOOOOOOK...KRK",
                        "......KOOSSSSOOK..KRYRK",
                        "......KKOOOOOOKK..KRYYRK",
                        ".....KOOOOOOOOOOK.KRRYRK",
                        "....KOOCCCCCCCCOK..KRRK",
                        "....KOCCCCCCCCCOKKKSOK",
                        "....KOCCCCCCCCCOKKSOK",
                        "....KOCCCCCCCCCOK",
                        ".....KOCCCCCCCOK",
                        "....KOOKKKKOOK",
                        "...KKKK....KKKK",
                    },
                },
            },
            new PokemonSprite
            {
                Key = "bulbasaur",
                NameKo = "이상해씨",
                ScaleFactor = 1.0,
                Palette = new Dictionary<char, string>
                {
                    { 'K', "#2b2b2b" },
                    { 'T', "#7ec8a4" },
                    { 'D', "#4e9e7c" },
                    { 'G', "#6dbe45" },
                    { 'E', "#4a9130" },
                    { 'W', "#ffffff" },
                },
                Frames = new string[][]
                {
                    new string[]
                    {
                        ".......KKGGGGKK",
                        "......KGGEGGEGGK",
                        ".....KGGGGEGGGGGK",
                        ".....KGGEGGGGEGGK",
                        "....KKKGGGGGGGKKK",
                        "...KTTTKKKKKKKTTTK",
                        "..KTTDTTTTTTTTTDTTK",
                        "..KTTTTTTTTTTTTTTTK",
                        "..KTKWKTTTTTTKWKTTK",
                        "..KTKKKTTTTTTKKKTTK",
                        "..KTTTTTTKKTTTTTTTK",
                        "..KTTDTTTTTTTTTDTTK",
                        "...KTTTTTTTTTTTTTK",
                        "...KTTTTTTTTTTTTTK",
                        "...KKTTKKKKKKTTKK",
                        "....KKKK....KKKK",
                    },
                    new string[]
                    {
                        ".......KKGGGGKK",
                        "......KGGEGGEGGK",
                        ".....KGGGGEGGGGGK",
                        ".....KGGEGGGGEGGK",
                        "....KKKGGGGGGGKKK",
                        "...KTTTKKKKKKKTTTK",
                        "..KTTDTTTTTTTTTDTTK",
                        "..KTTTTTTTTTTTTTTTK",
                        "..KTKWKTTTTTTKWKTTK",
                        "..KTKKKTTTTTTKKKTTK",
                        "..KTTTTTTKKTTTTTTTK",
                        "..KTTDTTTTTTTTTDTTK",
                        "...KTTTTTTTTTTTTTK",
                        "...KTTTTTTTTTTTTTK",
                        "..KKTTKKKKKKTTKK",
                        "..KKKK......KKKK",
                    },
                },
            },
            new PokemonSprite
            {
                Key = "squirtle",
                NameKo = "꼬부기",
                ScaleFactor = 1.0,
                Palette = new Dictionary<char, string>
                {
                    { 'K', "#2b2b2b" },
                    { 'B', "#78c8f0" },
                    { 'D', "#3878a8" },
                    { 'S', "#c87838" },
                    { 'L', "#f0c078" },
                    { 'C', "#f8e8c0" },
                    { 'W', "#ffffff" },
                },
                Frames = new string[][]
                {
                    new string[]
                    {
                        "......KKKKKK",
                        ".....KBBBBBBK",
                        "....KBBBBBBBBK",
                        "....KBKWKBBKWKBK",
                        "....KBKKKBBKKKBK",
                        "....KBBBBBBBBBBK",
                        ".....KBBDDDDBBK",
                        "....KKKKKKKKKKKK",
                        "...KSSLLSSSSLLSSK",
                        "..KSLLSSSSSSSSLLSK",
                        "..KSSSSCCCCCCSSSSK",
                        "..KSLLSCCCCCCSLLSK",
                        "..KSSSSCCCCCCSSSSK",
                        "...KSSSSSSSSSSSSK",
                        "....KKBBKKKKBBKK",
                        ".....KKKK..KKKK",
                    },
                    new string[]
                    {
                        "......KKKKKK",
                        ".....KBBBBBBK",
                        "....KBBBBBBBBK",
                        "....KBKWKBBKWKBK",
                        "....KBKKKBBKKKBK",
                        "....KBBBBBBBBBBK",
                        ".....KBBDDDDBBK",
                        "....KKKKKKKKKKKK",
                        "...KSSLLSSSSLLSSK",
                        "..KSLLSSSSSSSSLLSK",
                        "..KSSSSCCCCCCSSSSK",
                        "..KSLLSCCCCCCSLLSK",
                        "..KSSSSCCCCCCSSSSK",
                        "...KSSSSSSSSSSSSK",
                        "...KKBBKKKKBBKK",
                        "...KKKK....KKKK",
                    },
                },
            },
        };

        public static PokemonSprite Find(string key)
        {
            foreach (PokemonSprite sprite in All)
            {
                if (sprite.Key == key)
                {
                    return sprite;
                }
            }
            return null;
        }
    }
}
