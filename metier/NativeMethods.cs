#nullable disable
using System;
using System.Runtime.InteropServices;

namespace eep.editer1
{
    public static class NativeMethods
    {
        // --- IME関連 ---
        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        public static extern int ImmGetCompositionString(IntPtr hIMC, int dwIndex, byte[] lpBuf, int dwBufLen);

        public const int GCS_COMPSTR = 0x0008;

        // --- キャレット（カーソル）制御関連 ---
        [DllImport("user32.dll")]
        public static extern bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, int nWidth, int nHeight);

        [DllImport("user32.dll")]
        public static extern bool HideCaret(IntPtr hWnd);

        // --- ウィンドウデザイン（アクセントカラー）関連 ---
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmGetColorizationColor(out int pcrColorization, out bool pfOpaqueBlend);

        // --- タイマー精度変更用 ---
        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        public static extern uint timeEndPeriod(uint uPeriod);

        // --- ★追加: 描画停止/再開用 (WM_SETREDRAW) ---
        public const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);
    }
}