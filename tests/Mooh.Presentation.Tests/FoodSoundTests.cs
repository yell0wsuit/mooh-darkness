using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks the delayed second food sound using recovered pickup registrations.</summary>
    public sealed class FoodSoundTests
    {
        /// <summary>Each food plays its initial bite and a single second bite after 200–250 ms.</summary>
        [Theory]
        [InlineData("OEat01")]
        [InlineData("OEat02")]
        [InlineData("OEat03")]
        public void FoodSchedulesTheSecondBite(string type)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, (type, 3, 1)));
            ScenePresenter presenter = harness.Presenter;
            Core.Entities.Entity food = harness.Level.GetEntity(200);
            presenter.Update(harness.Level.World, [], 0);
            presenter.Update(harness.Level.World,
                [new PlayerAte(harness.Player.Id, food.Id), new PlayerAte(harness.Player.Id, food.Id)], 1);
            _ = Assert.Single(presenter.Sounds, cue => cue.Alias == "EatEat");
            Assert.DoesNotContain(presenter.Sounds, cue => cue.Alias == "EatEat2");

            presenter.Update(harness.Level.World, [], 0.199);
            Assert.DoesNotContain(presenter.Sounds, cue => cue.Alias == "EatEat2");
            presenter.Update(harness.Level.World, [], 0);
            Assert.DoesNotContain(presenter.Sounds, cue => cue.Alias == "EatEat2");
            presenter.Update(harness.Level.World, [], 0.051);
            _ = Assert.Single(presenter.Sounds, cue => cue.Alias == "EatEat2");
            presenter.Update(harness.Level.World, [], 1);
            Assert.DoesNotContain(presenter.Sounds, cue => cue.Alias == "EatEat2");
        }

        /// <summary>Both edible enemy families play their absorption sound.</summary>
        [Theory]
        [InlineData("MGrR")]
        [InlineData("MBlueR")]
        public void EatenEnemyPlaysSmallGet(string type)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, (type, 3, 1)));
            Core.Entities.Entity enemy = harness.Level.GetEntity(200);
            harness.Presenter.Update(harness.Level.World, [], 0);
            harness.Presenter.Update(harness.Level.World, [new PlayerAte(harness.Player.Id, enemy.Id)], 0);

            Assert.Contains(harness.Presenter.Sounds, cue => cue.Alias == "SmallGet");
            Assert.DoesNotContain(harness.Presenter.Sounds, cue => cue.Alias == "CellBreak");
        }
    }
}
