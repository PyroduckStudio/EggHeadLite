using com.pyroduck.eggheads.Runtime.Scripts.Character;
using NUnit.Framework;
using UnityEngine;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class HealthComponentTests
    {
        private GameObject _go;
        private HealthComponent _health;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("health");
            _health = _go.AddComponent<HealthComponent>();
            // Edit Mode does not run MonoBehaviour.Awake for this component.
            _health.ResetHealth();
            _health.BroadcastInvoke();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void ApplyDamage_ReducesHealth()
        {
            float before = _health.Health;
            _health.ApplyDamage(20f, _go);
            Assert.AreEqual(before - 20f, _health.Health, 0.001f);
        }

        [Test]
        public void ApplyDamage_NegativeAmount_Ignored()
        {
            float before = _health.Health;
            _health.ApplyDamage(-10f, _go);
            Assert.AreEqual(before, _health.Health);
        }

        [Test]
        public void ApplyDamage_CannotGoBelowZero()
        {
            _health.ApplyDamage(9999f, _go);
            Assert.AreEqual(0f, _health.Health);
            Assert.IsFalse(_health.IsAlive);
        }

        [Test]
        public void ApplyDamage_Dead_DoesNothing()
        {
            _health.ApplyDamage(9999f, _go);
            _health.ApplyDamage(1f, _go);
            Assert.AreEqual(0f, _health.Health);
        }

        [Test]
        public void ApplyDamage_SameSourceWithinCooldown_Ignored()
        {
            float before = _health.Health;

            _health.ApplyDamage(10f, _go);
            _health.ApplyDamage(10f, _go);

            Assert.AreEqual(before - 10f, _health.Health, 0.001f);
        }

        [Test]
        public void ApplyDamage_DifferentSourcesWithinCooldown_AppliesBoth()
        {
            var other = new GameObject("other-source");
            try
            {
                float before = _health.Health;

                _health.ApplyDamage(10f, _go);
                _health.ApplyDamage(10f, other);

                Assert.AreEqual(before - 20f, _health.Health, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ResetHealth_RestoresToMax()
        {
            _health.ApplyDamage(9999f, _go);
            _health.ResetHealth();
            Assert.AreEqual(_health.MaxHealth, _health.Health);
            Assert.IsTrue(_health.IsAlive);
        }

        [Test]
        public void ResetHealth_ClearsDamageCooldown()
        {
            _health.ApplyDamage(9999f, _go);
            _health.ResetHealth();

            _health.ApplyDamage(10f, _go);

            Assert.AreEqual(_health.MaxHealth - 10f, _health.Health, 0.001f);
        }
    }

    // Helper extension to disable the event broadcast during tests so EventManager
    // subscribers from previous suites don't interact.
    internal static class HealthComponentTestExtensions
    {
        public static void BroadcastInvoke(this HealthComponent health)
        {
            var field = typeof(HealthComponent).GetField("broadcastDamageEvent",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(health, false);
        }
    }
}
