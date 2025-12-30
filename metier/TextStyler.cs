#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Metier
{
    public class TextStyler(RichTextBox richTextBox)
    {
        private readonly RichTextBox _richTextBox = richTextBox;
        private readonly ColorRepository _colorRepo = new();

        private long _lastShiftReleaseTime = 0;
        private long _lastSpaceReleaseTime = 0;

        // “直前に色を変えた範囲” を記憶
        private (int Start, int Length) _lastColoredRange = (-1, 0);

        private const int DOUBLE_TAP_SPEED = 400; // 400ミリ秒
        private const string FONT_FAMILY = "Meiryo UI";
        private const float FONT_SIZE_NORMAL = 14;
        private const float FONT_SIZE_HEADING = 24;

        // --- 公開イベントハンドラ ---

        public void OnSelectionChanged()
        {
            // 特殊な判定が必要ない場合は空でも可
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
            int currentPos = _richTextBox.SelectionStart;
            if (currentPos <= 0) return;

            char lastChar = _richTextBox.Text[currentPos - 1];

            // --- 【全角スペース単発】 モード終了 (削除せずスタイルのみリセット) ---
            if (lastChar == '　')
            {
                if (IsHeadingMode() || IsColorMode())
                {
                    ResetSelectionStyle();
                    return;
                }
            }

            // --- 【スペース2連打】 判定 ---
            if (now - _lastSpaceReleaseTime < DOUBLE_TAP_SPEED)
            {
                // ここで「入力されたスペース(全角含む)」を確実に消去する準備
                // 各アクション内で RemoveTrailingSpaces を呼び出す

                // 優先順位1: 直前の色変換取り消し (Undo)
                int expectedEnd = _lastColoredRange.Start + _lastColoredRange.Length;

                // ※ペンモード(Length=0)の場合もUndo対象にするため条件を調整
                bool isUndoTarget = (_lastColoredRange.Start >= 0) &&
                                    (currentPos >= expectedEnd && currentPos <= expectedEnd + 2);

                if (isUndoTarget)
                {
                    UndoColorChange(currentPos, expectedEnd);
                    _lastSpaceReleaseTime = 0;
                    return;
                }

                // 優先順位2: 見出し・色モード終了
                if (IsHeadingMode() || IsColorMode())
                {
                    // 全角・半角問わずスペースを削除してリセット
                    RemoveTrailingSpaces(currentPos);
                    ResetSelectionStyle();
                    _lastSpaceReleaseTime = 0;
                    return;
                }

                // 優先順位3: カッコ抜け (脱出)
                if (TryStepOutBrackets(currentPos))
                {
                    _lastSpaceReleaseTime = 0;
                    return;
                }
            }
            else _lastSpaceReleaseTime = now;
        }

        // --- 内部判定・処理用ヘルパー ---

        private bool IsHeadingMode() => (_richTextBox.SelectionFont?.Size ?? 0) >= 20;
        private bool IsColorMode() => _richTextBox.SelectionColor.ToArgb() != Color.Black.ToArgb();

        private void UndoColorChange(int currentPos, int expectedEnd)
        {
            // 1. 入力されたスペース（連打分）を削除
            // currentPos はスペース入力後の位置なので、そこから expectedEnd までの差分を消す
            int spacesToRemove = currentPos - expectedEnd;
            if (spacesToRemove > 0)
            {
                _richTextBox.Select(expectedEnd, spacesToRemove);
                _richTextBox.SelectedText = "";
            }

            // 2. 色の復元 / リセット
            if (_lastColoredRange.Length > 0)
            {
                // 「範囲」につけた色を黒に戻す
                _richTextBox.Select(_lastColoredRange.Start, _lastColoredRange.Length);
                _richTextBox.SelectionColor = Color.Black;

                // カーソルを末尾に戻し、以降も黒にする
                _richTextBox.Select(_lastColoredRange.Start + _lastColoredRange.Length, 0);
                _richTextBox.SelectionColor = Color.Black;
            }
            else
            {
                // 「ペンモード（行頭設定）」の取り消しの場合
                // カーソル位置の色を黒に戻すだけ
                _richTextBox.Select(_lastColoredRange.Start, 0);
                _richTextBox.SelectionColor = Color.Black;
            }

            _lastColoredRange = (-1, 0);
        }

        private void RemoveTrailingSpaces(int currentPos)
        {
            // カーソル直前の空白を最大2文字探して消す
            // char.IsWhiteSpace は 全角スペース('　') も true を返すため、全角も消える
            int count = 0;
            for (int i = 1; i <= 2; i++)
            {
                if (currentPos - i >= 0 && char.IsWhiteSpace(_richTextBox.Text[currentPos - i]))
                    count = i;
                else break;
            }
            if (count > 0)
            {
                _richTextBox.Select(currentPos - count, count);
                _richTextBox.SelectedText = "";
            }
        }

        private bool TryStepOutBrackets(int currentPos)
        {
            if (currentPos >= _richTextBox.TextLength) return false;
            char nextChar = _richTextBox.Text[currentPos];

            string closingBrackets = "」』）)}]”";

            if (closingBrackets.Contains(nextChar))
            {
                // 全角スペースなどが混ざっていても削除
                RemoveTrailingSpaces(currentPos);

                int newPos = _richTextBox.SelectionStart;
                _richTextBox.Select(newPos + 1, 0);
                return true;
            }
            return false;
        }

        // --- 色適用ロジック ---

        public bool ApplyColorLogic(bool keepTriggerWord)
        {
            int caretPos = _richTextBox.SelectionStart;
            if (caretPos == 0) return false;

            int searchStart = GetTriggerChunkStart(caretPos);
            // 行頭の場合 searchStart == caretPos になる可能性があるため条件緩和
            if (searchStart < 0) return false;

            string chunkText = _richTextBox.Text[searchStart..caretPos];
            string matchedKey = null;
            string matchedInput = null;

            foreach (var key in _colorRepo.SortedKeys)
            {
                if (chunkText.EndsWith(key)) { matchedKey = key; matchedInput = key; break; }
                if (chunkText.EndsWith(key + "色")) { matchedKey = key; matchedInput = key + "色"; break; }
            }

            if (matchedKey != null)
            {
                string prefix = chunkText[..^matchedInput.Length];
                Color baseColor = _colorRepo.GetBaseColor(matchedKey);
                Color finalColor = _colorRepo.ApplyModifier(baseColor, prefix, out int modLength);
                if (modLength > 0) matchedInput = string.Concat(prefix.AsSpan(prefix.Length - modLength), matchedInput);

                Color targetColor = (modLength > 0) ? finalColor : _colorRepo.GetClosestCategoryColor(matchedKey);
                ApplyColorToSelection(caretPos, matchedInput, targetColor, keepTriggerWord);
                return true;
            }
            return false;
        }

        private void ApplyColorToSelection(int caretPos, string matchedInput, Color targetColor, bool keepTriggerWord)
        {
            int keywordStartPos = caretPos - matchedInput.Length;
            if (keywordStartPos < 0) keywordStartPos = 0;

            // 1. トリガーワード（「赤」など）を削除
            if (!keepTriggerWord)
            {
                _richTextBox.Select(keywordStartPos, matchedInput.Length);
                _richTextBox.SelectedText = "";
            }

            // 2. 色を適用する範囲を決定
            int rangeStart = GetColorRangeStart(keywordStartPos);
            int modifyLength = keywordStartPos - rangeStart;

            if (modifyLength > 0)
            {
                // 【通常モード】前の文字がある場合：その範囲の色を変える
                _richTextBox.Select(rangeStart, modifyLength);
                _richTextBox.SelectionColor = targetColor;

                _lastColoredRange = (rangeStart, modifyLength);

                // カーソル位置の色指定はリセットしない（続きを黒にするかはEnter等の責務）
                // ただし、範囲外に出たときに色が漏れないよう念のため末尾を選択
                int resetPos = keepTriggerWord ? (keywordStartPos + matchedInput.Length) : keywordStartPos;
                _richTextBox.Select(Math.Min(resetPos, _richTextBox.TextLength), 0);
            }
            else
            {
                // 【ペンモード】前の文字がない（行頭など）：これから書く文字の色を変える
                _richTextBox.Select(keywordStartPos, 0); // 削除後の位置
                _richTextBox.SelectionColor = targetColor;

                // Undo用に記録（長さ0 = ペンモードの印）
                _lastColoredRange = (keywordStartPos, 0);
            }

            _richTextBox.Focus();
        }

        // --- フォント・リセット操作 ---

        private int ApplyHeadingLogic()
        {
            int caretPos = _richTextBox.SelectionStart;
            bool isAfterChar = (caretPos > 0) && !char.IsWhiteSpace(_richTextBox.Text[caretPos - 1]);
            int resultHeight = 0;

            if (isAfterChar)
            {
                int startPos = GetTriggerChunkStart(caretPos);
                int length = caretPos - startPos;
                if (length > 0)
                {
                    _richTextBox.Select(startPos, length);
                    ToggleCurrentSelectionFont();
                    if (_richTextBox.SelectionFont.Size >= 20) resultHeight = _richTextBox.SelectionFont.Height;
                    _richTextBox.Select(caretPos, 0);
                    _richTextBox.SelectionFont = new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular);
                }
            }
            else
            {
                ToggleCurrentSelectionFont();
                resultHeight = _richTextBox.SelectionFont.Height;
            }
            return resultHeight;
        }

        private void ToggleCurrentSelectionFont()
        {
            Font currentFont = _richTextBox.SelectionFont ?? _richTextBox.Font;
            bool isHeading = (currentFont.Size >= 20);
            _richTextBox.SelectionFont = isHeading
                ? new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular)
                : new Font(FONT_FAMILY, FONT_SIZE_HEADING, FontStyle.Bold);
        }

        public void ResetToNormalFont()
        {
            _richTextBox.SelectionFont = new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular);
        }

        public void ResetColorToBlack()
        {
            _richTextBox.SelectionColor = Color.Black;
            _lastColoredRange = (-1, 0);
        }

        public void ResetSelectionStyle()
        {
            ResetToNormalFont();
            ResetColorToBlack();
        }

        private int GetTriggerChunkStart(int caretPos)
        {
            int limit = Math.Max(0, caretPos - 50);
            for (int i = caretPos - 1; i >= limit; i--)
                if (char.IsWhiteSpace(_richTextBox.Text[i])) return i + 1;
            return limit;
        }

        private int GetColorRangeStart(int keywordStartPos)
        {
            int limit = Math.Max(0, keywordStartPos - 200);
            for (int i = keywordStartPos - 1; i >= limit; i--)
            {
                char c = _richTextBox.Text[i];
                if (char.IsWhiteSpace(c) || "。、.,？！?!".Contains(c)) return i + 1;
            }
            return limit;
        }
    }
}