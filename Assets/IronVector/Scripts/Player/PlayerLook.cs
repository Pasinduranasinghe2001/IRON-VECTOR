using FishNet.Object;
using UnityEngine;

namespace IronVector
{
    /// <summary>Mouse look, recoil, FOV/ADS zoom, pause/cursor handling and vehicle chase camera.</summary>
    public class PlayerLook : NetworkBehaviour
    {
        public Transform cameraPivot;
        public Camera playerCamera;
        public float minPitch = -85f;
        public float maxPitch = 85f;
        public float recoilRecoverySpeed = 12f;

        /// <summary>Set by WeaponController (1 = hip, &lt;1 = zoomed).</summary>
        [System.NonSerialized] public float FovMultiplier = 1f;

        public static bool PauseMenuOpen { get; private set; }

        PlayerIdentity _id;
        AudioListener _listener;
        float _yaw, _pitch, _recoilToRecover;
        float _camYaw, _camPitch = 12f;
        bool _vehicleCamActive;

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
            playerCamera.enabled = true;
            if (_listener != null) _listener.enabled = true;
            _yaw = transform.eulerAngles.y;
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
            if (!PauseMenuOpen && Cursor.lockState != CursorLockMode.Locked && IVInput.RawDown(IVKey.Fire)) SetPaused(false);

            float ads = _id.Weapons != null ? _id.Weapons.AdsAmount : 0f;
            Vector2 look = IVInput.Look * LocalProfile.Sensitivity * Mathf.Lerp(1f, 0.55f, ads);

            if (_id.VehicleUser.InVehicle)
            {
                _camYaw += look.x;
                _camPitch = Mathf.Clamp(_camPitch - look.y, -10f, 60f);
                return;
            }

            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);

            // Partial recoil recovery after the player stops shooting.
            if (_recoilToRecover > 0f && _id.Weapons != null && Time.time - _id.Weapons.LastShotTime > 0.12f)
            {
                float r = Mathf.Min(_recoilToRecover, recoilRecoverySpeed * Time.deltaTime);
                _pitch += r;
                _recoilToRecover -= r;
            }

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void LateUpdate()
        {
            if (!IsOwner || playerCamera == null) return;

            VehicleController vehicle = _id.VehicleUser.Vehicle;
            if (vehicle != null)
            {
                Quaternion orbit = Quaternion.Euler(_camPitch, _camYaw, 0f);
                Vector3 target = vehicle.transform.position + Vector3.up * 1.6f;
                Vector3 desired = target + orbit * new Vector3(0f, 0f, -7.5f);
                Vector3 dir = desired - target;
                if (Physics.SphereCast(target, 0.25f, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(vehicle.transform))
                {
                    desired = hit.point + hit.normal * 0.3f;
                }
                playerCamera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(target - desired));
                _vehicleCamActive = true;
            }
            else if (_vehicleCamActive)
            {
                _vehicleCamActive = false;
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            float targetFov = LocalProfile.FieldOfView * (vehicle != null ? 1.05f : FovMultiplier);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        public void AddRecoil(float vertical, float horizontal)
        {
            _pitch = Mathf.Clamp(_pitch - vertical, minPitch, maxPitch);
            _yaw += horizontal;
            _recoilToRecover += vertical * 0.6f;
        }

        public void ResetView(float yaw)
        {
            _yaw = yaw;
            _pitch = 0f;
            _recoilToRecover = 0f;
        }

        public void EnterVehicleView(float vehicleYaw)
        {
            _camYaw = vehicleYaw;
            _camPitch = 12f;
        }
    }
}
