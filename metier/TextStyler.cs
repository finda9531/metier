#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace eep.editer1
{
    public class TextStyler
    {
        private readonly RichTextBox _richTextBox;
        private long _lastShiftReleaseTime = 0;
        private long _lastSpaceReleaseTime = 0;

        private readonly Dictionary<string, Color> _colorDictionary = new Dictionary<string, Color>();
        private readonly List<string> _sortedColorKeys;
        private readonly Dictionary<string, Func<Color, Color>> _modifiers = new Dictionary<string, Func<Color, Color>>();
        private readonly List<(string Name, Color Color)> _standardCategories = new List<(string, Color)>();

        private const int DOUBLE_TAP_SPEED = 600;
        private const string FONT_FAMILY = "Meiryo UI";
        private const float FONT_SIZE_NORMAL = 14;
        private const float FONT_SIZE_HEADING = 24;

        public TextStyler(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;

            InitializeColorClassifier();
            InitializeModifiers();

            _sortedColorKeys = _colorDictionary.Keys.OrderByDescending(k => k.Length).ToList();
        }

        public int HandleShiftKeyUp()
        {
            long now = DateTime.Now.Ticks / 10000;
            int appliedHeight = 0;

            if (now - _lastShiftReleaseTime < DOUBLE_TAP_SPEED)
            {
                appliedHeight = ApplyHeadingLogic();
                _lastShiftReleaseTime = 0;
            }
            else _lastShiftReleaseTime = now;

            return appliedHeight;
        }

        public void HandleSpaceKeyUp()
        {
            long now = DateTime.Now.Ticks / 10000;
            if (now - _lastSpaceReleaseTime < DOUBLE_TAP_SPEED)
            {
                int currentPos = _richTextBox.SelectionStart;
                if (currentPos >= 2)
                {
                    _richTextBox.Select(currentPos - 2, 2);
                    ResetSelectionStyle();
                    _richTextBox.SelectedText = " ";
                    ResetSelectionStyle();
                }
                _lastSpaceReleaseTime = 0;
            }
            else _lastSpaceReleaseTime = now;
        }

        public bool ToggleColor(bool keepTriggerWord)
        {
            int caretPos = _richTextBox.SelectionStart;
            if (caretPos == 0) return false;

            int searchStart = GetTriggerChunkStart(caretPos);
            string chunkText = _richTextBox.Text.Substring(searchStart, caretPos - searchStart);

            string matchedKey = null;
            string matchedInput = null;

            foreach (var key in _sortedColorKeys)
            {
                if (chunkText.EndsWith(key))
                {
                    matchedKey = key;
                    matchedInput = key;
                    break;
                }
                if (chunkText.EndsWith(key + "色"))
                {
                    matchedKey = key;
                    matchedInput = key + "色";
                    break;
                }
            }

            if (matchedKey != null)
            {
                string prefix = chunkText.Substring(0, chunkText.Length - matchedInput.Length);
                Color baseColor = GetBaseColorFromKey(matchedKey);
                Color finalColor = ApplyModifier(baseColor, prefix, out int modLength);

                if (modLength > 0)
                {
                    matchedInput = prefix.Substring(prefix.Length - modLength) + matchedInput;
                }

                Color targetColor;
                if (modLength > 0)
                {
                    targetColor = finalColor;
                }
                else
                {
                    targetColor = IdentifyCategoryColor(matchedKey);
                }

                ApplyColorLogic(caretPos, matchedInput, targetColor, keepTriggerWord);
                return true;
            }

            return false;
        }

        private void ApplyColorLogic(int caretPos, string matchedInput, Color targetColor, bool keepTriggerWord)
        {
            int keywordStartPos = caretPos - matchedInput.Length;
            bool isPatternB = (keywordStartPos == 0) || char.IsWhiteSpace(_richTextBox.Text[keywordStartPos - 1]);

            if (isPatternB)
            {
                if (!keepTriggerWord)
                {
                    _richTextBox.Select(keywordStartPos, matchedInput.Length);
                    _richTextBox.SelectedText = "";
                }

                bool isAlreadyTargetColor = (_richTextBox.SelectionColor.ToArgb() == targetColor.ToArgb());
                _richTextBox.SelectionColor = isAlreadyTargetColor ? Color.Black : targetColor;
            }
            else
            {
                int rangeStart = GetColorRangeStart(keywordStartPos);
                int modifyLength = keywordStartPos - rangeStart;

                if (!keepTriggerWord)
                {
                    _richTextBox.Select(keywordStartPos, matchedInput.Length);
                    _richTextBox.SelectedText = "";
                }

                if (modifyLength > 0)
                {
                    _richTextBox.Select(rangeStart, modifyLength);
                    bool isAlreadyTargetColor = (_richTextBox.SelectionColor.ToArgb() == targetColor.ToArgb());
                    _richTextBox.SelectionColor = isAlreadyTargetColor ? Color.Black : targetColor;

                    _richTextBox.Select(rangeStart + modifyLength, 0);
                    _richTextBox.SelectionColor = Color.Black;
                }
            }

            _richTextBox.Focus();
        }

        private int ApplyHeadingLogic()
        {
            int caretPos = _richTextBox.SelectionStart;
            bool isAfterCharacter = caretPos > 0 && !char.IsWhiteSpace(_richTextBox.Text[caretPos - 1]);
            int resultHeight = 0;

            if (isAfterCharacter)
            {
                int startPos = GetTriggerChunkStart(caretPos);
                int length = caretPos - startPos;

                _richTextBox.Select(startPos, length);
                Font newFont = ToggleCurrentSelectionFont();
                if (newFont.Size >= 20) resultHeight = newFont.Height;

                _richTextBox.Select(caretPos, 0);
                _richTextBox.SelectionFont = new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular);
            }
            else
            {
                Font newFont = ToggleCurrentSelectionFont();
                resultHeight = newFont.Height;
            }
            _richTextBox.Focus();
            return resultHeight;
        }

        private Font ToggleCurrentSelectionFont()
        {
            Font currentFont = _richTextBox.SelectionFont;
            bool isHeading = (currentFont != null && currentFont.Size >= 20);
            Font newFont = isHeading ? new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular)
                                     : new Font(FONT_FAMILY, FONT_SIZE_HEADING, FontStyle.Bold);
            _richTextBox.SelectionFont = newFont;
            return newFont;
        }

        public void ResetToNormalFont()
        {
            Font currentFont = _richTextBox.SelectionFont;
            if (currentFont != null && currentFont.Size >= 20)
            {
                _richTextBox.SelectionFont = new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular);
            }
        }

        public void ResetColorToBlack() => _richTextBox.SelectionColor = Color.Black;

        private void ResetSelectionStyle()
        {
            ResetColorToBlack();
            ResetToNormalFont();
        }

        private Color GetBaseColorFromKey(string key)
        {
            if (_colorDictionary.TryGetValue(key, out Color c)) return c;
            return Color.Black;
        }

        private string NormalizeInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.EndsWith("色") ? input.Substring(0, input.Length - 1) : input;
        }

        private Color IdentifyCategoryColor(string inputWord)
        {
            string key = NormalizeInput(inputWord);

            if (!_colorDictionary.TryGetValue(key, out Color hitColor))
            {
                if (!_colorDictionary.TryGetValue(inputWord, out hitColor)) return Color.Black;
            }

            // “色相・彩度・明度”
            float h1 = hitColor.GetHue();
            float s1 = hitColor.GetSaturation();
            float b1 = hitColor.GetBrightness();

            Color bestColor = Color.Black;
            double minDistance = double.MaxValue;

            foreach (var cat in _standardCategories)
            {
                float h2 = cat.Color.GetHue();
                float s2 = cat.Color.GetSaturation();
                float b2 = cat.Color.GetBrightness();

                // 色相の差
                float dh = Math.Abs(h1 - h2);
                if (dh > 180) dh = 360 - dh;
                float normalizedDh = dh / 180.0f; // 0.0 ~ 1.0

                // 彩度が極端に低い “無彩色” と “有彩色” が混ざらないように重み付け
          
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


        private int GetTriggerChunkStart(int caretPos)
        {
            int limit = Math.Max(0, caretPos - 50);
            int startPos = limit;

            for (int i = caretPos - 1; i >= limit; i--)
            {
                if (char.IsWhiteSpace(_richTextBox.Text[i]))
                {
                    startPos = i + 1;
                    break;
                }
            }
            return startPos;
        }

        private int GetColorRangeStart(int keywordStartPos)
        {
            int startPos = keywordStartPos;
            bool encounteredPunctuationAtEnd = false;
            int limit = Math.Max(0, keywordStartPos - 200);

            for (int i = keywordStartPos - 1; i >= limit; i--)
            {
                char c = _richTextBox.Text[i];

                if (char.IsWhiteSpace(c))
                {
                    startPos = i + 1;
                    break;
                }

                if (c == '。' || c == '、' || c == '.' || c == ',')
                {
                    if (i == keywordStartPos - 1)
                    {
                        encounteredPunctuationAtEnd = true;
                        continue;
                    }
                    if (encounteredPunctuationAtEnd)
                    {
                        startPos = i + 1;
                        break;
                    }
                    startPos = i + 1;
                    break;
                }
                if (i == 0) startPos = 0;
            }
            return startPos;
        }

        private void InitializeModifiers()
        {
            Func<Color, Color> lighter = c => ControlPaint.Light(c, 0.6f);
            Func<Color, Color> darker = c => ControlPaint.Dark(c, 0.3f);
            Func<Color, Color> pastel = c => ControlPaint.Light(c, 0.3f);

            void AddMod(string[] words, Func<Color, Color> func)
            {
                foreach (var w in words) _modifiers[w] = func;
            }

            AddMod(new[] { "薄い", "うすい", "淡い", "あわい", "ライトな" }, lighter);
            AddMod(new[] { "暗い", "くらい", "濃い", "こい", "ダークな" }, darker);
            AddMod(new[] { "パステル", "ぱすてる", "明るい", "あかるい" }, pastel);
        }

        private Color ApplyModifier(Color baseColor, string textBeforeColor, out int modifierLength)
        {
            modifierLength = 0;
            foreach (var mod in _modifiers)
            {
                if (textBeforeColor.EndsWith(mod.Key))
                {
                    modifierLength = mod.Key.Length;
                    return mod.Value(baseColor);
                }
            }
            return baseColor;
        }

        #region Color Definitions

        private void InitializeColorClassifier()
        {
            void AddColor(string[] names, int r, int g, int b)
            {
                Color c = Color.FromArgb(r, g, b);
                foreach (var name in names) _colorDictionary[name] = c;
            }
            void Add(string name, int r, int g, int b) => _colorDictionary[name] = Color.FromArgb(r, g, b);

            _standardCategories.Clear();
            //標準カテゴリ
            _standardCategories.Add(("BLACK", Color.Black));
            _standardCategories.Add(("DIM_GRAY", Color.DimGray));
            _standardCategories.Add(("GRAY", Color.Gray));
            _standardCategories.Add(("SILVER", Color.Silver));
            _standardCategories.Add(("WHITE", Color.White));
            _standardCategories.Add(("RED", Color.Red));
            _standardCategories.Add(("MAROON", Color.Maroon));
            _standardCategories.Add(("CRIMSON", Color.Crimson));
            _standardCategories.Add(("SALMON", Color.Salmon));
            _standardCategories.Add(("PINK", Color.HotPink));
            _standardCategories.Add(("LIGHT_PINK", Color.Pink));
            _standardCategories.Add(("MAGENTA", Color.Magenta));
            _standardCategories.Add(("PURPLE", Color.Purple));
            _standardCategories.Add(("INDIGO", Color.Indigo));
            _standardCategories.Add(("LAVENDER", Color.Lavender));
            _standardCategories.Add(("BLUE", Color.Blue));
            _standardCategories.Add(("NAVY", Color.Navy));
            _standardCategories.Add(("ROYAL_BLUE", Color.RoyalBlue));
            _standardCategories.Add(("SKY_BLUE", Color.DeepSkyBlue));
            _standardCategories.Add(("CYAN", Color.Cyan));
            _standardCategories.Add(("TEAL", Color.Teal));
            _standardCategories.Add(("GREEN", Color.Green));
            _standardCategories.Add(("DARK_GREEN", Color.DarkGreen));
            _standardCategories.Add(("LIME", Color.Lime));
            _standardCategories.Add(("OLIVE", Color.Olive));
            _standardCategories.Add(("YELLOW", Color.Gold));
            _standardCategories.Add(("ORANGE", Color.Orange));
            _standardCategories.Add(("BROWN", Color.SaddleBrown));
            _standardCategories.Add(("BEIGE", Color.Beige));
            _standardCategories.Add(("YELLOW_GREEN", Color.YellowGreen));
            _standardCategories.Add(("LAWN_GREEN", Color.LawnGreen));
            _standardCategories.Add(("CREAM", Color.LemonChiffon));
            _standardCategories.Add(("GOLDENROD", Color.Goldenrod));
            _standardCategories.Add(("CHOCOLATE", Color.Chocolate));
            //カラー辞書
            AddColor(new[] { "赤", "あか", "アカ", "RED", "red" }, 255, 0, 0);
            AddColor(new[] { "紅", "べに", "ベニ", "クリムゾン", "くりむぞん" }, 220, 20, 60);
            AddColor(new[] { "朱", "しゅ", "あけ", "バーミリオン", "ばーみりおん" }, 235, 97, 1);
            AddColor(new[] { "茜", "あかね" }, 167, 53, 62);
            AddColor(new[] { "金赤", "きんあか" }, 234, 85, 80);
            AddColor(new[] { "エンジ", "えんじ", "臙脂" }, 100, 0, 0);
            AddColor(new[] { "緋", "ひ", "あけ", "スカーレット", "すかーれっと" }, 255, 36, 0);
            AddColor(new[] { "桃", "もも", "ピーチ", "ぴーち", "PINK", "pink", "ぴんく" }, 255, 192, 203);
            AddColor(new[] { "桜", "さくら", "サクラ" }, 254, 223, 225);
            AddColor(new[] { "薔薇", "ばら", "ローズ", "ろーず" }, 255, 0, 127);
            AddColor(new[] { "珊瑚", "さんご", "コーラル", "こーらる" }, 255, 127, 80);
            AddColor(new[] { "サーモンピンク", "さーもんぴんく" }, 255, 145, 164);
            AddColor(new[] { "撫子", "なでしこ" }, 238, 187, 204);
            AddColor(new[] { "マゼンタ", "まぜんた", "MAGENTA" }, 255, 0, 255);
            AddColor(new[] { "牡丹", "ぼたん" }, 211, 47, 127);
            AddColor(new[] { "つつじ" }, 233, 82, 149);

            AddColor(new[] { "橙", "だいだい", "オレンジ", "おれんじ", "ORANGE", "orange" }, 255, 165, 0);
            AddColor(new[] { "柿", "かき" }, 237, 109, 53);
            AddColor(new[] { "杏", "あんず", "アプリコット", "あぷりこっと" }, 247, 185, 119);
            AddColor(new[] { "蜜柑", "みかん", "マンダリン", "まんだりん" }, 245, 130, 32);
            AddColor(new[] { "茶", "ちゃ", "ブラウン", "ぶらうん", "BROWN", "brown" }, 165, 42, 42);
            AddColor(new[] { "焦茶", "こげちゃ" }, 107, 68, 35);
            AddColor(new[] { "栗", "くり", "マロン", "まろん" }, 118, 47, 7);
            AddColor(new[] { "チョコレート", "ちょこれーと", "チョコ", "ちょこ" }, 58, 36, 33);
            AddColor(new[] { "コーヒー", "こーひー" }, 75, 54, 33);
            AddColor(new[] { "駱駝", "らくだ", "キャメル", "きゃめる" }, 193, 154, 107);
            AddColor(new[] { "ベージュ", "べーじゅ", "肌", "はだ" }, 245, 245, 220);
            AddColor(new[] { "黄土", "おうど", "オーカー", "おーかー" }, 195, 145, 67);
            AddColor(new[] { "琥珀", "こはく", "アンバー", "あんばー" }, 255, 191, 0);
            AddColor(new[] { "セピア", "せぴあ" }, 112, 66, 20);
            AddColor(new[] { "煉瓦", "れんが", "レンガ", "ブリック", "ぶりっく" }, 181, 82, 47);
            AddColor(new[] { "鳶", "とび" }, 149, 72, 63);

            AddColor(new[] { "黄", "き", "イエロー", "いえろー", "YELLOW", "yellow" }, 255, 255, 0);
            AddColor(new[] { "山吹", "やまぶき" }, 248, 181, 0);
            AddColor(new[] { "金", "きん", "ゴールド", "ごーるど", "GOLD" }, 255, 215, 0);
            AddColor(new[] { "レモン", "れもん" }, 255, 243, 82);
            AddColor(new[] { "クリーム", "くりーむ" }, 255, 253, 208);
            AddColor(new[] { "象牙", "ぞうげ", "アイボリー", "あいぼりー" }, 255, 255, 240);
            AddColor(new[] { "向日葵", "ひまわり" }, 255, 219, 0);
            AddColor(new[] { "芥子", "からし", "マスタード", "ますたーど" }, 208, 176, 54);
            AddColor(new[] { "ウコン", "うこん", "ターメリック", "たーめりっく" }, 250, 186, 12);
            AddColor(new[] { "カナリア", "かなりあ" }, 229, 216, 92);

            AddColor(new[] { "緑", "みどり", "グリーン", "ぐりーん", "GREEN", "green" }, 0, 128, 0);
            AddColor(new[] { "黄緑", "きみどり", "ライム", "らいむ" }, 50, 205, 50);
            AddColor(new[] { "深緑", "ふかみどり" }, 0, 85, 46);
            AddColor(new[] { "抹茶", "まっちゃ" }, 197, 197, 106);
            AddColor(new[] { "鶯", "うぐいす" }, 146, 139, 58);
            AddColor(new[] { "若草", "わかくさ" }, 195, 216, 37);
            AddColor(new[] { "萌黄", "もえぎ" }, 167, 189, 0);
            AddColor(new[] { "苔", "こけ", "モスグリーン", "もすぐりーん" }, 119, 150, 86);
            AddColor(new[] { "オリーブ", "おりーぶ" }, 128, 128, 0);
            AddColor(new[] { "エメラルド", "えめらるど" }, 80, 200, 120);
            AddColor(new[] { "翡翠", "ひすい", "ジェイド", "じぇいど" }, 56, 176, 137);
            AddColor(new[] { "常盤", "ときわ" }, 0, 123, 67);
            AddColor(new[] { "ビリジアン", "びりじあん" }, 0, 125, 101);
            AddColor(new[] { "フォレスト", "ふぉれすと" }, 34, 139, 34);
            AddColor(new[] { "ミント", "みんと" }, 189, 252, 201);
            AddColor(new[] { "海松", "みる" }, 114, 109, 66);
            AddColor(new[] { "青磁", "せいじ" }, 126, 190, 171);

            AddColor(new[] { "青", "あお", "アオ", "ブルー", "ぶるー", "BLUE", "blue" }, 0, 0, 255);
            AddColor(new[] { "水", "みず", "ライトブルー", "らいとぶるー" }, 173, 216, 230);
            AddColor(new[] { "シアン", "しあん", "CYAN" }, 0, 255, 255);
            AddColor(new[] { "空", "そら", "スカイブルー", "すかいぶるー" }, 135, 206, 235);
            AddColor(new[] { "紺", "こん", "ネイビー", "ねいびー", "NAVY" }, 0, 0, 128);
            AddColor(new[] { "藍", "あい", "インディゴ", "いんでぃご" }, 75, 0, 130);
            AddColor(new[] { "群青", "ぐんじょう", "ウルトラマリン", "うるとらまりん" }, 70, 70, 175);
            AddColor(new[] { "瑠璃", "るり", "ラピスラズリ", "らぴすらずり" }, 31, 71, 136);
            AddColor(new[] { "浅葱", "あさぎ" }, 0, 163, 175);
            AddColor(new[] { "新橋", "しんばし" }, 89, 185, 198);
            AddColor(new[] { "ターコイズ", "たーこいず", "トルコ石", "とるこいし" }, 64, 224, 208);
            AddColor(new[] { "アクアマリン", "あくあまりん" }, 127, 255, 212);
            AddColor(new[] { "ロイヤルブルー", "ろいやるぶるー" }, 65, 105, 225);
            AddColor(new[] { "ミッドナイトブルー", "みっどないとぶるー" }, 25, 25, 112);
            AddColor(new[] { "サックス", "さっくす" }, 75, 144, 194);
            AddColor(new[] { "鉄紺", "てつこん" }, 23, 27, 38);

            AddColor(new[] { "紫", "むらさき", "パープル", "ぱーぷる", "PURPLE", "purple" }, 128, 0, 128);
            AddColor(new[] { "菫", "すみれ", "バイオレット", "ばいおれっと" }, 238, 130, 238);
            AddColor(new[] { "藤", "ふじ", "ウィステリア", "うぃすてりあ" }, 187, 188, 222);
            AddColor(new[] { "菖蒲", "あやめ", "アイリス", "あいりす" }, 204, 125, 182);
            AddColor(new[] { "桔梗", "ききょう" }, 104, 72, 169);
            AddColor(new[] { "ラベンダー", "らべんだー" }, 230, 230, 250);
            AddColor(new[] { "ライラック", "らいらっく" }, 200, 162, 200);
            AddColor(new[] { "江戸紫", "えどむらさき" }, 116, 83, 153);
            AddColor(new[] { "古代紫", "こだいむらさき" }, 137, 91, 138);
            AddColor(new[] { "京紫", "きょうむらさき" }, 157, 94, 135);
            AddColor(new[] { "葡萄", "ぶどう", "グレープ", "ぐれーぷ" }, 106, 75, 106);
            AddColor(new[] { "オーキッド", "おーきっど", "蘭", "らん" }, 218, 112, 214);
            AddColor(new[] { "プラム", "ぷらむ" }, 221, 160, 221);

            AddColor(new[] { "白", "しろ", "ホワイト", "ほわいと", "WHITE", "white" }, 255, 255, 255);
            AddColor(new[] { "黒", "くろ", "ブラック", "ぶらっく", "BLACK", "black" }, 0, 0, 0);
            AddColor(new[] { "灰", "はい", "グレー", "ぐれー", "グレイ", "ぐれい", "GRAY", "gray" }, 128, 128, 128);
            AddColor(new[] { "鼠", "ねずみ", "マウスグレー", "まうすぐれー" }, 148, 148, 148);
            AddColor(new[] { "銀", "ぎん", "シルバー", "しるばー", "SILVER" }, 192, 192, 192);
            AddColor(new[] { "墨", "すみ" }, 89, 88, 87);
            AddColor(new[] { "鉛", "なまり" }, 119, 120, 123);
            AddColor(new[] { "木炭", "もくたん", "チャコール", "ちゃこーる" }, 54, 69, 79);
            AddColor(new[] { "スレート", "すれーと" }, 112, 128, 144);
            AddColor(new[] { "利休鼠", "りきゅうねずみ" }, 136, 142, 126);
            AddColor(new[] { "深川鼠", "ふかがわねずみ" }, 133, 169, 174);
            AddColor(new[] { "鳩羽鼠", "はとばねずみ" }, 158, 143, 150);

            AddColor(new[] { "虹", "にじ", "レインボー", "れいんぼー" }, 255, 255, 255);
            Add("透明", 255, 255, 255);
            Add("とうめい", 255, 255, 255);
        }

        #endregion
    }
}