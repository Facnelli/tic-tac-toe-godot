using NUnit.Framework;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;

namespace TicTacToeRoguelike.Tests.Runes
{
    public sealed class RuneInventoryStateTests
    {
        [Test]
        public void SixthNormalRune_IsRejected()
        {
            RuneInventoryState inventory =
                new RuneInventoryState(
                    ScoreActor.Player);

            for (int i = 0; i < 5; i++)
            {
                Assert.That(
                    inventory.TryAdd(
                        CreateRune($"normal-{i}")),
                    Is.EqualTo(
                        RuneInventoryAddResult.Added));
            }

            Assert.That(
                inventory.TryAdd(
                    CreateRune("sixth")),
                Is.EqualTo(
                    RuneInventoryAddResult
                        .CapacityReached));

            Assert.That(
                inventory.OccupiedSlots,
                Is.EqualTo(5));

            Assert.That(
                inventory.Count,
                Is.EqualTo(5));
        }

        [Test]
        public void IntangibleRune_DoesNotConsumeSlot()
        {
            RuneInventoryState inventory =
                new RuneInventoryState(
                    ScoreActor.Player);

            for (int i = 0; i < 5; i++)
            {
                inventory.TryAdd(
                    CreateRune($"normal-{i}"));
            }

            RuneInstance intangible =
                CreateRune(
                    "intangible",
                    RuneAttributeId.Intangible);

            Assert.That(
                inventory.TryAdd(intangible),
                Is.EqualTo(
                    RuneInventoryAddResult.Added));

            Assert.That(
                inventory.OccupiedSlots,
                Is.EqualTo(5));

            Assert.That(
                inventory.Count,
                Is.EqualTo(6));

            Assert.That(
                inventory.Contains(
                    intangible.InstanceId),
                Is.True);
        }

        [Test]
        public void EqualDefinitions_CanHaveDifferentInstances()
        {
            RuneDefinition definition =
                CreateDefinition("same");

            RuneInstance first =
                RuneInstance.Create(definition);

            RuneInstance second =
                RuneInstance.Create(definition);

            Assert.That(
                first.Definition,
                Is.SameAs(second.Definition));

            Assert.That(
                first.InstanceId,
                Is.Not.EqualTo(
                    second.InstanceId));
        }

        [Test]
        public void Attributes_AreUnique()
        {
            RuneAttributeSet attributes =
                new RuneAttributeSet(
                    new[]
                    {
                        RuneAttributeId.Blessed,
                        RuneAttributeId.Blessed,
                        RuneAttributeId.Broken
                    });

            Assert.That(
                attributes.Count,
                Is.EqualTo(2));

            Assert.That(
                attributes.Contains(
                    RuneAttributeId.Blessed),
                Is.True);

            Assert.That(
                attributes.Contains(
                    RuneAttributeId.Broken),
                Is.True);
        }

        [Test]
        public void PlayerAndEnemy_UseSameDefinitionType()
        {
            RuneDefinition shared =
                CreateDefinition("shared");

            RuneInventoryState player =
                new RuneInventoryState(
                    ScoreActor.Player);

            RuneInventoryState enemy =
                new RuneInventoryState(
                    ScoreActor.Enemy);

            RuneInstance playerRune =
                RuneInstance.Create(shared);

            RuneInstance enemyRune =
                RuneInstance.Create(shared);

            player.TryAdd(playerRune);
            enemy.TryAdd(enemyRune);

            Assert.That(
                player.Runes[0].Definition,
                Is.SameAs(
                    enemy.Runes[0].Definition));
        }

        [Test]
        public void Removal_ReportsInstanceAndReason()
        {
            RuneInventoryState inventory =
                new RuneInventoryState(
                    ScoreActor.Player);

            RuneInstance rune =
                CreateRune("remove-me");

            inventory.TryAdd(rune);

            bool removed =
                inventory.TryRemove(
                    rune.InstanceId,
                    RuneRemovalReason.Sacrificed,
                    out RuneRemovalResult result);

            Assert.That(removed, Is.True);
            Assert.That(
                result.Rune,
                Is.SameAs(rune));
            Assert.That(
                result.Reason,
                Is.EqualTo(
                    RuneRemovalReason.Sacrificed));
            Assert.That(
                inventory.Count,
                Is.Zero);
        }

        [Test]
        public void DuplicateInstance_IsRejected()
        {
            RuneInventoryState inventory =
                new RuneInventoryState(
                    ScoreActor.Enemy);

            RuneInstance rune =
                CreateRune("duplicate");

            Assert.That(
                inventory.TryAdd(rune),
                Is.EqualTo(
                    RuneInventoryAddResult.Added));

            Assert.That(
                inventory.TryAdd(rune),
                Is.EqualTo(
                    RuneInventoryAddResult
                        .DuplicateInstance));
        }

        private static RuneInstance CreateRune(
            string id,
            params RuneAttributeId[] attributes)
        {
            return RuneInstance.Create(
                CreateDefinition(id),
                attributes);
        }

        private static RuneDefinition CreateDefinition(
            string id)
        {
            return new RuneDefinition(
                new RuneDefinitionId(id),
                $"Rune {id}",
                "Test rune",
                RuneRarity.Common);
        }
    }
}
