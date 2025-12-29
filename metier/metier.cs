#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace eep.editer1
{
    public partial class Metier : Form
    {
        private readonly CursorPhysics _physics;
        private readonly CursorRenderer _renderer;
        private readonly CursorInputState _inputState;
        private readonly TextStyler _textStyler;
        private readonly FileManager _fileManager;
        private readonly Stopwatch _stopwatch;

        // --- Settings ---
        private const int TIMER_INTERVAL_MS = 6;
        private const float BASE_INTERVAL_MS = 10.0f;
        private const float CLICK_ANIMATION_MS = 260.0f;

        private const long PHYSICS_TIMEOUT_MS = 200;
        private const long BLINK_TIMEOUT_MS = 400;
        private const float MAX_ELAPSED_MS = 100.0f;
        private const float RATCHET_THRESHOLD_MULTIPLIER = 3.0f;
        private const float Y_SNAP_THRESHOLD = 50.0f;

        // --- State ---
        private float _lastInputBaseLine = -1f;
        private bool _isLineSignificant = false;
        private int _currentLineIndex = -1;
        private int _lastSelectionStart = 0;

        public Metier()
        {
            InitializeComponent();
            NativeMethods.timeBeginPeriod(1);

            _stopwatch = new Stopwatch();
            _physics = new CursorPhysics();
            _physics.AnimationDuration = CLICK_ANIMATION_MS;

            _inputState = new CursorInputState();
            _renderer = new CursorRenderer(cursorBox);
            _textStyler = new TextStyler(richTextBox1);
            _fileManager = new FileManager(richTextBox1);

            InitializeForm();
            InitializeRichTextBox();
            InitializeTimer();

            _fileManager.AutoLoad();
            _stopwatch.Start();
            timer1.Start();
        }

        private void InitializeForm()
        {
            Text = "metier";
            FormClosing += (s, e) =>
            {
                _fileManager.AutoSave();
                NativeMethods.timeEndPeriod(1);
            };
        }

        private void InitializeRichTextBox()
        {
            richTextBox1.Text = "";
            richTextBox1.Font = new Font("Yu Gothic UI", 12, FontStyle.Regular);
            richTextBox1.ImeMode = ImeMode.On;
            richTextBox1.AcceptsTab = true;

            richTextBox1.SelectionChanged += (s, e) => { ForceHideSystemCaret(); _renderer.ResetBlink(); };
            richTextBox1.MouseDown += (s, e) => ForceHideSystemCaret();
            richTextBox1.GotFocus += (s, e) => ForceHideSystemCaret();

            richTextBox1.TextChanged += RichTextBox1_TextChanged;
            richTextBox1.KeyDown += RichTextBox1_KeyDown;
            richTextBox1.KeyUp += RichTextBox1_KeyUp;
            richTextBox1.MouseDown += RichTextBox1_MouseDown;
        }

        private void InitializeTimer()
        {
            timer1.Interval = TIMER_INTERVAL_MS;
            timer1.Tick += Timer1_Tick;
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            // 1. 削除操作の検知
            int currentSelStart = richTextBox1.SelectionStart;
            if (currentSelStart < _lastSelectionStart)
            {
                _inputState.RegisterKeyDown(Keys.Back);
            }
            _lastSelectionStart = currentSelStart;

            // 2. 状態取得
            float deltaTime = CalculateDeltaTime();
            var metrics = GetCurrentCursorMetrics();
            var input = GetCurrentInputState();

            InitializeBaseLineIfNeeded(metrics);

            // 3. ターゲット座標決定 & 行移動検知
            int currentLineIdx = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
            int calculatedBottom = metrics.RawPosition.Y + metrics.Height;

            if (currentLineIdx != _currentLineIndex)
            {
                // 行が変わった場合：ベースライン更新 & アニメーション開始
                _lastInputBaseLine = calculatedBottom;
                _currentLineIndex = currentLineIdx;

                Point jumpTarget = new Point(metrics.RawPosition.X, (int)_lastInputBaseLine);
                // 改行アニメーション開始（isLineJump = true）
                _physics.StartAnimation(jumpTarget, isLineJump: true);
            }
            else
            {
                // 同じ行にいる場合：Sticky Baseline制御
                if (!input.IsComposing && !input.IsTypingForPhysics)
                {
                    _lastInputBaseLine = Math.Max(_lastInputBaseLine, calculatedBottom);
                }
            }

            Point targetPosition = new Point(metrics.RawPosition.X, (int)_lastInputBaseLine);

            // 4. 物理演算更新
            _physics.Update(
                targetPosition,
                input.IsTypingForPhysics,
                input.IsDeleting,
                metrics.RatchetThreshold,
                deltaTime,
                metrics.CharWidth,
                input.IsComposing,
                input.ElapsedSinceInput
            );

            // 5. 描画 (液体の幅と色ブレンドを反映)
            _renderer.Render(
                _physics.PosX,
                _physics.PosY - metrics.Height,
                _physics.CurrentWidth,
                metrics.Height,
                input.IsComposing,
                input.IsTypingForBlink,
                metrics.Color
            );

            ForceHideSystemCaret();
        }

        private void InitializeBaseLineIfNeeded(CursorMetrics m)
        {
            if (_lastInputBaseLine < 0)
            {
                _lastInputBaseLine = m.RawPosition.Y + m.Height;
                _currentLineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
            }
        }

        private void RichTextBox1_TextChanged(object sender, EventArgs e)
        {
            _inputState.RegisterInput();
            ForceHideSystemCaret();

            int lineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
            int start = richTextBox1.GetFirstCharIndexFromLine(lineIndex);
            int end = richTextBox1.GetFirstCharIndexFromLine(lineIndex + 1);
            if (end == -1) end = richTextBox1.TextLength;

            int currentLineLength = end - start;

            if (currentLineLength == 0)
            {
                if (_isLineSignificant)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _textStyler.ResetToNormalFont();
                        _textStyler.ResetColorToBlack();
                    }));
                    _isLineSignificant = false;
                }
            }
            else if (currentLineLength >= 5)
            {
                _isLineSignificant = true;
            }
        }

        private void RichTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            _inputState.RegisterKeyDown(e.KeyCode);
            _renderer.ResetBlink();

            bool isComposing = _inputState.IsImeComposing(richTextBox1.Handle);

            if (e.KeyCode == Keys.Enter && !isComposing)
            {
                _textStyler.ResetToNormalFont();
                _textStyler.ResetColorToBlack();
                _isLineSignificant = false;
            }

            if (e.KeyCode == Keys.Tab && _textStyler.ToggleColor(e.Shift)) e.SuppressKeyPress = true;
        }

        private void RichTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey)
            {
                int headingHeight = _textStyler.HandleShiftKeyUp();
                if (headingHeight > 0)
                {
                    Point p = GetCaretPosition();
                    _lastInputBaseLine = p.Y + headingHeight;
                }
            }

            if (e.KeyCode == Keys.Space)
            {
                if (!_inputState.IsImeComposing(richTextBox1.Handle))
                {
                    _textStyler.HandleSpaceKeyUp();
                }
            }

            ForceHideSystemCaret();
        }

        private void RichTextBox1_MouseDown(object sender, MouseEventArgs e)
        {
            _inputState.RegisterMouseClick();

            // 1. 基本的な位置取得
            int index = richTextBox1.GetCharIndexFromPosition(e.Location);
            Point pt = richTextBox1.GetPositionFromCharIndex(index);

            // 2. ★シンプル補正: 「最後の文字」の右側をクリックした時だけ特別扱いする
            // 他の文字は、右半分をクリックすれば勝手に「次の文字」がindexとして返ってくるので無視してOK
            if (index == richTextBox1.TextLength - 1)
            {
                Point ptEnd = richTextBox1.GetPositionFromCharIndex(index + 1); // 文末の座標
                int charWidth = ptEnd.X - pt.X;

                // クリック位置が最後の文字の「右半分～無限の彼方」なら
                if (e.Location.X > pt.X + (charWidth / 2))
                {
                    index++;      // インデックスを「文末」に進める
                    pt = ptEnd;   // 描画座標も「文末」にする
                }
            }

            // ★重要: 計算した index を即座に内部カーソルに反映させる（これが「勘違い」の原因でした）
            richTextBox1.Select(index, 0);

            // --- 以降は座標変換とアニメーション処理 ---
            pt.X += richTextBox1.Location.X;
            pt.Y += richTextBox1.Location.Y;

            Font clickedFont = richTextBox1.SelectionFont ?? richTextBox1.Font;
            int appliedHeight = clickedFont.Height;

            _currentLineIndex = richTextBox1.GetLineFromCharIndex(index);
            int lineStart = richTextBox1.GetFirstCharIndexFromLine(_currentLineIndex);
            int lineEnd = richTextBox1.GetFirstCharIndexFromLine(_currentLineIndex + 1);
            if (lineEnd == -1) lineEnd = richTextBox1.TextLength;

            // フォントサイズスキャン（ちらつき防止）
            if (clickedFont.Size < 20)
            {
                NativeMethods.SendMessage(richTextBox1.Handle, NativeMethods.WM_SETREDRAW, 0, 0);
                try
                {
                    // indexは既にSelect済みなので、スキャン後に復帰させるだけでOK
                    for (int i = lineStart; i < lineEnd; i++)
                    {
                        richTextBox1.Select(i, 1);
                        if (richTextBox1.SelectionFont != null && richTextBox1.SelectionFont.Size >= 20)
                        {
                            appliedHeight = richTextBox1.SelectionFont.Height;
                            break;
                        }
                    }
                    richTextBox1.Select(index, 0); // 復帰
                }
                finally
                {
                    NativeMethods.SendMessage(richTextBox1.Handle, NativeMethods.WM_SETREDRAW, 1, 0);
                    richTextBox1.Refresh();
                }
            }

            _lastInputBaseLine = pt.Y + appliedHeight;
            _isLineSignificant = (lineEnd - lineStart) >= 5;

            // 前の文字が大きい場合の補正 (行が変わっていない場合のみ)
            if (index > 0)
            {
                int prevCharIndex = index - 1;
                // 同じ行かチェック
                if (richTextBox1.GetLineFromCharIndex(prevCharIndex) == richTextBox1.GetLineFromCharIndex(index))
                {
                    richTextBox1.Select(prevCharIndex, 1);
                    Font prevFont = richTextBox1.SelectionFont;
                    if (prevFont != null && prevFont.Height > appliedHeight)
                    {
                        _lastInputBaseLine = pt.Y + prevFont.Height;
                    }
                    richTextBox1.Select(index, 0); // 復帰
                }
            }

            _physics.StartAnimation(new Point(pt.X, (int)_lastInputBaseLine), isLineJump: false);

            ForceHideSystemCaret();
        }

        private void ForceHideSystemCaret()
        {
            NativeMethods.HideCaret(richTextBox1.Handle);
            NativeMethods.CreateCaret(richTextBox1.Handle, IntPtr.Zero, 0, 0);
        }

        private float CalculateDeltaTime()
        {
            float ms = (float)_stopwatch.Elapsed.TotalMilliseconds;
            _stopwatch.Restart();
            return (ms > MAX_ELAPSED_MS || ms <= 0f) ? 1.0f : ms / BASE_INTERVAL_MS;
        }

        private CursorMetrics GetCurrentCursorMetrics()
        {
            Point p = GetCaretPosition();
            Font f = richTextBox1.SelectionFont ?? richTextBox1.Font;
            return new CursorMetrics { RawPosition = p, Font = f, Height = f.Height, Color = richTextBox1.SelectionColor, CharWidth = TextRenderer.MeasureText("あ", f).Width, RatchetThreshold = f.Size * RATCHET_THRESHOLD_MULTIPLIER };
        }

        private InputStateInfo GetCurrentInputState()
        {
            long el = _inputState.GetMillisecondsSinceLastInput();
            bool comp = _inputState.IsImeComposing(richTextBox1.Handle);
            bool isDeleting = _inputState.IsDeleting();

            bool forcePhysicsByIme = comp && !isDeleting;

            return new InputStateInfo
            {
                IsComposing = comp,
                IsDeleting = isDeleting,
                ElapsedSinceInput = el,
                IsTypingForPhysics = (el < PHYSICS_TIMEOUT_MS) || forcePhysicsByIme,
                IsTypingForBlink = el < BLINK_TIMEOUT_MS
            };
        }

        private Point GetCaretPosition()
        {
            int idx = richTextBox1.SelectionStart;
            Point p = (idx < 0) ? new Point(0, 0) : richTextBox1.GetPositionFromCharIndex(idx);
            p.X += richTextBox1.Location.X; p.Y += richTextBox1.Location.Y;
            return p;
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); ForceHideSystemCaret(); }

        private struct CursorMetrics { public Point RawPosition; public Font Font; public int Height; public Color Color; public float CharWidth; public float RatchetThreshold; }
        private struct InputStateInfo { public bool IsComposing; public bool IsDeleting; public long ElapsedSinceInput; public bool IsTypingForPhysics; public bool IsTypingForBlink; }
    }
}