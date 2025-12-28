#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace eep.editer1
{
    public class CursorRenderer
    {
        private readonly PictureBox _cursorBox;

        private int _blinkTimer = 0;
        private const int BLINK_INTERVAL = 88;

        public CursorRenderer(PictureBox cursorBox)
        {
            _cursorBox = cursorBox;
            InitializeStyle();
        }

        private void InitializeStyle()
        {
            if (_cursorBox != null)
            {
                _cursorBox.BackColor = Color.Black;
                _cursorBox.Width = 2;
                _cursorBox.Visible = true;
                _cursorBox.BringToFront();
            }
        }

        public void ResetBlink()
        {
            _blinkTimer = 0;
        }

        public void Render(float x, float y, float width, int height, bool isImeComposing, bool isTyping, Color currentColor)
        {
            if (_cursorBox == null) return;

            _cursorBox.Location = new Point((int)x, (int)y);
            _cursorBox.Height = height;

            int pixelWidth = (int)Math.Round(width);
            if (pixelWidth < 1) pixelWidth = 1;

            _cursorBox.Width = pixelWidth;

            // 色の薄め処理
            Color displayColor = GetThinnedColor(currentColor, width);
            _cursorBox.BackColor = displayColor;

            if (isImeComposing || isTyping)
            {
                _cursorBox.Visible = true;
                _blinkTimer = 0;
            }
            else
            {
                _blinkTimer++;
                bool isVisible = (_blinkTimer % (BLINK_INTERVAL * 2)) < BLINK_INTERVAL;
                _cursorBox.Visible = isVisible;
            }

            _cursorBox.BringToFront();
        }

        private Color GetThinnedColor(Color baseColor, float width)
        {
            const float BASE_WIDTH = 2.0f;
            float expansion = width - BASE_WIDTH;

            if (expansion <= 0) return baseColor;

            // ★修正: 減衰係数を大幅に強化 (0.15 -> 0.2)
            // これにより、わずか数ピクセル広がっただけで色が急速に薄くなります。
            // まさに「よく見るとある」レベルの隠し味になります。
            float intensity = 1.0f / (1.0f + expansion * 0.3f);

            // 白背景とのブレンド
            int r = (int)(baseColor.R * intensity + 255 * (1 - intensity));
            int g = (int)(baseColor.G * intensity + 255 * (1 - intensity));
            int b = (int)(baseColor.B * intensity + 255 * (1 - intensity));

            return Color.FromArgb(255, r, g, b);
        }
    }
}