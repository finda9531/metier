#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Metier // 一般的な命名規則に合わせて大文字にしました
{
    /// <summary>
    /// 色の定義データと、色の検索・変換・近似色計算ロジックを管理するクラス
    /// </summary>
    public class ColorRepository
    {
        public Dictionary<string, Color> ColorMap { get; } = [];
        public List<string> SortedKeys { get; private set; }
        public Dictionary<string, Func<Color, Color>> Modifiers { get; } = [];

        // 標準カテゴリ
        public List<(string Name, Color Color)> StandardCategories { get; } =
        [
            ("BLACK", Color.Black),
            ("DIM_GRAY", Color.DimGray),
            ("GRAY", Color.Gray),
            ("SILVER", Color.Silver),
            ("WHITE", Color.White),
            ("RED", Color.Red),
            ("MAROON", Color.Maroon),
            ("CRIMSON", Color.Crimson),
            ("SALMON", Color.Salmon),
            ("PINK", Color.HotPink),
            ("LIGHT_PINK", Color.Pink),
            ("MAGENTA", Color.Magenta),
            ("PURPLE", Color.Purple),
            ("INDIGO", Color.Indigo),
            ("LAVENDER", Color.Lavender),
            ("BLUE", Color.Blue),
            ("NAVY", Color.Navy),
            ("ROYAL_BLUE", Color.RoyalBlue),
            ("SKY_BLUE", Color.DeepSkyBlue),
            ("CYAN", Color.Cyan),
            ("TEAL", Color.Teal),
            ("GREEN", Color.Green),
            ("DARK_GREEN", Color.DarkGreen),
            ("LIME", Color.Lime),
            ("OLIVE", Color.Olive),
            ("YELLOW", Color.Gold),
            ("ORANGE", Color.Orange),
            ("BROWN", Color.SaddleBrown),
            ("BEIGE", Color.Beige),
            ("YELLOW_GREEN", Color.YellowGreen),
            ("LAWN_GREEN", Color.LawnGreen),
            ("CREAM", Color.LemonChiffon),
            ("GOLDENROD", Color.Goldenrod),
            ("CHOCOLATE", Color.Chocolate)
        ];

        // 静的データ定義
        private static readonly (string[] Names, int R, int G, int B)[] _colorDefinitions =
        [
            // --- 赤・ピンク系 ---
            (["赤", "あか", "アカ", "RED", "red"], 255, 0, 0),
            (["紅", "べに", "ベニ", "クリムゾン", "くりむぞん"], 220, 20, 60),
            (["朱", "しゅ", "あけ", "バーミリオン", "ばーみりおん"], 235, 97, 1),
            (["茜", "あかね"], 167, 53, 62),
            (["金赤", "きんあか"], 234, 85, 80),
            (["エンジ", "えんじ", "臙脂"], 100, 0, 0),
            (["緋", "ひ", "あけ", "スカーレット", "すかーれっと"], 255, 36, 0),
            (["桃", "もも", "ピーチ", "ぴーち", "PINK", "pink", "ぴんく"], 255, 192, 203),
            (["桜", "さくら", "サクラ"], 254, 223, 225),
            (["薔薇", "ばら", "ローズ", "ろーず"], 255, 0, 127),
            (["珊瑚", "さんご", "コーラル", "こーらる"], 255, 127, 80),
            (["サーモンピンク", "さーもんぴんく"], 255, 145, 164),
            (["撫子", "なでしこ"], 238, 187, 204),
            (["マゼンタ", "まぜんた", "MAGENTA"], 255, 0, 255),
            (["牡丹", "ぼたん"], 211, 47, 127),
            (["つつじ"], 233, 82, 149),

            // --- 橙・茶系 ---
            (["橙", "だいだい", "オレンジ", "おれんじ", "ORANGE", "orange"], 255, 165, 0),
            (["柿", "かき"], 237, 109, 53),
            (["杏", "あんず", "アプリコット", "あぷりこっと"], 247, 185, 119),
            (["蜜柑", "みかん", "マンダリン", "まんだりん"], 245, 130, 32),
            (["茶", "ちゃ", "ブラウン", "ぶらうん", "BROWN", "brown"], 165, 42, 42),
            (["焦茶", "こげちゃ"], 107, 68, 35),
            (["栗", "くり", "マロン", "まろん"], 118, 47, 7),
            (["チョコレート", "ちょこれーと", "チョコ", "ちょこ"], 58, 36, 33),
            (["コーヒー", "こーひー"], 75, 54, 33),
            (["駱駝", "らくだ", "キャメル", "きゃめる"], 193, 154, 107),
            (["ベージュ", "べーじゅ", "肌", "はだ"], 245, 245, 220),
            (["黄土", "おうど", "オーカー", "おーかー"], 195, 145, 67),
            (["琥珀", "こはく", "アンバー", "あんばー"], 255, 191, 0),
            (["セピア", "せぴあ"], 112, 66, 20),
            (["煉瓦", "れんが", "レンガ", "ブリック", "ぶりっく"], 181, 82, 47),
            (["鳶", "とび"], 149, 72, 63),

            // --- 黄・金系 ---
            (["黄", "き", "イエロー", "いえろー", "YELLOW", "yellow"], 255, 255, 0),
            (["山吹", "やまぶき"], 248, 181, 0),
            (["金", "きん", "ゴールド", "ごーるど", "GOLD"], 255, 215, 0),
            (["レモン", "れもん"], 255, 243, 82),
            (["クリーム", "くりーむ"], 255, 253, 208),
            (["象牙", "ぞうげ", "アイボリー", "あいぼりー"], 255, 255, 240),
            (["向日葵", "ひまわり"], 255, 219, 0),
            (["芥子", "からし", "マスタード", "ますたーど"], 208, 176, 54),
            (["ウコン", "うこん", "ターメリック", "たーめりっく"], 250, 186, 12),
            (["カナリア", "かなりあ"], 229, 216, 92),

            // --- 緑・黄緑系 ---
            (["緑", "みどり", "グリーン", "ぐりーん", "GREEN", "green"], 0, 128, 0),
            (["黄緑", "きみどり", "ライム", "らいむ"], 50, 205, 50),
            (["深緑", "ふかみどり"], 0, 85, 46),
            (["抹茶", "まっちゃ"], 197, 197, 106),
            (["鶯", "うぐいす"], 146, 139, 58),
            (["若草", "わかくさ"], 195, 216, 37),
            (["萌黄", "もえぎ"], 167, 189, 0),
            (["苔", "こけ", "モスグリーン", "もすぐりーん"], 119, 150, 86),
            (["オリーブ", "おりーぶ"], 128, 128, 0),
            (["エメラルド", "えめらるど"], 80, 200, 120),
            (["翡翠", "ひすい", "ジェイド", "じぇいど"], 56, 176, 137),
            (["常盤", "ときわ"], 0, 123, 67),
            (["ビリジアン", "びりじあん"], 0, 125, 101),
            (["フォレスト", "ふぉれすと"], 34, 139, 34),
            (["ミント", "みんと"], 189, 252, 201),
            (["海松", "みる"], 114, 109, 66),
            (["青磁", "せいじ"], 126, 190, 171),

            // --- 青・水色系 ---
            (["青", "あお", "アオ", "ブルー", "ぶるー", "BLUE", "blue"], 0, 0, 255),
            (["水", "みず", "ライトブルー", "らいとぶるー"], 173, 216, 230),
            (["シアン", "しあん", "CYAN"], 0, 255, 255),
            (["空", "そら", "スカイブルー", "すかいぶるー"], 135, 206, 235),
            (["紺", "こん", "ネイビー", "ねいびー", "NAVY"], 0, 0, 128),
            (["藍", "あい", "インディゴ", "いんでぃご"], 75, 0, 130),
            (["群青", "ぐんじょう", "ウルトラマリン", "うるとらまりん"], 70, 70, 175),
            (["瑠璃", "るり", "ラピスラズリ", "らぴすらずり"], 31, 71, 136),
            (["浅葱", "あさぎ"], 0, 163, 175),
            (["新橋", "しんばし"], 89, 185, 198),
            (["ターコイズ", "たーこいず", "トルコ石", "とるこいし"], 64, 224, 208),
            (["アクアマリン", "あくあまりん"], 127, 255, 212),
            (["ロイヤルブルー", "ろいやるぶるー"], 65, 105, 225),
            (["ミッドナイトブルー", "みっどないとぶるー"], 25, 25, 112),
            (["サックス", "さっくす"], 75, 144, 194),
            (["鉄紺", "てつこん"], 23, 27, 38),

            // --- 紫・菫系 ---
            (["紫", "むらさき", "パープル", "ぱーぷる", "PURPLE", "purple"], 128, 0, 128),
            (["菫", "すみれ", "バイオレット", "ばいおれっと"], 238, 130, 238),
            (["藤", "ふじ", "ウィステリア", "うぃすてりあ"], 187, 188, 222),
            (["菖蒲", "あやめ", "アイリス", "あいりす"], 204, 125, 182),
            (["桔梗", "ききょう"], 104, 72, 169),
            (["ラベンダー", "らべんだー"], 230, 230, 250),
            (["ライラック", "らいらっく"], 200, 162, 200),
            (["江戸紫", "えどむらさき"], 116, 83, 153),
            (["古代紫", "こだいむらさき"], 137, 91, 138),
            (["京紫", "きょうむらさき"], 157, 94, 135),
            (["葡萄", "ぶどう", "グレープ", "ぐれーぷ"], 106, 75, 106),
            (["オーキッド", "おーきっど", "蘭", "らん"], 218, 112, 214),
            (["プラム", "ぷらむ"], 221, 160, 221),

            // --- 白・黒・灰系 ---
            (["白", "しろ", "ホワイト", "ほわいと", "WHITE", "white"], 255, 255, 255),
            (["黒", "くろ", "ブラック", "ぶらっく", "BLACK", "black"], 0, 0, 0),
            (["灰", "はい", "グレー", "ぐれー", "グレイ", "ぐれい", "GRAY", "gray"], 128, 128, 128),
            (["鼠", "ねずみ", "マウスグレー", "まうすぐれー"], 148, 148, 148),
            (["銀", "ぎん", "シルバー", "しるばー", "SILVER"], 192, 192, 192),
            (["墨", "すみ"], 89, 88, 87),
            (["鉛", "なまり"], 119, 120, 123),
            (["木炭", "もくたん", "チャコール", "ちゃこーる"], 54, 69, 79),
            (["スレート", "すれーと"], 112, 128, 144),
            (["利休鼠", "りきゅうねずみ"], 136, 142, 126),
            (["深川鼠", "ふかがわねずみ"], 133, 169, 174),
            (["鳩羽鼠", "はとばねずみ"], 158, 143, 150),

            // --- その他 ---
            (["虹", "にじ", "レインボー", "れいんぼー"], 255, 255, 255),
            (["透明", "とうめい"], 255, 255, 255)
        ];

        public ColorRepository()
        {
            InitializeColors();
            InitializeModifiers();

            // 検索時に「長い単語」を優先させるため、文字数降順でソートしておく
            SortedKeys = [.. ColorMap.Keys.OrderByDescending(k => k.Length)];
        }

        public Color GetBaseColor(string key)
        {
            return ColorMap.TryGetValue(key, out Color c) ? c : Color.Black;
        }

        public Color ApplyModifier(Color baseColor, string prefixText, out int matchedLength)
        {
            matchedLength = 0;
            if (string.IsNullOrEmpty(prefixText)) return baseColor;

            var match = Modifiers.Keys
                .Where(prefixText.EndsWith)
                .OrderByDescending(k => k.Length)
                .FirstOrDefault();

            if (match != null)
            {
                matchedLength = match.Length;
                return Modifiers[match](baseColor);
            }

            return baseColor;
        }

        public Color GetClosestCategoryColor(string inputWord)
        {
            string key = NormalizeInput(inputWord);

            if (!ColorMap.TryGetValue(key, out Color hitColor))
            {
                if (!ColorMap.TryGetValue(inputWord, out hitColor)) return Color.Black;
            }

            float h1 = hitColor.GetHue();
            float s1 = hitColor.GetSaturation();
            float b1 = hitColor.GetBrightness();

            Color bestColor = Color.Black;
            double minDistance = double.MaxValue;

            foreach (var cat in StandardCategories)
            {
                float h2 = cat.Color.GetHue();
                float s2 = cat.Color.GetSaturation();
                float b2 = cat.Color.GetBrightness();

                float dh = Math.Abs(h1 - h2);
                if (dh > 180) dh = 360 - dh;
                float normalizedDh = dh / 180.0f;

                float hueWeight = (s1 < 0.15f || s2 < 0.15f) ? 0.0f : 1.5f;

                double dist = Math.Pow(normalizedDh * hueWeight, 2) +
                              Math.Pow(s1 - s2, 2) +
                              Math.Pow(b1 - b2, 2);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestColor = cat.Color;
                }
            }
            return bestColor;
        }

        private static string NormalizeInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.EndsWith('色') ? input[..^1] : input;
        }

        private void InitializeModifiers()
        {
            static Color Lighter(Color c) => ControlPaint.Light(c, 0.6f);
            static Color Darker(Color c) => ControlPaint.Dark(c, 0.3f);
            static Color Pastel(Color c) => ControlPaint.Light(c, 0.3f);

            void AddMod(string[] words, Func<Color, Color> func)
            {
                foreach (var w in words) Modifiers[w] = func;
            }

            AddMod(["薄い", "うすい", "淡い", "あわい", "ライトな"], Lighter);
            AddMod(["暗い", "くらい", "濃い", "こい", "ダークな"], Darker);
            AddMod(["パステル", "ぱすてる", "明るい", "あかるい"], Pastel);
        }

        private void InitializeColors()
        {
            foreach (var (names, r, g, b) in _colorDefinitions)
            {
                Color c = Color.FromArgb(r, g, b);
                foreach (var name in names)
                {
                    ColorMap[name] = c;
                }
            }
        }
    }
}