#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace eep.editer1
{
    public class CursorRenderer
    {
        private readonly PictureBox _cursorBox;

        // アクセントカラー保持用 (初期値は黒)
        private Color _systemAccentColor = Color.Black;

        private int _blinkTimer = 0;
        private const int BLINK_INTERVAL = 88;

        public CursorRenderer(PictureBox cursorBox)
        {
            _cursorBox = cursorBox;
            InitializeStyle();
            GetSystemAccentColor(); // 起動時に色を取得
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

        // Windowsのアクセントカラーを取得するメソッド
        private void GetSystemAccentColor()
        {
            try
            {
                int colorRaw;
                bool opaque;
                // WindowsのAPIから現在のテーマ色を取得
                NativeMethods.DwmGetColorizationColor(out colorRaw, out opaque);

                // アルファチャンネル(透明度)を255(不透明)に強制して色を作成
                _systemAccentColor = Color.FromArgb(255, Color.FromArgb(colorRaw));
            }
            catch
            {
                // 取得に失敗した場合は、安全策として「黒」にする（青にはしない）
                _systemAccentColor = Color.Black;
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

            // --- 色と太さの決定 ---
            Color targetColor;
            float targetWidth = width;

            if (isImeComposing)
            {
                // IME入力中: アクセントカラーを使い、少し太くする(5px)
                // もし物理演算で5px以上に膨らんでいたら、太い方を採用する
                targetColor = _systemAccentColor;
                if (targetWidth < 5.0f) targetWidth = 5.0f;
            }
            else
            {
                // 通常時: 現在の文字色(黒)を使用
                targetColor = currentColor;
            }

            // 幅の適用
            int pixelWidth = (int)Math.Round(targetWidth);
            if (pixelWidth < 1) pixelWidth = 1;
            _cursorBox.Width = pixelWidth;

            // 色の薄め処理 (アクセントカラーの場合も、移動時は少し薄くして液体感を出す)
            Color displayColor = GetThinnedColor(targetColor, targetWidth);
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

            // IME入力中で太さが固定されている(5px)場合、
            // その太さを「基準」とみなして、そこからさらに広がった時だけ薄くする
            float effectiveBase = (baseColor == _systemAccentColor) ? 5.0f : BASE_WIDTH;

            float expansion = width - effectiveBase;
            if (expansion <= 0) return baseColor;

            // 減衰計算
            float intensity = 1.0f / (1.0f + expansion * 0.25f);

            // 白背景とのブレンド
            int r = (int)(baseColor.R * intensity + 255 * (1 - intensity));
            int g = (int)(baseColor.G * intensity + 255 * (1 - intensity));
            int b = (int)(baseColor.B * intensity + 255 * (1 - intensity));

            return Color.FromArgb(255, r, g, b);
        }
    }
}