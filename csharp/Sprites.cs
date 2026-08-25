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
        public Dictionary<char, string> Palette;
        public string[] Rows;
        public Dictionary<int, string> StepRows;
    }

    public static class Sprites
    {
        public static readonly List<PokemonSprite> All = new List<PokemonSprite>
        {
            new PokemonSprite
            {
                Key = "pikachu",
                NameKo = "피카츄",
                Palette = new Dictionary<char, string>
                {
                    { 'K', "#2b2b2b" },
                    { 'Y', "#f8d030" },
                    { 'B', "#8a5a00" },
                    { 'R', "#e8646a" },
                    { 'W', "#ffffff" },
                },
                Rows = new string[]
                {
                    "......KK...KK",
                    ".....KKBK.KBKK",
                    ".....KBBK.KBBK",
                    "....KKYBKKKBYKK",
                    "...KKYYYKKKYYYKK......KK",
                    "..KKYYYYYYYYYYYK.....KYK",
                    "..KYYYYYYYYYYYYK....KYYK",
                    "..KYKWKYYYYKWKYK....KYKK",
                    "..KYKKKYYYYKKKYK...KYYK",
                    "..KYYYYYKKYYYYYK...KYK",
                    "..KRRYYYYYYYYRRK..KYYK",
                    "..KRRYYYYYYYYRRKKKYYK",
                    "...KYYYYYYYYYYKKKYYK",
                    "...KYYYYYYYYYYK.KKK",
                    "...KYYYYYYYYYYK",
                    "...KYYYYYYYYYYK",
                    "...KKYYKKKKYYKK",
                    "....KKKK..KKKK",
                },
                StepRows = new Dictionary<int, string>
                {
                    { 16, "..KKYYKKKKYYKK" },
                    { 17, "..KKKK....KKKK" },
                },
            },
            new PokemonSprite
            {
                Key = "charmander",
                NameKo = "파이리",
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
                Rows = new string[]
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
                StepRows = new Dictionary<int, string>
                {
                    { 14, "....KOOKKKKOOK" },
                    { 15, "...KKKK....KKKK" },
                },
            },
            new PokemonSprite
            {
                Key = "bulbasaur",
                NameKo = "이상해씨",
                Palette = new Dictionary<char, string>
                {
                    { 'K', "#2b2b2b" },
                    { 'T', "#7ec8a4" },
                    { 'D', "#4e9e7c" },
                    { 'G', "#6dbe45" },
                    { 'E', "#4a9130" },
                    { 'W', "#ffffff" },
                },
                Rows = new string[]
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
                StepRows = new Dictionary<int, string>
                {
                    { 14, "..KKTTKKKKKKTTKK" },
                    { 15, "..KKKK......KKKK" },
                },
            },
            new PokemonSprite
            {
                Key = "squirtle",
                NameKo = "꼬부기",
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
                Rows = new string[]
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
                StepRows = new Dictionary<int, string>
                {
                    { 14, "...KKBBKKKKBBKK" },
                    { 15, "...KKKK....KKKK" },
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
