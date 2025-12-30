#nullable disable
using System;
using System.Drawing;

namespace Metier
{
    public class CursorPhysics
    {
        // --- 入力時（タイピング）用の物理定数 ---
        private const float Y_SMOOTH = 0.3f;
        private const float X_TENSION = 0.15f;
        private const float RAPID_TENSION = 0.02f;
        private const float RAPID_FRICTION = 0.85f;
        private const float FRICTION_FORWARD = 0.65f;
        private const float FRICTION_BACKWARD = 0.45f;
        private const float SNAP_THRESHOLD = 0.5f;
        private const float STOP_VELOCITY = 0.5f;

        // 液体シミュレーション定数
        private const float LIQUID_FOLLOW_FACTOR = 0.2f;
        private const float LIQUID_WIDTH_GAIN = 0.3f;
        private const float BASE_WIDTH = 2.0f;

        // --- カーソル座標 ---
        public float PosX { get; private set; }
        public float PosY { get; private set; }

        // --- 液体シミュレーション用変数 ---
        private float _liquidX;
        public float CurrentWidth { get; private set; } = BASE_WIDTH;

        private float velX = 0;
        private float maxTargetX = 0;

        // --- アニメーション用変数 ---
        private bool _isAnimationMode = false;
        private bool _preserveAnimationOnTyping = false;
        private float _animStartX;
        private float _animStartY;
        private float _animTargetX;
        private float _animTargetY;
        private float _animTimeCurrent;

        public float AnimationDuration { get; set; } = 250.0f;

        public CursorPhysics()
        {
            _liquidX = 0;
            CurrentWidth = BASE_WIDTH;
        }

        public void StartAnimation(Point newTargetPos, bool isLineJump = false)
        {
            _animStartX = PosX;
            _animStartY = PosY;
            _animTargetX = newTargetPos.X;
            _animTargetY = newTargetPos.Y;
            _animTimeCurrent = 0;

            _isAnimationMode = true;
            _preserveAnimationOnTyping = isLineJump;

            velX = 0;
            maxTargetX = newTargetPos.X;
        }

        // 修正: 使われていないパラメータ (charWidthLimit, isComposing, elapsedInput) を削除
        public void Update(Point realTargetPos, bool isTyping, bool isDeleting, float ratchetThreshold, float deltaTime)
        {
            if (_liquidX == 0 && PosX != 0) _liquidX = PosX;

            if (isTyping && !_preserveAnimationOnTyping)
            {
                _isAnimationMode = false;
            }

            if (_isAnimationMode)
            {
                _animTimeCurrent += deltaTime * 10.0f;
                float progress = _animTimeCurrent / AnimationDuration;

                if (progress >= 1.0f)
                {
                    PosX = _animTargetX;
                    PosY = _animTargetY;
                    _isAnimationMode = false;
                    _preserveAnimationOnTyping = false;
                }
                else
                {
                    float t = 1.0f - progress;
                    float ease = 1.0f - (t * t * t * t);

                    PosX = _animStartX + (_animTargetX - _animStartX) * ease;
                    PosY = _animStartY + (_animTargetY - _animStartY) * ease;
                }
            }
            else
            {
                // 修正: 初期化代入を削除 (IDE0059対策)
                float effectiveTargetX;

                if (isTyping && !isDeleting)
                {
                    if (realTargetPos.X >= maxTargetX)
                    {
                        maxTargetX = realTargetPos.X;
                        effectiveTargetX = realTargetPos.X;
                    }
                    else
                    {
                        float jumpDistance = maxTargetX - realTargetPos.X;
                        if (jumpDistance < ratchetThreshold)
                        {
                            effectiveTargetX = maxTargetX;
                        }
                        else
                        {
                            maxTargetX = realTargetPos.X;
                            effectiveTargetX = realTargetPos.X;
                        }
                    }
                }
                else
                {
                    maxTargetX = realTargetPos.X;
                    effectiveTargetX = realTargetPos.X;
                }

                PosY += (realTargetPos.Y - PosY) * Y_SMOOTH * deltaTime;

                float diffX = effectiveTargetX - PosX;
                float diffY = Math.Abs(realTargetPos.Y - PosY);

                if (diffY > 5.0f)
                {
                    PosX += diffX * 0.3f * deltaTime;
                    velX = 0;
                }
                else if (Math.Abs(diffX) < SNAP_THRESHOLD && Math.Abs(velX) < STOP_VELOCITY)
                {
                    PosX = effectiveTargetX;
                    velX = 0;
                }
                else
                {
                    float tension;
                    float friction;
                    bool isMovingLeft = (diffX < 0);

                    if (isTyping && !isMovingLeft)
                    {
                        tension = RAPID_TENSION;
                        friction = RAPID_FRICTION;
                    }
                    else if (isMovingLeft)
                    {
                        tension = X_TENSION;
                        friction = FRICTION_BACKWARD;
                    }
                    else
                    {
                        tension = X_TENSION;
                        friction = FRICTION_FORWARD;
                    }

                    float force = diffX * tension;
                    velX += force * deltaTime;
                    velX *= (float)Math.Pow(friction, deltaTime);
                    PosX += velX * deltaTime;
                }
            }

            // 液体シミュレーション
            float followFactor = LIQUID_FOLLOW_FACTOR * deltaTime;
            if (followFactor > 1.0f) followFactor = 1.0f;

            _liquidX += (PosX - _liquidX) * followFactor;

            float diffLiquid = PosX - _liquidX;
            float extraWidth = Math.Abs(diffLiquid) * LIQUID_WIDTH_GAIN;

            CurrentWidth = BASE_WIDTH + extraWidth;

            if (Math.Abs(diffLiquid) < 0.1f)
            {
                _liquidX = PosX;
                CurrentWidth = BASE_WIDTH;
            }
        }
    }
}