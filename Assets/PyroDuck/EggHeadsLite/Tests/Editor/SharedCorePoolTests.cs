using System.Reflection;
using com.pyroduck.eggheads.Runtime.Scripts.Pool;
using NUnit.Framework;
using UnityEngine;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public sealed class SharedCorePoolTests
    {
        [Test]
        public void GetThrowable_RegistersSuppliedPrefabWithoutPrewarm()
        {
            var root = new GameObject("pool");
            var template = new GameObject("throwable");
            try
            {
                var pool = root.AddComponent<ProjectilePool>();
                typeof(ProjectilePool).GetMethod("RegisterPools", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pool, null);
                var prefab = template.AddComponent<ThrowableProjectile>();
                var result = pool.GetThrowable(prefab, new Vector3(4, 5, 0));
                Assert.IsNotNull(result);
                Assert.AreNotSame(prefab, result);
                Assert.AreEqual(new Vector3(4, 5, 0), result.transform.position);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(template); }
        }
    }
}
