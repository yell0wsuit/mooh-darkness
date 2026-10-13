using Mooh.Core.Entities;

namespace Mooh.Presentation.Scene
{
    /// <summary>One frame to draw this update.</summary>
    /// <param name="Entity">The entity the frame shows.</param>
    /// <param name="Page">The sprite page the frame is on.</param>
    /// <param name="Frame">The frame name on that page.</param>
    /// <param name="X">The drawn column, fractional while the entity moves.</param>
    /// <param name="Y">The drawn row, fractional while the entity moves.</param>
    /// <param name="Pass">
    /// The original's render pass, <c>RPass</c>: lower passes draw first. The start door is -1,
    /// ordinary objects 0, terrain, pipes and message tables 1, and the teleporting player 2.
    /// </param>
    public readonly record struct SceneSprite(EntityId Entity, string Page, string Frame, double X, double Y, int Pass);
}
