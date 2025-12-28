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

        private const int DOUBLE_TAP_SPEED = 600;
        private readonly List<(Color Color, string[] Keywords)> _colorDefinitions;

        private const string FONT_FAMILY = "Meiryo UI";
        private const float FONT_SIZE_NORMAL = 14;
        private const float FONT_SIZE_HEADING = 24;

        public TextStyler(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
            _colorDefinitions = new List<(Color, string[])>
            {
                (Color.Red, new[] { "red", "赤色", "あか", "赤" }),
                (Color.Green, new[] { "green", "緑色", "みどり", "緑" })
            };
        }

        public int HandleShiftKeyUp()
        {
            long now = DateTime.Now.Ticks / 10000;
            int appliedHeight = 0;

            if (now - _lastShiftReleaseTime < DOUBLE_TAP_SPEED)
            {
                appliedHeight = ApplyHeadingLogic(); // 高さを受け取る
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
                // 直前の塊を見出しにする
                int startPos = GetChunkStartPosition(caretPos);
                int length = caretPos - startPos;

                _richTextBox.Select(startPos, length);

                // ★変更: トグルではなく「強制的に大きくする」
                Font newFont = EnlargeCurrentSelectionFont();

                // 見出し化（大きい文字）になった場合、その高さを記録
                if (newFont.Size >= 20)
                {
                    resultHeight = newFont.Height;
                }

                // 選択解除して末尾へ。入力用フォントは標準に戻す
                _richTextBox.Select(caretPos, 0);

                // ここで入力用フォントを標準に戻すかは議論の余地ありだが、
                // 「見出しを書いた後は本文」という流れなら戻すのが自然。
                _richTextBox.SelectionFont = new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular);
            }
            else
            {
                // これから書く文字のサイズを大きくする
                Font newFont = EnlargeCurrentSelectionFont();
                resultHeight = newFont.Height;
            }

            _richTextBox.Focus();
            return resultHeight;
        }

        // ★変更: 名前を変更し、ロジックを「大きくするだけ」に変更
        private Font EnlargeCurrentSelectionFont()
        {
            // 既に大きくても、改めて大きいフォントを設定する（副作用はないため安全）
            Font newFont = new Font(FONT_FAMILY, FONT_SIZE_HEADING, FontStyle.Bold);
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
                    // スペース2回押しでリセット機能
                    _richTextBox.Select(currentPos - 2, 2);

                    // ★ここが「戻す」役割になる
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

        // ... 以下、既存メソッドは変更なし ...
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

        public bool ToggleColor(bool keepTriggerWord)
        {
            int caretPos = _richTextBox.SelectionStart;
            int startPos = GetChunkStartPosition(caretPos);

            if (startPos >= caretPos) return false;
            string chunkText = _richTextBox.Text.Substring(startPos, caretPos - startPos);

            var definition = _colorDefinitions.FirstOrDefault(d => d.Keywords.Any(k => chunkText.EndsWith(k)));

            if (definition.Keywords != null)
            {
                string foundKeyword = definition.Keywords.First(k => chunkText.EndsWith(k));
                Color targetColor = definition.Color;
                int modifyLength = chunkText.Length;

                if (!keepTriggerWord)
                {
                    _richTextBox.Select(startPos + modifyLength - foundKeyword.Length, foundKeyword.Length);
                    _richTextBox.SelectedText = "";
                    modifyLength -= foundKeyword.Length;
                }

                if (modifyLength > 0)
                {
                    _richTextBox.Select(startPos, modifyLength);
                    bool isAlreadyTargetColor = (_richTextBox.SelectionColor.ToArgb() == targetColor.ToArgb());
                    _richTextBox.SelectionColor = isAlreadyTargetColor ? Color.Black : targetColor;
                }

                _richTextBox.Select(startPos + modifyLength, 0);
                _richTextBox.SelectionColor = Color.Black;
                _richTextBox.Focus();
                return true;
            }
            return false;
        }

        public void ResetToNormalFont()
        {
            // 無条件で標準に戻す
            _richTextBox.SelectionFont = new Font(FONT_FAMILY, FONT_SIZE_NORMAL, FontStyle.Regular);
        }

        public void ResetColorToBlack()
        {
            _richTextBox.SelectionColor = Color.Black;
        }
    }
}