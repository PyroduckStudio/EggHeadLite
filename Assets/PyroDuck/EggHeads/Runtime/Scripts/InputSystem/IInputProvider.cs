using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.InputSystem
{
    /// <summary>Analog/digital movement input (walk/run/crouch/jump).</summary>
    public interface IMovementInput
    {
        Vector2 GetMove();
        bool GetJumpDown();
        bool GetCrouchHeld();
        bool GetRunHeld();
    }

    /// <summary>Pointer/aim query surface (screen position + availability).</summary>
    public interface IPointerInput
    {
        Vector2 GetPointerPosition();
        bool IsPointerActive();
    }

    /// <summary>Combat/weapon oriented input (fire, drop, cycle).</summary>
    public interface ICombatInput
    {
        bool GetFireDown();
        bool GetFireHeld();
        bool GetDropWeaponDown();
        bool GetNextWeaponDown();
        bool GetPrevWeaponDown();
    }

    /// <summary>
    /// Aggregate input surface kept for backwards compatibility. Prefer depending
    /// on the segregated interfaces (<see cref="IMovementInput"/>, <see cref="IPointerInput"/>,
    /// <see cref="ICombatInput"/>) in new code.
    /// </summary>
    public interface IInputProvider : IMovementInput, IPointerInput, ICombatInput
    {
        void DisableInput();
    }
}
