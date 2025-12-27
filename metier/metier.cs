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

        private const float BASE_INTERVAL_MS = 10.0f;
        private const long PHYSICS_TIMEOUT_MS = 200;
        private const long BLINK_TIMEOUT_MS = 400;
        private const float MAX_ELAPSED_MS = 100.0f;
        private const float RATCHET_THRESHOLD_MULTIPLIER = 3.0f;
        private const int TIMER_INTERVAL_MS = 10;
        private const float Y_SNAP_THRESHOLD = 50.0f;

        private float _lastInputBaseLine = -1f;
        private bool _isLineSignificant = false;

        // ★追加: 現在の行番号を記録して、行移動を検知する
        private int _currentLineIndex = -1;

        public Metier()
        {
            InitializeComponent();
            _stopwatch = new Stopwatch();
            _physics = new CursorPhysics();
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
            FormClosing += (s, e) => _fileManager.AutoSave();
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
            float deltaTime = CalculateDeltaTime();
            var metrics = GetCurrentCursorMetrics();
            var input = GetCurrentInputState();

            // 基準線が未初期化なら初期化
            if (_lastInputBaseLine < 0)
            {
                _lastInputBaseLine = metrics.RawPosition.Y + metrics.Height;
                _currentLineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
            }

            Point targetPosition;

            // ▼▼▼ 修正: ベースライン(Y軸)の計算ロジック ▼▼▼
            // IME入力中はガタつき防止のため、既存の _lastInputBaseLine を固定で使用
            if (input.IsComposing || input.IsTypingForPhysics)
            {
                targetPosition = new Point(metrics.RawPosition.X, (int)_lastInputBaseLine);
            }
            else
            {
                int currentLineIdx = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
                int calculatedBottom = metrics.RawPosition.Y + metrics.Height;

                // 削除操作かどうか (Back or Delete)
                // ※削除時は行が縮む可能性があるので、大きい高さを維持せずリセットする
                bool isDeleting = (input.IsDeleting || _inputState.LastKeyDown == Keys.Delete);

                // 条件: 「行が変わった」 または 「削除操作中」 ならベースラインを更新(リセット)
                if (currentLineIdx != _currentLineIndex || isDeleting)
                {
                    _lastInputBaseLine = calculatedBottom;
                    _currentLineIndex = currentLineIdx;
                }
                else
                {
                    // 条件: 「同じ行」で入力中なら、ベースラインを下げない(大きい文字に合わせる)
                    // これにより、見出しの後に小さい文字を打ってもカーソル位置が浮かない
                    _lastInputBaseLine = Math.Max(_lastInputBaseLine, calculatedBottom);
                }

                targetPosition = new Point(metrics.RawPosition.X, (int)_lastInputBaseLine);
            }
            // ▲▲▲ 修正ここまで ▲▲▲

            float diffY = Math.Abs(_physics.PosY - targetPosition.Y);
            bool isRowChanged = (diffY > Y_SNAP_THRESHOLD);

            _physics.Update(
                targetPosition,
                input.IsTypingForPhysics || isRowChanged,
                input.IsDeleting,
                metrics.RatchetThreshold,
                deltaTime,
                metrics.CharWidth,
                input.IsComposing,
                input.ElapsedSinceInput
            );

            _renderer.Render(_physics.PosX, _physics.PosY - metrics.Height, metrics.Height, input.IsComposing, input.IsTypingForBlink, metrics.Color);

            ForceHideSystemCaret();
        }

        private void RichTextBox1_TextChanged(object sender, EventArgs e)
        {
            _inputState.RegisterInput();
            ForceHideSystemCaret();

            // 行の実績判定（全消し時のリセット用）
            int lineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
            int start = richTextBox1.GetFirstCharIndexFromLine(lineIndex);
            int end = richTextBox1.GetFirstCharIndexFromLine(lineIndex + 1);
            if (end == -1) end = richTextBox1.TextLength;

            int currentLineLength = end - start;

            if (currentLineLength == 0)
            {
                if (_isLineSignificant)
                {
                    // IMEキャンセル対策: 処理後にリセットを実行
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

            if (!_inputState.IsImeComposing(richTextBox1.Handle))
            {
                Point p = GetCaretPosition();
                // ここでは仮更新のみ。詳細はTimer1_Tickで決定
                float h = (richTextBox1.SelectionFont ?? richTextBox1.Font).Height;
                // _lastInputBaseLine = p.Y + h; // Timer側で制御するため削除
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
            if (e.KeyCode == Keys.ShiftKey) _textStyler.HandleShiftKeyUp();

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
            int index = richTextBox1.GetCharIndexFromPosition(e.Location);
            Point pt = richTextBox1.GetPositionFromCharIndex(index);

            pt.X += richTextBox1.Location.X;
            pt.Y += richTextBox1.Location.Y;

            Font f = richTextBox1.SelectionFont ?? richTextBox1.Font;
            int lineHeight = f.Height;

            // クリック時はその場所の高さを採用してリセット
            _lastInputBaseLine = pt.Y + lineHeight;
            _currentLineIndex = richTextBox1.GetLineFromCharIndex(index);

            // 行の実績フラグ更新
            int start = richTextBox1.GetFirstCharIndexFromLine(_currentLineIndex);
            int end = richTextBox1.GetFirstCharIndexFromLine(_currentLineIndex + 1);
            if (end == -1) end = richTextBox1.TextLength;
            _isLineSignificant = (end - start) >= 5;

            _physics.NotifyMouseDown(pt);
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
            return new InputStateInfo { IsComposing = comp, IsDeleting = _inputState.IsDeleting(), ElapsedSinceInput = el, IsTypingForPhysics = (el < PHYSICS_TIMEOUT_MS) || comp, IsTypingForBlink = el < BLINK_TIMEOUT_MS };
        }

        private void InitializeBaseLineIfNeeded(CursorMetrics m) { if (_lastInputBaseLine < 0) _lastInputBaseLine = m.RawPosition.Y + m.Height; }

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