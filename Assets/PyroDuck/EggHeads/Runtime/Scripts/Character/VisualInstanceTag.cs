using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Data;

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    /// <summary>
    /// Runtime marker attached by <see cref="CharacterVisualController"/> to every
    /// spawned visual. Lets systems (like <c>CharacterSerializer</c>) map a live
    /// <see cref="GameObject"/> back to the authoring <see cref="VisualDataSO"/>
    /// without relying on the fragile <c>name.Contains(...)</c> trick.
    /// </summary>
    [DisallowMultipleComponent]
    public class VisualInstanceTag : MonoBehaviour
    {
        [SerializeField] private VisualDataSO source;

        public VisualDataSO Source => source;

        public void Bind(VisualDataSO data)
        {
            source = data;
        }
    }
}
