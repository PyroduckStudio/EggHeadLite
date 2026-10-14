using System.Collections.Generic;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Data
{
    [CreateAssetMenu(fileName = "VisualGroup", menuName = "PyroDuck/EggHeads/Visual Group")]
    public class VisualGroupsDataSO : ScriptableObject
    {
        public string Name;
        public bool IsSubGroup;
        public List<VisualDataSO> VisualList;
        public VisualType VisualType;
    }
}

