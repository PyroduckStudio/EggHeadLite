using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Utils
{
    /// <summary>
    /// Thin compatibility layer that maps <see cref="Rigidbody2D.linearVelocity"/> (Unity 6+)
    /// onto <c>velocity</c> on supported pre-Unity 6 editors.
    /// Keeps the runtime package compiling across every Unity version declared
    /// in the product documentation.
    /// </summary>
    public static class Rigidbody2DUtils
    {
        public static Vector2 GetLinearVelocity(this Rigidbody2D rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetLinearVelocity(this Rigidbody2D rb, Vector2 value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }
    }
}