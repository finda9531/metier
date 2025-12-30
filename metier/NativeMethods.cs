#nullable disable
using System;
using System.Runtime.InteropServices;

namespace Metier
{
#pragma warning disable SYSLIB1054 

    internal static class NativeMethods
    {
        // --- IME関連 ---
        [DllImport("imm32.dll")]
        internal static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        internal static extern IntPtr ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        // ★復活: CursorInputState.cs で使用しています
        [DllImport("imm32.dll")]
        internal static extern int ImmGetCompositionString(IntPtr hIMC, int dwIndex, byte[] lpBuf, int dwBufLen);

        // ★復活: CursorInputState.cs で使用しています
        internal const int GCS_COMPSTR = 0x0008;

        // --- キャレット制御関連 ---
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, int nWidth, int nHeight);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HideCaret(IntPtr hWnd);

        // --- ウィンドウデザイン ---
        [DllImport("dwmapi.dll", PreserveSig = false)]
        internal static extern void DwmGetColorizationColor(out int pcrColorization, [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);

        // --- タイマー精度 ---
        [DllImport("winmm.dll")]
        internal static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        internal static extern uint timeEndPeriod(uint uPeriod);

        // --- 描画制御 ---
        internal const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        internal static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);
    }
#pragma warning restore SYSLIB1054
}