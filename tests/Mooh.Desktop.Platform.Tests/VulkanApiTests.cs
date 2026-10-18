using System;
using System.Globalization;

using Mooh.Desktop.Platform.Graphics;

using Xunit;

namespace Mooh.Desktop.Platform.Tests
{
    /// <summary>Verifies Vulkan failures are classified for renderer recovery.</summary>
    public sealed class VulkanApiTests
    {
        /// <summary>Verifies device and surface loss request renderer recovery.</summary>
        [Theory]
        [InlineData(Vk.ErrorDeviceLost)]
        [InlineData(Vk.ErrorSurfaceLostKhr)]
        [InlineData(Vk.ErrorFullScreenExclusiveModeLostExt)]
        public void ALostDeviceOrSurfaceIsSomethingTheHostRecoversFrom(int result)
        {
            GraphicsDeviceLostException lost = Assert.Throws<GraphicsDeviceLostException>(() => VulkanApi.Check(result, "vkQueuePresentKHR"));

            Assert.Contains("vkQueuePresentKHR", lost.Message, StringComparison.Ordinal);
            Assert.Contains(result.ToString(CultureInfo.InvariantCulture), lost.Message, StringComparison.Ordinal);
        }

        // An out-of-date swapchain (a resize, F11) is rebuilt by the device itself; as a loss it would
        // replace the whole device on every resize.
        /// <summary>Verifies ordinary failures retain their original classification.</summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(-2)]
        [InlineData(Vk.ErrorOutOfDateKhr)]
        public void AnyOtherFailureIsAnOrdinaryFailure(int result)
        {
            _ = Assert.Throws<InvalidOperationException>(() => VulkanApi.Check(result, "vkQueueSubmit"));
            Assert.False(VulkanApi.IsDeviceLost(result));
        }

        /// <summary>Verifies a successful Vulkan call does not throw.</summary>
        [Fact]
        public void ASucceedingCallReportsNothing()
        {
            VulkanApi.Check(Vk.Success, "vkQueueSubmit");
        }
    }
}
