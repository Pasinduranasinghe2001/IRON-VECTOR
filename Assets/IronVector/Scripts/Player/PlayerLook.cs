using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Mouse look, recoil, micro screen shake, HUD toggle (H), pause handling,
    /// vehicle chase camera and Search &amp; Destroy teammate spectating.
    /// </summary>
    public class PlayerLook : NetworkBehaviour
    {
        public Transform cameraPivot;
        public Camera playerCamera;
        public float minPitch = -85f;
        public float maxPitch = 85f;
        public float recoilRecoverySpeed = 14f;

        [System.NonSerialized] public float FovMultiplier = 1f;

        public static bool PauseMenuOpen { get; private set; }
        public string SpectatingName { get; private set; } = "";

        PlayerIdentity _id;
        AudioListener _listener;
        float _yaw, _pitch, _recoilToRecover;
        float _camYaw, _camPitch = 14f;
        float _shake;
        bool _thirdPersonCamActive;
        int _spectateIndex;

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
            if (playerCamera != null)
            {
                _listener = playerCamera.GetComponent<AudioListener>();
                playerCamera.enabled = false;
                if (_listener != null) _listener.enabled = false;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner || playerCamera == null) return;
            playerCamera.nearClipPlane = 0.03f;
            playerCamera.enabled = true;
            if (_listener != null) _listener.enabled = true;
            _yaw = transform.eulerAngles.y;
            GraphicsSettingsManager.Apply();
            SetPaused(false);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (playerCamera != null && playerCamera.enabled) ResetInputState();
        }

        public void SetPaused(bool paused)
        {
            PauseMenuOpen = paused;
            IVInput.GameplayEnabled = !paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        public static void Resume()
        {
            if (PlayerIdentity.Local != null) PlayerIdentity.Local.Look.SetPaused(false);
        }

        public static void ResetInputState()
        {
            PauseMenuOpen = false;
            IVInput.GameplayEnabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (!IsOwner) return;

            if (IVInput.RawDown(IVKey.Pause)) SetPaused(!PauseMenuOpen);
            if (IVInput.RawDown(IVKey.ToggleHud)) LocalProfile.ShowHud = !LocalProfile.ShowHud;
            if (!PauseMenuOpen && Cursor.lockState != CursorLockMode.Locked && IVInput.RawDown(IVKey.Fire)) SetPaused(false);

            float ads = _id.Weapons != null ? _id.Weapons.AdsAmount : 0f;
            Vector2 look = IVInput.Look * LocalProfile.Sensitivity * Mathf.Lerp(1f, 0.55f, ads);

            bool spectating = !_id.Health.IsAlive && MatchManager.Instance != null && MatchManager.Instance.IsSearchAndDestroy;
            if (_id.VehicleUser.InVehicle || spectating)
            {
                _camYaw += look.x;
                _camPitch = Mathf.Clamp(_camPitch - look.y, -10f, 60f);
                if (spectating && (IVInput.Down(IVKey.Jump) || IVInput.Down(IVKey.Fire))) _spectateIndex++;
                return;
            }

            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);

            if (_recoilToRecover > 0f && _id.Weapons != null && Time.time - _id.Weapons.LastShotTime > 0.11f)
            {
                float r = Mathf.Min(_recoilToRecover, recoilRecoverySpeed * Time.deltaTime);
                _pitch += r;
                _recoilToRecover -= r;
            }

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 4.5f);
                float sx = _shake > 0.001f ? Random.Range(-_shake, _shake) : 0f;
                float sy = _shake > 0.001f ? Random.Range(-_shake, _shake) : 0f;
                cameraPivot.localRotation = Quaternion.Euler(_pitch + sx, sy, 0f);
            }
        }

        void LateUpdate()
        {
            if (!IsOwner || playerCamera == null) return;

            SpectatingName = "";
            VehicleController vehicle = _id.VehicleUser.Vehicle;
            Transform chaseTarget = null;
            float chaseDistance = 7.5f;

            if (vehicle != null)
            {
                chaseTarget = vehicle.transform;
                chaseDistance = 7.5f;
            }
            else if (!_id.Health.IsAlive && MatchManager.Instance != null && MatchManager.Instance.IsSearchAndDestroy)
            {
                PlayerIdentity mate = GetSpectateTarget();
                if (mate != null)
                {
                    chaseTarget = mate.transform;
                    chaseDistance = 4.2f;
                    SpectatingName = mate.PlayerName.Value;
                }
            }

            if (chaseTarget != null)
            {
                Quaternion orbit = Quaternion.Euler(_camPitch, _camYaw, 0f);
                Vector3 target = chaseTarget.position + Vector3.up * 1.6f;
                Vector3 desired = target + orbit * new Vector3(0f, 0f, -chaseDistance);
                Vector3 dir = desired - target;
                if (Physics.SphereCast(target, 0.25f, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(chaseTarget))
                {
                    desired = hit.point + hit.normal * 0.3f;
                }
                playerCamera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(target - desired));
                _thirdPersonCamActive = true;
            }
            else if (_thirdPersonCamActive)
            {
                _thirdPersonCamActive = false;
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            bool sprinting = _id.Motor != null && _id.Motor.IsSprinting && vehicle == null;
            float fovBoost = sprinting ? 1.06f : 1f;
            float targetFov = LocalProfile.FieldOfView * (vehicle != null ? 1.05f : FovMultiplier * fovBoost);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        PlayerIdentity GetSpectateTarget()
        {
            var mates = new List<PlayerIdentity>();
            foreach (PlayerIdentity p in PlayerIdentity.All)
                if (p != null && p != _id && p.Team.Value == _id.Team.Value && p.Health != null && p.Health.IsAlive)
                    mates.Add(p);
            if (mates.Count == 0) return null;
            return mates[_spectateIndex % mates.Count];
        }

        public void AddRecoil(float vertical, float horizontal)
        {
            _pitch = Mathf.Clamp(_pitch - vertical, minPitch, maxPitch);
            _yaw += horizontal;
            _recoilToRecover += vertical * 0.58f;
        }

        public void AddScreenShake(float amount)
        {
            _shake = Mathf.Clamp(_shake + amount, 0f, 2.2f);
        }

        public void ResetView(float yaw)
        {
            _yaw = yaw;
            _pitch = 0f;
            _recoilToRecover = 0f;
            _shake = 0f;
        }

        public void EnterVehicleView(float vehicleYaw)
        {
            _camYaw = vehicleYaw;
            _camPitch = 12f;
        }
    }
}
