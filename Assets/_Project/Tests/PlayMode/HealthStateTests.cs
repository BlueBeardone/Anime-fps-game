using AnimeFpsGame.Combat;
using NUnit.Framework;

namespace AnimeFpsGame.Tests
{
    public class HealthStateTests
    {
        [Test]
        public void HealthStateClampsAndDiesOnce()
        {
            var health = new HealthState(100f);
            Assert.IsFalse(health.ApplyDamage(60f), "Non-lethal damage does not report a death");
            Assert.AreEqual(40f, health.Current, 0.001f);
            Assert.IsFalse(health.IsDead);
            Assert.IsTrue(health.ApplyDamage(60f), "Lethal damage reports the death transition");
            Assert.AreEqual(0f, health.Current, 0.001f);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void HealthStateIgnoresDamageWhileDead()
        {
            var health = new HealthState(100f);
            Assert.IsTrue(health.ApplyDamage(100f));
            Assert.IsFalse(health.ApplyDamage(10f), "A dead target must not report another death");
            Assert.AreEqual(0f, health.Current, 0.001f);
        }

        [Test]
        public void ReviveRestoresFullHealth()
        {
            var health = new HealthState(100f);
            health.ApplyDamage(100f);
            health.Revive();
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(100f, health.Current, 0.001f);
        }
    }
}
