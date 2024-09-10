using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;

namespace DokkanDailyTests
{
    public class DailyTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void CanBuildCharacterDb()
        {
            IEnumerable<Unit> list = [];

            Assert.DoesNotThrow(() => list = DDHelper.BuildCharacterDb());
            Assert.That(list, Is.Not.Null);
            Assert.That(list, Is.Not.Empty);
        }

        [Test]
        public void TestHelpers()
        {
            Assert.DoesNotThrow(() => DDConstants.GetUnit("Tenacious Secret Plan", "Super Vegeta"));
        }
    }
}