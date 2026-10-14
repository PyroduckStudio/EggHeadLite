using NUnit.Framework;
using com.pyroduck.eggheads.Runtime.Scripts.Events;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class EventManagerTests
    {
        private struct TestEvent { public int Value; }

        [SetUp]
        public void Reset() => EventManager.Clear();

        [Test]
        public void Subscribe_Publish_InvokesHandler()
        {
            int called = 0;
            EventManager.Subscribe<TestEvent>(e => called += e.Value);

            EventManager.Publish(new TestEvent { Value = 3 });

            Assert.AreEqual(3, called);
        }

        [Test]
        public void Unsubscribe_StopsFurtherInvocations()
        {
            int called = 0;
            System.Action<TestEvent> handler = e => called += 1;
            EventManager.Subscribe(handler);
            EventManager.Publish(new TestEvent());
            EventManager.Unsubscribe(handler);
            EventManager.Publish(new TestEvent());

            Assert.AreEqual(1, called);
        }

        [Test]
        public void Subscribe_NullAction_Ignored()
        {
            Assert.DoesNotThrow(() => EventManager.Subscribe<TestEvent>(null));
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventManager.Publish(new TestEvent()));
        }

        [Test]
        public void MultipleHandlers_AllInvoked()
        {
            int a = 0, b = 0;
            EventManager.Subscribe<TestEvent>(e => a = e.Value);
            EventManager.Subscribe<TestEvent>(e => b = e.Value * 2);

            EventManager.Publish(new TestEvent { Value = 5 });

            Assert.AreEqual(5, a);
            Assert.AreEqual(10, b);
        }
    }
}
