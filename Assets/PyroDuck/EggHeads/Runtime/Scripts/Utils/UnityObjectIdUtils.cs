using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Utils
{
    /// <summary>
    /// Provides an integer identifier for Unity objects across supported editor versions.
    /// </summary>
    public static class UnityObjectIdUtils
    {
        public static ulong GetObjectId(this Object unityObject)
        {
#if UNITY_6000_5_OR_NEWER
            return EntityId.ToULong(unityObject.GetEntityId());
#else
            return unchecked((ulong)(uint)unityObject.GetInstanceID());
#endif
        }
    }
}
