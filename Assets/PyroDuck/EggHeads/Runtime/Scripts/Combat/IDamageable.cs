using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    public interface IDamageable
    {
        void TakeDamage(float amount, GameObject source,Vector2 hitPoint);
    }
}
