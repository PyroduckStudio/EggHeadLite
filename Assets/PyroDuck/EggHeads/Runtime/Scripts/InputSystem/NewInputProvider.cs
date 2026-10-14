#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using TMPro;

namespace com.pyroduck.eggheads.Runtime.Scripts.InputSystem
{ 
    public class NewInputProvider : IInputProvider, IDisposable
    {
        private InputSystemActions _input;

        public NewInputProvider()
        {
            _input = new InputSystemActions();
            _input.Player.Enable();
        }

        public void DisableInput()
        {
            _input?.Player.Disable();
        }

        public void Dispose()
        {
            if (_input == null) return;

            _input.Disable();
            _input.Dispose();
            _input = null;
        }

        public Vector2 GetMove()
        {
            return _input.Player.Move.ReadValue<Vector2>();
        }

        public Vector2 GetPointerPosition()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }

            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Vector2.zero;
        }

        public bool IsPointerActive()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return true;
            }

            if (Mouse.current != null)
            {
                return true;
            }

            return false;
        }

        public bool GetJumpDown()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && !IsTextInputFocused())
                return true;

            return _input.Player.Jump.WasPressedThisFrame();
        }

        private static bool IsTextInputFocused()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
                return false;

            return selected.GetComponent<TMP_InputField>() != null
                   || selected.GetComponent<InputField>() != null;
        }

        public bool GetFireDown()
        {
            return _input.Player.Attack.WasPressedThisFrame();
        }

        public bool GetFireHeld()
        {
            return _input.Player.Attack.IsPressed();
        }

        public bool GetCrouchHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        public bool GetRunHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
        }

        public bool GetDropWeaponDown()
        {
            var kb = Keyboard.current;
            return kb != null && kb.gKey.wasPressedThisFrame;
        }

        public bool GetNextWeaponDown()
        {
            var kb = Keyboard.current;
            return kb != null && kb.eKey.wasPressedThisFrame;
        }

        public bool GetPrevWeaponDown()
        {
            var kb = Keyboard.current;
            return kb != null && kb.qKey.wasPressedThisFrame;
        }
    }
}
#endif
