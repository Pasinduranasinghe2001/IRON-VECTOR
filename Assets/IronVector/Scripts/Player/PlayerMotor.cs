using FishNet.Object;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// First-person movement: walk, sprint, crouch, slide, jump, scaled by weapon mobility
    /// and Rusher strategy (+18% movement speed).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        [Header("References")]
        public Transform cameraPivot;
        public Transform bodyVisual;
        public Transform head;

        [Header("Speeds")]
        public float walkSpeed = 5.4f;
        public float sprintSpeed = 8.4f;
        public float crouchSpeed = 2.9f;
        public float adsSpeedMultiplier = 0.62f;
        public float acceleration = 15f;
        public float airControl = 0.38f;

        [Header("Jump / Gravity")]
        public float jumpHeight = 1.15f;
        public float gravity = -22f;

        [Header("Slide")]
        public float slideSpeed = 12.0f;
        public float slideDuration = 0.72f;
        public float slideCooldown = 0.55f;

        [Header("Stance")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.15f;
        public float standEyeHeight = 1.6f;
        public float crouchEyeHeight = 1.0f;

        [System.NonSerialized] public bool AimingDown;

        CharacterController _cc;
        PlayerIdentity _id;
        Vector3 _horizontalVelocity;
        float _verticalVelocity;
        float _slideTimer;
        float _nextSlideTime;
        Vector3 _slideDirection;
        bool _crouching;
        float _stance = 1f;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching => _crouching;
        public bool IsSliding => _slideTimer > 0f;
        public bool IsGrounded => _cc != null && _cc.enabled && _cc.isGrounded;
        public float Speed01 => Mathf.Clamp01(_horizontalVelocity.magnitude / sprintSpeed);

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _id = GetComponent<PlayerIdentity>();
        }

        void Update()
        {
            if (IsOwner)
            {
                OwnerUpdate();
            }
            else if (cameraPivot != null)
            {
                float t = Mathf.InverseLerp(crouchEyeHeight, standEyeHeight, cameraPivot.localPosition.y);
                ApplyStance(t, false);
            }
        }

        void OwnerUpdate()
        {
            bool locked = !_id.Health.IsAlive || _id.VehicleUser.InVehicle;
            if (locked || !_cc.enabled)
            {
                _horizontalVelocity = Vector3.zero;
                _verticalVelocity = 0f;
                _slideTimer = 0f;
                IsSprinting = false;
                return;
            }

            float dt = Time.deltaTime;
            Vector2 input = IVInput.Move;
            bool grounded = _cc.isGrounded;
            bool firing = IVInput.Held(IVKey.Fire);
            bool wantsSprint = IVInput.Held(IVKey.Sprint) && input.y > 0.1f && !AimingDown && !firing;

            if (IVInput.Down(IVKey.Crouch) && IsSprinting && grounded && Time.time >= _nextSlideTime)
            {
                _slideTimer = slideDuration;
                _nextSlideTime = Time.time + slideDuration + slideCooldown;
                _slideDirection = transform.forward;
            }
            if (_slideTimer > 0f) _slideTimer -= dt;

            bool wantsCrouch = IVInput.Held(IVKey.Crouch) || _slideTimer > 0f;
            if (!wantsCrouch && _crouching && !CanStandUp()) wantsCrouch = true;
            _crouching = wantsCrouch;
            IsSprinting = wantsSprint && !_crouching;

            float perkSpeedMult = _id.Strategy.Value == (int)TacticalStrategy.Rusher ? 1.18f : 1.0f;
            float weaponMult = _id.Weapons != null ? _id.Weapons.MoveSpeedMultiplier : 1.0f;

            float targetSpeed = (_crouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed) * perkSpeedMult * weaponMult;
            if (AimingDown) targetSpeed *= adsSpeedMultiplier;
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * targetSpeed;

            if (_slideTimer > 0f)
            {
                float k = Mathf.Clamp01(_slideTimer / slideDuration);
                _horizontalVelocity = _slideDirection * Mathf.Lerp(crouchSpeed, slideSpeed * perkSpeedMult, k);
            }
            else
            {
                float accel = grounded ? acceleration : acceleration * airControl;
                _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, wish, 1f - Mathf.Exp(-accel * dt));
            }

            if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            if (grounded && IVInput.Down(IVKey.Jump))
            {
                _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                _slideTimer = 0f;
            }
            _verticalVelocity += gravity * dt;

            _cc.Move((_horizontalVelocity + Vector3.up * _verticalVelocity) * dt);

            _stance = Mathf.MoveTowards(_stance, _crouching ? 0f : 1f, dt * 6f);
            ApplyStance(_stance, true);
        }

        void ApplyStance(float standT, bool moveCamera)
        {
            float h = Mathf.Lerp(crouchHeight, standHeight, standT);
            if (_cc != null)
            {
                _cc.height = h;
                _cc.center = new Vector3(0f, h * 0.5f, 0f);
            }
            if (bodyVisual != null)
            {
                float bodyH = h - 0.2f;
                Vector3 s = bodyVisual.localScale;
                bodyVisual.localScale = new Vector3(s.x, bodyH * 0.5f, s.z);
                bodyVisual.localPosition = new Vector3(0f, bodyH * 0.5f, 0f);
            }
            if (head != null) head.localPosition = new Vector3(0f, h - 0.18f, 0f);
            if (moveCamera && cameraPivot != null)
                cameraPivot.localPosition = new Vector3(0f, Mathf.Lerp(crouchEyeHeight, standEyeHeight, standT), 0f);
        }

        bool CanStandUp()
        {
            float r = _cc.radius * 0.9f;
            Vector3 start = transform.position + Vector3.up * (_cc.height - r);
            float distance = standHeight - _cc.height + 0.05f;
            return !Physics.SphereCast(start, r, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _cc.enabled;
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _slideTimer = 0f;
            _cc.enabled = wasEnabled;
            Physics.SyncTransforms();
        }
    }
}
