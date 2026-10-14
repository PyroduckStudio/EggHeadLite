using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Data
{
    [CreateAssetMenu(fileName = "VisualData", menuName = "PyroDuck/EggHeads/Visual Data")]
    public class VisualDataSO : ScriptableObject
    {
        public string Name;
        public GameObject BodyPartPrefab;
        public Sprite IconSprite;
        public bool isColorable;
    }
}

