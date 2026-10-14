using System.Collections.Generic;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using TMPro;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.UI
{
    /// <summary>
    /// Cycles through EggHeadDatabaseSO characters with previous/next buttons
    /// and applies the selected character prefab to EggHeadController.
    /// Attach to a Canvas GameObject that is active at game-start.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [SerializeField] private EggHeadController targetController;
        [SerializeField] private EggHeadDatabaseSO database;
        [SerializeField] private UnityEngine.UI.Button previousCharacterButton;
        [SerializeField] private UnityEngine.UI.Button nextCharacterButton;
        [SerializeField] private TMP_Text characterNameLabel;

        private readonly List<GameObject> _prefabs = new List<GameObject>();
        private int _selectedIndex;

        public event System.Action<GameObject> OnCharacterSelected;

        private void Awake()
        {
            LoadPrefabsFromDatabase();
            RefreshSelectionUI();
        }

        private void Start()
        {
            if (!SelectFirstCharacterForUninitializedController())
                SyncSelectionWithActiveController();
        }

        private void OnEnable()
        {
            if (previousCharacterButton != null)
            {
                previousCharacterButton.onClick.RemoveListener(SelectPreviousCharacter);
                previousCharacterButton.onClick.AddListener(SelectPreviousCharacter);
            }
            if (nextCharacterButton != null)
            {
                nextCharacterButton.onClick.RemoveListener(SelectNextCharacter);
                nextCharacterButton.onClick.AddListener(SelectNextCharacter);
            }

            SyncSelectionWithActiveController();
        }

        private void OnDisable()
        {
            if (previousCharacterButton != null)
                previousCharacterButton.onClick.RemoveListener(SelectPreviousCharacter);
            if (nextCharacterButton != null)
                nextCharacterButton.onClick.RemoveListener(SelectNextCharacter);
        }

        public static bool TrySelectFirstCharacter()
        {
#if UNITY_6000_5_OR_NEWER
            var instance = FindAnyObjectByType<CharacterSelectUI>();
#else
            var instance = FindFirstObjectByType<CharacterSelectUI>();
#endif
            return instance != null && instance.SelectCharacter(0, true);
        }

        private void LoadPrefabsFromDatabase()
        {
            _prefabs.Clear();
            if (database?.characterPrefabs != null)
            {
                foreach (var prefab in database.characterPrefabs)
                {
                    if (prefab != null && !_prefabs.Contains(prefab))
                        _prefabs.Add(prefab);
                }
            }

            if (_prefabs.Count == 0)
                Debug.LogWarning("[CharacterSelectUI] No character prefabs assigned in EggHeadDatabaseSO.");
        }

        private void RefreshSelectionUI()
        {
            if (characterNameLabel != null)
                characterNameLabel.text = _prefabs.Count > 0 ? _prefabs[_selectedIndex].name : string.Empty;

            bool canCycle = _prefabs.Count > 1;
            if (previousCharacterButton != null)
                previousCharacterButton.interactable = canCycle;
            if (nextCharacterButton != null)
                nextCharacterButton.interactable = canCycle;
        }

        private void SyncSelectionWithActiveController()
        {
            var controller = FindEggHeadController();
            if (controller != null && controller.CharacterBasePrefab != null)
            {
                int index = FindPrefabIndex(controller.CharacterBasePrefab);
                if (index >= 0)
                    _selectedIndex = index;
            }

            RefreshSelectionUI();
        }

        private int FindPrefabIndex(GameObject prefab)
        {
            int index = _prefabs.IndexOf(prefab);
            if (index >= 0) return index;

            for (int i = 0; i < _prefabs.Count; i++)
            {
                if (_prefabs[i] != null && _prefabs[i].name == prefab.name)
                    return i;
            }

            return -1;
        }

        private bool SelectFirstCharacterForUninitializedController()
        {
            if (_prefabs.Count == 0) return false;

            var controller = FindEggHeadController();
            if (controller == null || controller.CharacterBasePrefab != null) return false;

            return SelectCharacter(0, true);
        }

        private bool SelectCharacter(int index, bool notify)
        {
            if (_prefabs.Count == 0) return false;

            index = Mathf.Clamp(index, 0, _prefabs.Count - 1);

            _selectedIndex = index;
            RefreshSelectionUI();

            if (notify)
            {
                if (targetController != null) targetController.SetCharacterPrefab(_prefabs[index]);
                else EventManager.Publish(new CharacterSelectedEvent { Prefab = _prefabs[index] });
                OnCharacterSelected?.Invoke(_prefabs[index]);
            }

            return true;
        }

        private EggHeadController FindEggHeadController()
        {
            if (targetController != null) return targetController;
#if UNITY_6000_5_OR_NEWER
            return FindAnyObjectByType<EggHeadController>();
#else
            return FindFirstObjectByType<EggHeadController>();
#endif
        }

        public void SelectPreviousCharacter()
        {
            CycleCharacter(-1);
        }

        public void SelectNextCharacter()
        {
            CycleCharacter(1);
        }

        private void CycleCharacter(int direction)
        {
            if (_prefabs.Count <= 1) return;

            SyncSelectionWithActiveController();
            int index = (_selectedIndex + direction + _prefabs.Count) % _prefabs.Count;
            SelectCharacter(index, true);
        }
    }
}