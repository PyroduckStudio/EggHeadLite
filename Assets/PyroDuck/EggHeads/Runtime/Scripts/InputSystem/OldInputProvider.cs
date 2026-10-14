using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.InputSystem
{ 
    public class OldInputProvider : IInputProvider
    {
        public Vector2 GetMove()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            return new Vector2(x, y);
        }

        public Vector2 GetPointerPosition()
        {
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;
            return Input.mousePosition;
        }

        public bool IsPointerActive()
        {
            if (Input.touchCount > 0) return true;
            return Input.mousePresent || Input.GetMouseButton(0);
        }

        public bool GetJumpDown()       => Input.GetKeyDown(KeyCode.Space);
        public bool GetFireDown()       => Input.GetMouseButtonDown(0);
        public bool GetFireHeld()       => Input.GetMouseButton(0);
        public bool GetCrouchHeld()     => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public bool GetRunHeld()        => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        public bool GetDropWeaponDown() => Input.GetKeyDown(KeyCode.G);
        public bool GetNextWeaponDown() => Input.GetKeyDown(KeyCode.E);
        public bool GetPrevWeaponDown() => Input.GetKeyDown(KeyCode.Q);

        public void DisableInput() { }
    }
}
