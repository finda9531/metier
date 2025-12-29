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

        // C++の colorDictionary に相当：あらゆる呼び名から代表RGBを引く辞書
        private readonly Dictionary<string, Color> _colorDictionary = new Dictionary<string, Color>();

        // C++の categories に相当：入力RGBに一番近い「標準色」を探すためのリスト
        private readonly List<(string Name, Color Color)> _standardCategories = new List<(string, Color)>();

        private const int DOUBLE_TAP_SPEED = 600;
        private const string FONT_FAMILY = "Meiryo UI";
        private const float FONT_SIZE_NORMAL = 14;
        private const float FONT_SIZE_HEADING = 24;

        public TextStyler(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
            InitializeColorClassifier();
        }

        // --- C++ロジックの移植部分 ---
        private void InitializeColorClassifier()
        {
            // ヘルパー: エイリアス一括登録
            void AddColor(string[] names, int r, int g, int b)
            {
                Color c = Color.FromArgb(r, g, b);
                foreach (var name in names) _colorDictionary[name] = c;
            }
            // ヘルパー: 単一名登録（コード短縮用）
            void Add(string name, int r, int g, int b)
            {
                _colorDictionary[name] = Color.FromArgb(r, g, b);
            }

            // =========================================================
            // 1. 標準カテゴリの定義（判定後の着地点）拡張版
            //    入力された色は、RGB距離が一番近いこのリストの色に変換されます。
            // =========================================================
            _standardCategories.Clear();

            // --- 無彩色 (Mono) ---
            _standardCategories.Add(("BLACK", Color.Black));
            _standardCategories.Add(("DIM_GRAY", Color.DimGray));      // 暗い灰
            _standardCategories.Add(("GRAY", Color.Gray));             // 灰
            _standardCategories.Add(("SILVER", Color.Silver));         // 明るい灰
            _standardCategories.Add(("WHITE", Color.White));

            // --- 赤系 (Red) ---
            _standardCategories.Add(("RED", Color.Red));               // 基本の赤
            _standardCategories.Add(("MAROON", Color.Maroon));         // 暗い赤・栗色
            _standardCategories.Add(("CRIMSON", Color.Crimson));       // 濃い赤
            _standardCategories.Add(("SALMON", Color.Salmon));         // 薄い赤・サーモン

            // --- ピンク系 (Pink) ---
            _standardCategories.Add(("PINK", Color.HotPink));          // 鮮やかなピンク
            _standardCategories.Add(("LIGHT_PINK", Color.Pink));       // 薄いピンク
            _standardCategories.Add(("MAGENTA", Color.Magenta));       // 赤紫・マゼンタ

            // --- 紫系 (Purple) ---
            _standardCategories.Add(("PURPLE", Color.Purple));         // 紫
            _standardCategories.Add(("INDIGO", Color.Indigo));         // 藍色・深い紫
            _standardCategories.Add(("LAVENDER", Color.Lavender));     // 薄紫

            // --- 青系 (Blue) ---
            _standardCategories.Add(("BLUE", Color.Blue));             // 青
            _standardCategories.Add(("NAVY", Color.Navy));             // 紺・濃い青
            _standardCategories.Add(("ROYAL_BLUE", Color.RoyalBlue));  // 鮮やかな青
            _standardCategories.Add(("SKY_BLUE", Color.DeepSkyBlue));  // 空色
            _standardCategories.Add(("CYAN", Color.Cyan));             // 水色・シアン
            _standardCategories.Add(("TEAL", Color.Teal));             // 鴨の羽色・青緑

            // --- 緑系 (Green) ---
            _standardCategories.Add(("GREEN", Color.Green));           // 緑
            _standardCategories.Add(("DARK_GREEN", Color.DarkGreen));  // 深緑
            _standardCategories.Add(("LIME", Color.Lime));             // 蛍光緑
            _standardCategories.Add(("OLIVE", Color.Olive));           // オリーブ・暗い黄緑

            // --- 黄・橙・茶系 (Yellow/Orange/Brown) ---
            _standardCategories.Add(("YELLOW", Color.Gold));           // 黄色 (Yellowだと白背景で見にくいのでGold推奨)
            _standardCategories.Add(("ORANGE", Color.Orange));         // オレンジ
            _standardCategories.Add(("BROWN", Color.SaddleBrown));     // 茶色
            _standardCategories.Add(("BEIGE", Color.Beige));           // 肌色・ベージュ

            // =========================================================
            // 2. 色辞書データ (JIS慣用色名 + Webカラー + 和名)
            //    ここにある言葉＋「色」がヒットするようになります。
            // =========================================================

            // --- 赤・ピンク系 ---
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

            // --- 橙・茶系 ---
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

            // --- 黄・金系 ---
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

            // --- 緑・黄緑系 ---
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

            // --- 青・水色系 ---
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

            // --- 紫・菫系 ---
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

            // --- 白・黒・灰系 ---
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

            // --- その他・特殊 ---
            AddColor(new[] { "虹", "にじ", "レインボー" }, 255, 255, 255); // 特殊処理しない場合は白等にマッピング
            Add("透明", 255, 255, 255); // 白扱い
        }

        // 戦略1：「色」を取り除く正規化関数 (C#版)
        private string NormalizeInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            // C#の文字列はUnicodeなので、バイト列操作ではなく EndsWith で判定可能
            return input.EndsWith("色") ? input.Substring(0, input.Length - 1) : input;
        }

        // 最も近い色カテゴリを特定するロジック (IdentifyCategory相当)
        private Color IdentifyCategoryColor(string inputWord)
        {
            string key = NormalizeInput(inputWord);

            // 1. 辞書検索
            if (!_colorDictionary.TryGetValue(key, out Color hitColor))
            {
                // 正規化前（元の言葉）でも一応探してみる
                if (!_colorDictionary.TryGetValue(inputWord, out hitColor))
                {
                    return Color.Black; // 未知の言葉は黒
                }
            }

            // 2. 一番近いカテゴリを探す (RGBのユークリッド距離の二乗)
            Color bestColor = Color.Black;
            double minDistance = double.MaxValue;

            foreach (var cat in _standardCategories)
            {
                // R, G, B それぞれの差の二乗和を計算
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

        // --- 既存機能の修正 ---
        public bool ToggleColor(bool keepTriggerWord)
        {
            int caretPos = _richTextBox.SelectionStart;
            int startPos = GetChunkStartPosition(caretPos);

            if (startPos >= caretPos) return false;
            string chunkText = _richTextBox.Text.Substring(startPos, caretPos - startPos);

            string matchedKey = null;
            string matchedInput = null;

            // ★修正ポイント: キーワードを「長い順」に並び替えてからチェックする
            // これにより、「緑」よりも先に「黄緑」や「深緑」がヒットするようになります。
            var sortedKeys = _colorDictionary.Keys.OrderByDescending(k => k.Length);

            foreach (var key in sortedKeys)
            {
                // 1. キーワードそのもので終わっているか ("黄緑")
                if (chunkText.EndsWith(key))
                {
                    matchedKey = key;
                    matchedInput = key;
                    break; // 最長一致したらループ終了
                }
                // 2. キーワード + "色" で終わっているか ("黄緑色")
                if (chunkText.EndsWith(key + "色"))
                {
                    matchedKey = key;
                    matchedInput = key + "色";
                    break; // 最長一致したらループ終了
                }
            }

            if (matchedKey != null)
            {
                // 色を判定（標準カテゴリへの吸着）
                Color targetColor = IdentifyCategoryColor(matchedInput);

                int modifyLength = chunkText.Length;

                if (!keepTriggerWord)
                {
                    // 見つかったキーワード部分（"黄緑" または "黄緑色"）を削除
                    _richTextBox.Select(startPos + modifyLength - matchedInput.Length, matchedInput.Length);
                    _richTextBox.SelectedText = "";
                    modifyLength -= matchedInput.Length;
                }

                if (modifyLength > 0)
                {
                    // 残りの部分（あれば）の色を変更
                    _richTextBox.Select(startPos, modifyLength);

                    bool isAlreadyTargetColor = (_richTextBox.SelectionColor.ToArgb() == targetColor.ToArgb());
                    _richTextBox.SelectionColor = isAlreadyTargetColor ? Color.Black : targetColor;
                }

                // 次の入力に備えてリセット
                _richTextBox.Select(startPos + modifyLength, 0);
                _richTextBox.SelectionColor = Color.Black;
                _richTextBox.Focus();
                return true;
            }

            return false;
        }

        // --- 以下、既存のコード維持 ---

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

        private int ApplyHeadingLogic()
        {
            int caretPos = _richTextBox.SelectionStart;
            bool isAfterCharacter = caretPos > 0 && !char.IsWhiteSpace(_richTextBox.Text[caretPos - 1]);
            int resultHeight = 0;

            if (isAfterCharacter)
            {
                int startPos = GetChunkStartPosition(caretPos);
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

        public void HandleSpaceKeyUp()
        {
            long now = DateTime.Now.Ticks / 10000;
            if (now - _lastSpaceReleaseTime < DOUBLE_TAP_SPEED)
            {
                int currentPos = _richTextBox.SelectionStart;
                if (currentPos >= 2)
                {
                    _richTextBox.Select(currentPos - 2, 2);
                    ResetColorToBlack();
                    ResetToNormalFont();
                    _richTextBox.SelectedText = " ";
                    ResetColorToBlack();
                    ResetToNormalFont();
                }
                _lastSpaceReleaseTime = 0;
            }
            else _lastSpaceReleaseTime = now;
        }

        private int GetChunkStartPosition(int caretPos)
        {
            int startPos = caretPos;
            for (int i = caretPos - 1; i >= 0; i--)
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

        public void ResetToNormalFont()
        {
            Font currentFont = _richTextBox.SelectionFont;
            if (currentFont != null && currentFont.Size >= 20)
            {
                _richTextBox.SelectionFont = new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular);
            }
        }

        public void ResetColorToBlack()
        {
            _richTextBox.SelectionColor = Color.Black;
        }
    }
}