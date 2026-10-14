using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Events;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    /// <summary>
    /// Attached to a dropped weapon object on the ground.
    /// Publishes WeaponPickupEvent when a character walks over it, then destroys itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class WeaponPickup : MonoBehaviour
    {
        [SerializeField] private VisualDataSO _weaponData;
        [SerializeField] private float _pickupCooldown = 0.3f;

        private float _spawnTime;

        public void Init(VisualDataSO weaponData)
        {
            _weaponData = weaponData;
            _spawnTime  = Time.time;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Time.time - _spawnTime < _pickupCooldown) return;
            if (_weaponData == null) return;

            if (other.GetComponentInParent<BaseCharacterController>() == null) return;

            EventManager.Publish(new WeaponPickupEvent { WeaponData = _weaponData });
            Destroy(gameObject);
        }
    }
}
