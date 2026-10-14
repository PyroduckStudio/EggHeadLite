using System.Collections.Generic;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.InputSystem;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    /// <summary>
    /// Owns the player weapon loadout: cycling, equipping, dropping, and pickup auto-equip.
    /// Movement and health stay on <see cref="EggHeadController"/>.
    /// </summary>
    [RequireComponent(typeof(EggHeadController))]
    public class WeaponLoadoutController : MonoBehaviour
    {
        [Header("Weapon Library")]
        [Tooltip("Drag Melee, Ranged, and Throwable VisualGroupsDataSO assets here.")]
        [SerializeField] private List<VisualGroupsDataSO> weaponGroups = new List<VisualGroupsDataSO>();

        [Header("Drop Settings")]
        [Tooltip("Impulse force applied to the dropped weapon.")]
        [SerializeField] private Vector2 dropImpulse = new Vector2(2f, 4f);

        [Tooltip("Radius of the CircleCollider2D added to the dropped weapon pickup object.")]
        [SerializeField] private float droppedColliderRadius = 0.3f;

        private readonly List<VisualDataSO> _allWeapons = new List<VisualDataSO>();
        private EggHeadController _owner;
        private IInputProvider _input;
        private int _currentWeaponIndex = -1;

        private void Awake()
        {
            _owner = GetComponent<EggHeadController>();
            _input = InputProviderService.Get();
            BuildWeaponList();
        }

        private void OnEnable()
        {
            _input = InputProviderService.Get();
            EventManager.Subscribe<WeaponPickupEvent>(OnWeaponPickupEvent);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<WeaponPickupEvent>(OnWeaponPickupEvent);
        }

        public void TickInput()
        {
            if (_input == null) return;

            if (_input.GetNextWeaponDown())
                CycleWeapon(+1);
            else if (_input.GetPrevWeaponDown())
                CycleWeapon(-1);

            if (_input.GetDropWeaponDown())
                DropWeapon();
        }

        public void BuildWeaponList()
        {
            _allWeapons.Clear();
            if (weaponGroups == null) return;

            foreach (var group in weaponGroups)
            {
                if (group == null || group.VisualList == null) continue;

                foreach (var item in group.VisualList)
                {
                    if (item != null)
                        _allWeapons.Add(item);
                }
            }
        }

        public void BindCurrentWeaponOwner()
        {
            var currentGO = _owner.CurrentVisualController?.GetCurrentVisual(VisualType.Weapon);
            var weapon = currentGO != null ? currentGO.GetComponentInChildren<Weapon>(true) : null;
            weapon?.SetOwner(_owner.GetRootOwner());
        }

        private void EquipWeapon(int index)
        {
            if (_allWeapons.Count == 0) return;

            _currentWeaponIndex = Mathf.Clamp(index, 0, _allWeapons.Count - 1);
            EquipWeapon(_allWeapons[_currentWeaponIndex]);
        }

        private void EquipWeapon(VisualDataSO data)
        {
            var visualController = _owner.CurrentVisualController;
            if (visualController == null || data == null) return;

            visualController.CreateItem(data, VisualType.Weapon);

            var currentGO = visualController.GetCurrentVisual(VisualType.Weapon);
            var weapon = currentGO != null ? currentGO.GetComponentInChildren<Weapon>() : null;
            if (weapon != null)
            {
                weapon.SetOwner(_owner.GetRootOwner());
                weapon.PlayEquipSound();
            }

            int idx = _allWeapons.IndexOf(data);
            if (idx >= 0)
                _currentWeaponIndex = idx;
        }

        private void CycleWeapon(int direction)
        {
            if (_allWeapons.Count == 0) return;

            if (_currentWeaponIndex < 0)
            {
                EquipWeapon(0);
                return;
            }

            int next = (_currentWeaponIndex + direction + _allWeapons.Count) % _allWeapons.Count;
            EquipWeapon(next);
        }

        private void DropWeapon()
        {
            var visualController = _owner.CurrentVisualController;
            if (visualController == null) return;

            var currentGO = visualController.GetCurrentVisual(VisualType.Weapon);
            if (currentGO == null) return;

            var droppedData = _currentWeaponIndex >= 0 && _currentWeaponIndex < _allWeapons.Count
                ? _allWeapons[_currentWeaponIndex]
                : null;
            if (droppedData == null) return;

            var weapon = currentGO.GetComponentInChildren<Weapon>();
            weapon?.PlayDropSound();

            WeaponPickupFactory.Create(
                droppedData,
                currentGO.transform.position,
                dropImpulse,
                facingDir: _owner.Move.x >= 0f ? 1f : -1f,
                colliderRadius: droppedColliderRadius);

            visualController.CreateItem((VisualDataSO)null, VisualType.Weapon);
            _currentWeaponIndex = -1;
        }

        private void OnWeaponPickupEvent(WeaponPickupEvent evt)
        {
            if (!_owner.IsAlive) return;
            if (evt.WeaponData == null) return;
            EquipWeapon(evt.WeaponData);
        }
    }
}