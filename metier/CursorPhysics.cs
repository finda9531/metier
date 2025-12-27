#nullable disable
using System;
using System.Drawing;

namespace eep.editer1
{
    public class CursorPhysics
    {
        // 既存の定数（入力時の挙動用）
        private const float Y_SMOOTH = 0.3f;
        private const float X_TENSION = 0.15f;
        private const float RAPID_TENSION = 0.02f;
        private const float RAPID_FRICTION = 0.85f;
        private const float FRICTION_FORWARD = 0.65f;
        private const float FRICTION_BACKWARD = 0.45f;
        private const float SNAP_THRESHOLD = 0.5f;
        private const float STOP_VELOCITY = 0.5f;

        // ★クリック移動用の定数（ここだけ新設）
        // クリック時は少しキビキビ動かすために強めの設定にします
        private const float CLICK_TENSION = 0.25f;
        private const float CLICK_FRICTION = 0.70f;

        public float PosX { get; private set; }
        public float PosY { get; private set; }
        private float velX = 0;
        private float velY = 0; // クリック移動のY軸用に追加
        private float maxTargetX = 0;

        // ★クリック移動モードかどうかのフラグ
        private bool _isClickMoveMode = false;

        // ★マウスでクリックしたときに呼ぶメソッド
        public void NotifyMouseDown(Point newTargetPos)
        {
            // リミッターを解除して新しい場所をセット
            maxTargetX = newTargetPos.X;
            _isClickMoveMode = true;

            // 速度をリセット（変な慣性を消す）
            velX = 0;
            velY = 0;
        }

        public void Update(Point realTargetPos, bool isTyping, bool isDeleting, float ratchetThreshold, float deltaTime, float charWidthLimit, bool isComposing, long elapsedInput)
        {
            // タイピングが始まったら、即座に「いつもの入力モード」に戻す
            if (isTyping)
            {
                _isClickMoveMode = false;
            }

            // =========================================================
            // 分岐: クリック移動モード or いつもの入力モード
            // =========================================================
            if (_isClickMoveMode)
            {
                // ★A. クリック時の挙動 (XもYもバネで素直に移動)
                // -----------------------------------------------------

                // --- X軸の計算 ---
                float diffX = realTargetPos.X - PosX;
                if (Math.Abs(diffX) < SNAP_THRESHOLD && Math.Abs(velX) < STOP_VELOCITY)
                {
                    PosX = realTargetPos.X;
                    velX = 0;
                }
                else
                {
                    // ラチェット(リミッター)なしで、前後左右にスムーズに動く
                    float forceX = diffX * CLICK_TENSION;
                    velX += forceX * deltaTime;
                    velX *= (float)Math.Pow(CLICK_FRICTION, deltaTime);
                    PosX += velX * deltaTime;
                }

                // --- Y軸の計算 ---
                float diffY = realTargetPos.Y - PosY;
                if (Math.Abs(diffY) < SNAP_THRESHOLD && Math.Abs(velY) < STOP_VELOCITY)
                {
                    PosY = realTargetPos.Y;
                    velY = 0;
                }
                else
                {
                    // Y_SMOOTH ではなくバネ計算を使って、X軸と同期して斜めに動くようにする
                    float forceY = diffY * CLICK_TENSION;
                    velY += forceY * deltaTime;
                    velY *= (float)Math.Pow(CLICK_FRICTION, deltaTime);
                    PosY += velY * deltaTime;
                }

                // ターゲットに到達したらモード終了
                if (PosX == realTargetPos.X && PosY == realTargetPos.Y)
                {
                    _isClickMoveMode = false;
                }
            }
            else
            {
                // ★B. いつもの入力モード (ご提示いただいたコードそのまま)
                // -----------------------------------------------------

                float effectiveTargetX = realTargetPos.X;

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
                        if (jumpDistance < ratchetThreshold) effectiveTargetX = maxTargetX;
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

                // 元のY軸移動 (線形補間)
                PosY += (realTargetPos.Y - PosY) * Y_SMOOTH * deltaTime;

                float diffX = effectiveTargetX - PosX;
                float diffY = Math.Abs(realTargetPos.Y - PosY);

                // 元のロジック: Yの差が大きいときはXをゆっくり動かす
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
        }
    }
}