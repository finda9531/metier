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

        public TextStyler(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
            _colorDefinitions = new List<(Color, string[])>
            {
                (Color.Red, new[] { "red", "赤色", "あか", "赤" }),
                (Color.Green, new[] { "green", "緑色", "みどり", "緑" })
            };
        }

        public void HandleShiftKeyUp()
        {
            long now = DateTime.Now.Ticks / 10000;
            if (now - _lastShiftReleaseTime < DOUBLE_TAP_SPEED)
            {
                ToggleHeading();
                _lastShiftReleaseTime = 0;
            }
            else _lastShiftReleaseTime = now;
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
            else
            {
                _lastSpaceReleaseTime = now;
            }
        }

        private void ToggleHeading()
        {
            Font currentFont = _richTextBox.SelectionFont;
            bool isHeading = (currentFont != null && currentFont.Size >= 20);
            if (isHeading) _richTextBox.SelectionFont = new Font("Meiryo UI", 14, FontStyle.Regular);
            else _richTextBox.SelectionFont = new Font("Meiryo UI", 24, FontStyle.Bold);
            _richTextBox.Focus();
        }

        public void ResetToNormalFont()
        {
            Font currentFont = _richTextBox.SelectionFont;
            if (currentFont != null && currentFont.Size >= 20)
            {
                _richTextBox.SelectionFont = new Font("Meiryo UI", 14, FontStyle.Regular);
            }
        }

        public void ResetColorToBlack()
        {
            _richTextBox.SelectionColor = Color.Black;
        }

        // ▼▼▼ 修正: 範囲を「スペース」または「色変わり」までの一塊に限定する ▼▼▼
        public bool ToggleColor(bool keepTriggerWord)
        {
            int caretPos = _richTextBox.SelectionStart;
            int startPos = caretPos;

            // 1. カーソル位置から後ろ向きに走査して「一塊」の開始位置を探す
            //    (スペース、改行、または「黒以外の文字」に当たったらストップ)
            for (int i = caretPos - 1; i >= 0; i--)
            {
                char c = _richTextBox.Text[i];

                // 条件A: 空白文字ならそこで区切る
                if (char.IsWhiteSpace(c))
                {
                    startPos = i + 1;
                    break;
                }

                // 条件B: すでに色が黒以外ならそこで区切る (既存の色を守るため)
                _richTextBox.Select(i, 1);
                if (_richTextBox.SelectionColor.ToArgb() != Color.Black.ToArgb())
                {
                    startPos = i + 1;
                    break;
                }

                if (i == 0) startPos = 0;
            }

            // 選択範囲などを計算のために一旦戻す
            _richTextBox.Select(caretPos, 0);

            // 対象となるテキスト塊を取得
            if (startPos >= caretPos) return false;
            string chunkText = _richTextBox.Text.Substring(startPos, caretPos - startPos);

            // 2. キーワードチェック
            var definition = _colorDefinitions.FirstOrDefault(d => d.Keywords.Any(k => chunkText.EndsWith(k)));

            if (definition.Keywords != null)
            {
                string foundKeyword = definition.Keywords.First(k => chunkText.EndsWith(k));
                Color targetColor = definition.Color;

                // 色を変える範囲の長さを計算
                int modifyLength = chunkText.Length;

                if (!keepTriggerWord)
                {
                    // キーワード部分("red"など)を削除
                    _richTextBox.Select(startPos + modifyLength - foundKeyword.Length, foundKeyword.Length);
                    _richTextBox.SelectedText = "";
                    modifyLength -= foundKeyword.Length;
                }

                // 残りの部分に色を適用
                if (modifyLength > 0)
                {
                    _richTextBox.Select(startPos, modifyLength);

                    // トグル動作: すでにその色なら黒に戻す、違えばその色にする
                    bool isAlreadyTargetColor = (_richTextBox.SelectionColor.ToArgb() == targetColor.ToArgb());
                    _richTextBox.SelectionColor = isAlreadyTargetColor ? Color.Black : targetColor;
                }

                // 最後にカーソル位置を調整し、次の入力文字を黒に戻しておく
                _richTextBox.Select(startPos + modifyLength, 0);
                _richTextBox.SelectionColor = Color.Black;
                _richTextBox.Focus();

                return true;
            }

            return false;
        }
    }
}