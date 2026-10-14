using System.Collections.Generic;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Data
{
    [CreateAssetMenu(fileName = "EggHeadDatabase", menuName = "PyroDuck/EggHeads/EggHead Database")]
    public class EggHeadDatabaseSO : ScriptableObject
    {
        [Tooltip("Contains all the major visual categories, such as Eyes, Hair, Hats, etc.")]
        public List<VisualGroupsDataSO> visualGroups = new List<VisualGroupsDataSO>();
        
        [Tooltip("All body parts available for generation/validation.")]
        public List<VisualDataSO> allVisuals = new List<VisualDataSO>();

        [Tooltip("Playable character prefabs available to the runtime character selector.")]
        public List<GameObject> characterPrefabs = new List<GameObject>();

        public bool AddCharacterPrefab(GameObject prefab)
        {
            if (prefab == null)
                return false;

            if (characterPrefabs == null)
                characterPrefabs = new List<GameObject>();

            if (characterPrefabs.Contains(prefab))
                return false;

            characterPrefabs.Add(prefab);
            return true;
        }

        public VisualGroupsDataSO GetGroup(Enums.VisualType type)
        {
            return visualGroups.Find(g => g != null && g.VisualType == type);
        }
    }
}

