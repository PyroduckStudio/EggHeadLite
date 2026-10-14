using System.Collections.Generic;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using NUnit.Framework;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class CharacterSerializerTests
    {
        [Test]
        public void Save_EmptyController_ProducesValidEmptyJson()
        {
            var groups = new List<VisualGroupsDataSO>();
            string json = CharacterSerializer.SaveToJson(null, null, groups);

            Assert.IsFalse(string.IsNullOrEmpty(json));
            StringAssert.Contains("Parts", json);
        }

        [Test]
        public void Load_NullJson_DoesNotThrow()
        {
            var groups = new List<VisualGroupsDataSO>();
            Assert.DoesNotThrow(() =>
                CharacterSerializer.LoadFromJson(null, null, null, groups));
        }

        [Test]
        public void Load_EmptyJson_DoesNotThrow()
        {
            var groups = new List<VisualGroupsDataSO>();
            Assert.DoesNotThrow(() =>
                CharacterSerializer.LoadFromJson(string.Empty, null, null, groups));
        }

        [Test]
        public void Load_MalformedJson_DoesNotThrow()
        {
            var groups = new List<VisualGroupsDataSO>();
            Assert.DoesNotThrow(() =>
                CharacterSerializer.LoadFromJson("{invalid", null, null, groups));
        }
    }
}
