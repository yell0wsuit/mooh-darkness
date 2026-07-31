using Xunit;

namespace Mooh.Core.Tests
{
    /// <summary>
    /// Verifies that the test host can discover and execute project tests.
    /// </summary>
    public sealed class ProjectSmokeTests
    {
        /// <summary>Confirms that the test host starts successfully.</summary>
        [Fact]
        public void TestHostStarts()
        {
            Assert.True(true);
        }
    }
}
