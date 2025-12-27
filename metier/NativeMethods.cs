#nullable disable
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace eep.editer1
{
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern int ShowCursor(bool bShow);
        [DllImport("user32.dll")]
        public static extern bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, int nWidth, int nHeight);
        [DllImport("user32.dll")]
        public static extern bool HideCaret(IntPtr hWnd);

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        public static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        public static extern int ImmGetCompositionString(IntPtr hIMC, int dwIndex, StringBuilder lpBuf, int dwBufLen);

        public const int GCS_COMPSTR = 0x0008;

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmGetColorizationColor(out int pcrColorization, out bool pfOpaqueBlend);


        private const int WM_USER = 0x0400;
        public const int EM_GETSCROLLPOS = WM_USER + 221;
        public const int EM_SETSCROLLPOS = WM_USER + 222;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, ref Point lParam);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool GetScrollRange(IntPtr hWnd, int nBar, out int lpMinPos, out int lpMaxPos);

        public const int SB_VERT = 1;

        // 現在のスクロール位置（ピクセル）を取得
        public static Point GetScrollPos(IntPtr hWnd)
        {
            Point pt = new Point();
            SendMessage(hWnd, EM_GETSCROLLPOS, IntPtr.Zero, ref pt);
            return pt;
        }

        // スクロール位置（ピクセル）をセット
        public static void SetScrollPos(IntPtr hWnd, Point pt)
        {
            SendMessage(hWnd, EM_SETSCROLLPOS, IntPtr.Zero, ref pt);
        }

        // 縦方向の最大スクロール量を取得
        public static int GetScrollMaxY(IntPtr hWnd)
        {
            int min, max;
            GetScrollRange(hWnd, SB_VERT, out min, out max);
            return max;
        }
    }
}