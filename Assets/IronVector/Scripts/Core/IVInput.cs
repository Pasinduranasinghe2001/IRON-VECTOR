using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace IronVector
{
    public enum IVKey
    {
        Forward, Back, Left, Right,
        Jump, Sprint, Crouch, CrouchAlt,
        Fire, Aim, Reload, Interact, Grenade,
        Weapon1, Weapon2,
        Scoreboard, Pause, ToggleHud
    }

    /// <summary>
    /// Tiny input layer that works with BOTH the new Input System and the legacy Input Manager.
    /// </summary>
    public static class IVInput
    {
        /// <summary>False while a menu is open: gameplay queries return nothing.</summary>
        public static bool GameplayEnabled = true;

        public static bool Held(IVKey key) => GameplayEnabled && RawHeld(key);
        public static bool Down(IVKey key) => GameplayEnabled && RawDown(key);

        public static Vector2 Move
        {
            get
            {
                if (!GameplayEnabled) return Vector2.zero;
                float x = (RawHeld(IVKey.Right) ? 1f : 0f) - (RawHeld(IVKey.Left) ? 1f : 0f);
                float y = (RawHeld(IVKey.Forward) ? 1f : 0f) - (RawHeld(IVKey.Back) ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static Vector2 Look => GameplayEnabled ? RawLook : Vector2.zero;
        public static float Scroll => GameplayEnabled ? RawScroll : 0f;

        public static bool RawHeld(IVKey key)
        {
            if (key == IVKey.Crouch) return Single(IVKey.Crouch, false) || Single(IVKey.CrouchAlt, false);
            return Single(key, false);
        }

        public static bool RawDown(IVKey key)
        {
            if (key == IVKey.Crouch) return Single(IVKey.Crouch, true) || Single(IVKey.CrouchAlt, true);
            return Single(key, true);
        }

#if ENABLE_INPUT_SYSTEM
        static ButtonControl Control(IVKey key)
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            switch (key)
            {
                case IVKey.Forward: return kb?.wKey;
                case IVKey.Back: return kb?.sKey;
                case IVKey.Left: return kb?.aKey;
                case IVKey.Right: return kb?.dKey;
                case IVKey.Jump: return kb?.spaceKey;
                case IVKey.Sprint: return kb?.leftShiftKey;
                case IVKey.Crouch: return kb?.cKey;
                case IVKey.CrouchAlt: return kb?.leftCtrlKey;
                case IVKey.Reload: return kb?.rKey;
                case IVKey.Interact: return kb?.fKey;
                case IVKey.Grenade: return kb?.gKey;
                case IVKey.Weapon1: return kb?.digit1Key;
                case IVKey.Weapon2: return kb?.digit2Key;
                case IVKey.Scoreboard: return kb?.tabKey;
                case IVKey.Pause: return kb?.escapeKey;
                case IVKey.ToggleHud: return kb?.hKey;
                case IVKey.Fire: return mouse?.leftButton;
                case IVKey.Aim: return mouse?.rightButton;
            }
            return null;
        }

        static bool Single(IVKey key, bool down)
        {
            ButtonControl c = Control(key);
            if (c == null) return false;
            return down ? c.wasPressedThisFrame : c.isPressed;
        }

        public static Vector2 RawLook => Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
        public static float RawScroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        static KeyCode Code(IVKey key)
        {
            switch (key)
            {
                case IVKey.Forward: return KeyCode.W;
                case IVKey.Back: return KeyCode.S;
                case IVKey.Left: return KeyCode.A;
                case IVKey.Right: return KeyCode.D;
                case IVKey.Jump: return KeyCode.Space;
                case IVKey.Sprint: return KeyCode.LeftShift;
                case IVKey.Crouch: return KeyCode.C;
                case IVKey.CrouchAlt: return KeyCode.LeftControl;
                case IVKey.Reload: return KeyCode.R;
                case IVKey.Interact: return KeyCode.F;
                case IVKey.Grenade: return KeyCode.G;
                case IVKey.Weapon1: return KeyCode.Alpha1;
                case IVKey.Weapon2: return KeyCode.Alpha2;
                case IVKey.Scoreboard: return KeyCode.Tab;
                case IVKey.Pause: return KeyCode.Escape;
                case IVKey.ToggleHud: return KeyCode.H;
                case IVKey.Fire: return KeyCode.Mouse0;
                case IVKey.Aim: return KeyCode.Mouse1;
            }
            return KeyCode.None;
        }

        static bool Single(IVKey key, bool down)
        {
            KeyCode code = Code(key);
            if (code == KeyCode.None) return false;
            return down ? Input.GetKeyDown(code) : Input.GetKey(code);
        }

        public static Vector2 RawLook => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        public static float RawScroll => Input.mouseScrollDelta.y;
#endif
    }
}
