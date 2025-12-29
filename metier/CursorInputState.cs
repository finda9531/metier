#nullable disable
using System;
using System.Windows.Forms;

namespace eep.editer1
{
    public class CursorInputState
    {
        private long lastInputTime = 0;

        // 最後のマウスクリック時間を記録する変数
        private long lastMouseClickTime = 0;

        public Keys LastKeyDown { get; private set; } = Keys.None;

        public bool IsImeComposing(IntPtr hWnd)
        {
            IntPtr hIMC = NativeMethods.ImmGetContext(hWnd);
            if (hIMC == IntPtr.Zero) return false;

            try
            {
                int strLen = NativeMethods.ImmGetCompositionString(hIMC, NativeMethods.GCS_COMPSTR, null, 0);
                return (strLen > 0);
            }
            finally
            {
                NativeMethods.ImmReleaseContext(hWnd, hIMC);
            }
        }

        public void RegisterKeyDown(Keys key)
        {
            LastKeyDown = key;
        }

        public void RegisterInput()
        {
            lastInputTime = DateTime.Now.Ticks / 10000;
        }

        // マウスをクリックした時に呼ぶメソッド
        public void RegisterMouseClick()
        {
            lastMouseClickTime = DateTime.Now.Ticks / 10000;
        }

        public long GetMillisecondsSinceLastInput()
        {
            long now = DateTime.Now.Ticks / 10000;
            return now - lastInputTime;
        }

        // 最後のクリックからの経過時間を取得するメソッド
        public long GetMillisecondsSinceLastClick()
        {
            long now = DateTime.Now.Ticks / 10000;
            return now - lastMouseClickTime;
        }

        public bool IsDeleting()
        {
            return (LastKeyDown == Keys.Back || LastKeyDown == Keys.Left);
        }
    }
}