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

        // 色辞書データ
        private readonly Dictionary<string, Color> _colorDictionary = new Dictionary<string, Color>();
        // パフォーマンス用: 長い順にソート済みのキーリスト
        private readonly List<string> _sortedColorKeys;
        // 色判定用の標準カテゴリ（着地点）
        private readonly List<(string Name, Color Color)> _standardCategories = new List<(string, Color)>();

        // 定数
        private const int DOUBLE_TAP_SPEED = 600;
        private const string FONT_FAMILY = "Meiryo UI";
        private const float FONT_SIZE_NORMAL = 14;
        private const float FONT_SIZE_HEADING = 24;

        public TextStyler(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;

            // 色データの初期化
            InitializeColorClassifier();

            // キーワード検索用に、キーを「長い順」にソートしてキャッシュしておく
            _sortedColorKeys = _colorDictionary.Keys.OrderByDescending(k => k.Length).ToList();
        }

        // =========================================================
        //  公開メソッド (イベントハンドラ)
        // =========================================================

        /// <summary>
        /// Shiftキー連打時の処理（見出し化）
        /// </summary>
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

        /// <summary>
        /// Spaceキー連打時の処理（リセット）
        /// </summary>
        public void HandleSpaceKeyUp()
        {
            long now = DateTime.Now.Ticks / 10000;
            if (now - _lastSpaceReleaseTime < DOUBLE_TAP_SPEED)
            {
                int currentPos = _richTextBox.SelectionStart;
                if (currentPos >= 2)
                {
                    // 直前のスペースごとリセット
                    _richTextBox.Select(currentPos - 2, 2);
                    ResetSelectionStyle();

                    _richTextBox.SelectedText = " ";

                    ResetSelectionStyle();
                }
                _lastSpaceReleaseTime = 0;
            }
            else _lastSpaceReleaseTime = now;
        }

        /// <summary>
        /// Tabキー押下時の色変更処理
        /// </summary>
        public bool ToggleColor(bool keepTriggerWord)
        {
            int caretPos = _richTextBox.SelectionStart;
            if (caretPos == 0) return false;

            // 1. キーワードを探す範囲を特定（空白区切りで遡る）
            int searchStart = GetTriggerChunkStart(caretPos);
            if (searchStart >= caretPos) return false;

            string chunkText = _richTextBox.Text.Substring(searchStart, caretPos - searchStart);
            string matchedKey = null;
            string matchedInput = null;

            // 2. キャッシュ済みのソート済みキーリストを使って最長一致検索
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
                ApplyColorLogic(caretPos, matchedInput, keepTriggerWord);
                return true;
            }

            return false;
        }

        // =========================================================
        //  内部ロジック (スタイル適用)
        // =========================================================

        private void ApplyColorLogic(int caretPos, string matchedInput, bool keepTriggerWord)
        {
            // キーワードの開始位置
            int keywordStartPos = caretPos - matchedInput.Length;

            // Pattern A/B 判定: キーワードの直前が「行頭」または「空白」なら書き始めモード(B)
            bool isPatternB = (keywordStartPos == 0) || char.IsWhiteSpace(_richTextBox.Text[keywordStartPos - 1]);

            Color targetColor = IdentifyCategoryColor(matchedInput);

            if (isPatternB)
            {
                // 【Pattern B】書き始めモード（カーソル色変更のみ）
                if (!keepTriggerWord)
                {
                    _richTextBox.Select(keywordStartPos, matchedInput.Length);
                    _richTextBox.SelectedText = "";
                }

                // カーソル色をトグル
                bool isAlreadyTargetColor = (_richTextBox.SelectionColor.ToArgb() == targetColor.ToArgb());
                _richTextBox.SelectionColor = isAlreadyTargetColor ? Color.Black : targetColor;
            }
            else
            {
                // 【Pattern A】直前の文を塗るモード
                // 句読点を考慮して塗る範囲を計算
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

                    // 完了後は黒に戻す
                    _richTextBox.Select(rangeStart + modifyLength, 0);
                    _richTextBox.SelectionColor = Color.Black;
                }
            }

            _richTextBox.Focus();
        }

        private int ApplyHeadingLogic()
        {
            int caretPos = _richTextBox.SelectionStart;
            // 直前が文字なら「その文字」を、そうでなければ「これから打つ文字」を変更
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

        // =========================================================
        //  ヘルパーメソッド (フォント・色判定)
        // =========================================================

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

            Color bestColor = Color.Black;
            double minDistance = double.MaxValue;

            foreach (var cat in _standardCategories)
            {
                double dist = Math.Pow(hitColor.R - cat.Color.R, 2) +
                              Math.Pow(hitColor.G - cat.Color.G, 2) +
                              Math.Pow(hitColor.B - cat.Color.B, 2);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestColor = cat.Color;
                }
            }
            return bestColor;
        }

        // =========================================================
        //  チャンク判定ロジック
        // =========================================================

        // トリガー検出用: 空白で区切られた範囲を探す
        private int GetTriggerChunkStart(int caretPos)
        {
            int startPos = caretPos;
            // 検索範囲を最大20文字程度に制限してパフォーマンス確保
            int limit = Math.Max(0, caretPos - 20);

            for (int i = caretPos - 1; i >= limit; i--)
            {
                if (char.IsWhiteSpace(_richTextBox.Text[i]))
                {
                    startPos = i + 1;
                    break;
                }
                if (i == 0) startPos = 0;
            }
            return startPos;
        }

        // 色塗り範囲決定用: 句読点を考慮して賢く範囲を決める
        private int GetColorRangeStart(int keywordStartPos)
        {
            int startPos = keywordStartPos;
            bool encounteredPunctuationAtEnd = false;

            for (int i = keywordStartPos - 1; i >= 0; i--)
            {
                char c = _richTextBox.Text[i];

                if (char.IsWhiteSpace(c))
                {
                    startPos = i + 1;
                    break;
                }

                if (c == '。' || c == '、' || c == '.' || c == ',')
                {
                    // キーワード直結の句読点は含める
                    if (i == keywordStartPos - 1)
                    {
                        encounteredPunctuationAtEnd = true;
                        continue;
                    }
                    // 文末句読点を通過後の、次の句読点は区切りとみなす
                    if (encounteredPunctuationAtEnd)
                    {
                        startPos = i + 1;
                        break;
                    }
                    // それ以外の途中にある句読点も区切りとみなす
                    startPos = i + 1;
                    break;
                }

                if (i == 0) startPos = 0;
            }
            return startPos;
        }

        // =========================================================
        //  色定義データ (長大なのでRegion化)
        // =========================================================
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

            // --- 標準カテゴリ ---
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
            // 直前のやり取りで追加したカテゴリ
            _standardCategories.Add(("YELLOW_GREEN", Color.YellowGreen));
            _standardCategories.Add(("LAWN_GREEN", Color.LawnGreen));
            _standardCategories.Add(("CREAM", Color.LemonChiffon));
            _standardCategories.Add(("GOLDENROD", Color.Goldenrod));
            _standardCategories.Add(("CHOCOLATE", Color.Chocolate));

            // --- 辞書データ (300色規模) ---

            // 赤・ピンク系
            AddColor(new[] { "赤", "あか", "アカ", "RED", "red" }, 255, 0, 0);
            AddColor(new[] { "紅", "べに", "クリムゾン" }, 220, 20, 60);
            AddColor(new[] { "朱", "バーミリオン" }, 235, 97, 1);
            AddColor(new[] { "茜", "あかね" }, 167, 53, 62);
            AddColor(new[] { "金赤" }, 234, 85, 80);
            AddColor(new[] { "エンジ", "臙脂" }, 100, 0, 0);
            AddColor(new[] { "緋", "スカーレット" }, 255, 36, 0);
            AddColor(new[] { "桃", "もも", "ピーチ", "PINK", "pink" }, 255, 192, 203);
            AddColor(new[] { "桜", "さくら", "サクラ" }, 254, 223, 225);
            AddColor(new[] { "薔薇", "ローズ" }, 255, 0, 127);
            AddColor(new[] { "珊瑚", "コーラル" }, 255, 127, 80);
            AddColor(new[] { "サーモンピンク" }, 255, 145, 164);
            AddColor(new[] { "撫子", "なでしこ" }, 238, 187, 204);
            AddColor(new[] { "マゼンタ", "MAGENTA" }, 255, 0, 255);
            AddColor(new[] { "牡丹", "ぼたん" }, 211, 47, 127);
            AddColor(new[] { "つつじ" }, 233, 82, 149);

            // 橙・茶系
            AddColor(new[] { "橙", "だいだい", "オレンジ", "ORANGE", "orange" }, 255, 165, 0);
            AddColor(new[] { "柿", "かき" }, 237, 109, 53);
            AddColor(new[] { "杏", "あんず", "アプリコット" }, 247, 185, 119);
            AddColor(new[] { "蜜柑", "みかん", "マンダリン" }, 245, 130, 32);
            AddColor(new[] { "茶", "ちゃ", "ブラウン", "BROWN", "brown" }, 165, 42, 42);
            AddColor(new[] { "焦茶", "こげちゃ" }, 107, 68, 35);
            AddColor(new[] { "栗", "マロン" }, 118, 47, 7);
            AddColor(new[] { "チョコレート", "チョコ" }, 58, 36, 33);
            AddColor(new[] { "コーヒー" }, 75, 54, 33);
            AddColor(new[] { "駱駝", "キャメル" }, 193, 154, 107);
            AddColor(new[] { "ベージュ", "肌" }, 245, 245, 220);
            AddColor(new[] { "黄土", "オーカー" }, 195, 145, 67);
            AddColor(new[] { "琥珀", "アンバー" }, 255, 191, 0);
            AddColor(new[] { "セピア" }, 112, 66, 20);
            AddColor(new[] { "煉瓦", "レンガ", "ブリック" }, 181, 82, 47);
            AddColor(new[] { "鳶", "とび" }, 149, 72, 63);

            // 黄・金系
            AddColor(new[] { "黄", "き", "イエロー", "YELLOW", "yellow" }, 255, 255, 0);
            AddColor(new[] { "山吹", "やまぶき" }, 248, 181, 0);
            AddColor(new[] { "金", "きん", "ゴールド", "GOLD" }, 255, 215, 0);
            AddColor(new[] { "レモン" }, 255, 243, 82);
            AddColor(new[] { "クリーム" }, 255, 253, 208);
            AddColor(new[] { "象牙", "アイボリー" }, 255, 255, 240);
            AddColor(new[] { "向日葵", "ひまわり" }, 255, 219, 0);
            AddColor(new[] { "芥子", "からし", "マスタード" }, 208, 176, 54);
            AddColor(new[] { "ウコン", "ターメリック" }, 250, 186, 12);
            AddColor(new[] { "カナリア" }, 229, 216, 92);

            // 緑・黄緑系
            AddColor(new[] { "緑", "みどり", "グリーン", "GREEN", "green" }, 0, 128, 0);
            AddColor(new[] { "黄緑", "きみどり", "ライム" }, 50, 205, 50);
            AddColor(new[] { "深緑", "ふかみどり" }, 0, 85, 46);
            AddColor(new[] { "抹茶", "まっちゃ" }, 197, 197, 106);
            AddColor(new[] { "鶯", "うぐいす" }, 146, 139, 58);
            AddColor(new[] { "若草", "わかくさ" }, 195, 216, 37);
            AddColor(new[] { "萌黄", "もえぎ" }, 167, 189, 0);
            AddColor(new[] { "苔", "モスグリーン" }, 119, 150, 86);
            AddColor(new[] { "オリーブ" }, 128, 128, 0);
            AddColor(new[] { "エメラルド" }, 80, 200, 120);
            AddColor(new[] { "翡翠", "ひすい", "ジェイド" }, 56, 176, 137);
            AddColor(new[] { "常盤", "ときわ" }, 0, 123, 67);
            AddColor(new[] { "ビリジアン" }, 0, 125, 101);
            AddColor(new[] { "フォレスト" }, 34, 139, 34);
            AddColor(new[] { "ミント" }, 189, 252, 201);
            AddColor(new[] { "海松", "みる" }, 114, 109, 66);
            AddColor(new[] { "青磁", "せいじ" }, 126, 190, 171);

            // 青・水色系
            AddColor(new[] { "青", "あお", "アオ", "BLUE", "blue" }, 0, 0, 255);
            AddColor(new[] { "水", "みず", "ライトブルー" }, 173, 216, 230);
            AddColor(new[] { "シアン", "CYAN" }, 0, 255, 255);
            AddColor(new[] { "空", "そら", "スカイブルー" }, 135, 206, 235);
            AddColor(new[] { "紺", "こん", "ネイビー", "NAVY" }, 0, 0, 128);
            AddColor(new[] { "藍", "あい", "インディゴ" }, 75, 0, 130);
            AddColor(new[] { "群青", "ぐんじょう", "ウルトラマリン" }, 70, 70, 175);
            AddColor(new[] { "瑠璃", "るり", "ラピスラズリ" }, 31, 71, 136);
            AddColor(new[] { "浅葱", "あさぎ" }, 0, 163, 175);
            AddColor(new[] { "新橋", "しんばし" }, 89, 185, 198);
            AddColor(new[] { "ターコイズ", "トルコ石" }, 64, 224, 208);
            AddColor(new[] { "アクアマリン" }, 127, 255, 212);
            AddColor(new[] { "ロイヤルブルー" }, 65, 105, 225);
            AddColor(new[] { "ミッドナイトブルー" }, 25, 25, 112);
            AddColor(new[] { "サックス" }, 75, 144, 194);
            AddColor(new[] { "鉄紺", "てつこん" }, 23, 27, 38);

            // 紫・菫系
            AddColor(new[] { "紫", "むらさき", "パープル", "PURPLE", "purple" }, 128, 0, 128);
            AddColor(new[] { "菫", "すみれ", "バイオレット" }, 238, 130, 238);
            AddColor(new[] { "藤", "ふじ", "ウィステリア" }, 187, 188, 222);
            AddColor(new[] { "菖蒲", "あやめ", "アイリス" }, 204, 125, 182);
            AddColor(new[] { "桔梗", "ききょう" }, 104, 72, 169);
            AddColor(new[] { "ラベンダー" }, 230, 230, 250);
            AddColor(new[] { "ライラック" }, 200, 162, 200);
            AddColor(new[] { "江戸紫" }, 116, 83, 153);
            AddColor(new[] { "古代紫" }, 137, 91, 138);
            AddColor(new[] { "京紫" }, 157, 94, 135);
            AddColor(new[] { "葡萄", "ぶどう", "グレープ" }, 106, 75, 106);
            AddColor(new[] { "オーキッド", "蘭" }, 218, 112, 214);
            AddColor(new[] { "プラム" }, 221, 160, 221);

            // 白・黒・灰系
            AddColor(new[] { "白", "しろ", "ホワイト", "WHITE", "white" }, 255, 255, 255);
            AddColor(new[] { "黒", "くろ", "ブラック", "BLACK", "black" }, 0, 0, 0);
            AddColor(new[] { "灰", "はい", "グレー", "グレイ", "GRAY", "gray" }, 128, 128, 128);
            AddColor(new[] { "鼠", "ねずみ", "マウスグレー" }, 148, 148, 148);
            AddColor(new[] { "銀", "ぎん", "シルバー", "SILVER" }, 192, 192, 192);
            AddColor(new[] { "墨", "すみ" }, 89, 88, 87);
            AddColor(new[] { "鉛", "なまり" }, 119, 120, 123);
            AddColor(new[] { "木炭", "チャコール" }, 54, 69, 79);
            AddColor(new[] { "スレート" }, 112, 128, 144);
            AddColor(new[] { "利休鼠" }, 136, 142, 126);
            AddColor(new[] { "深川鼠" }, 133, 169, 174);
            AddColor(new[] { "鳩羽鼠" }, 158, 143, 150);

            // その他
            AddColor(new[] { "虹", "にじ", "レインボー" }, 255, 255, 255);
            Add("透明", 255, 255, 255);
        }

        #endregion
    }
}