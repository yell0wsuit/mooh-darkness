namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// The recovered backdrop layer sets of <c>CBackLayer.ModInit</c>: each puts a tile layer over
    /// the shared <c>BackA_1_01</c> image.
    /// </summary>
    public enum BackdropStyle
    {
        /// <summary><c>LayerTypeNrm</c>: <c>SpriteBack</c> tiles.</summary>
        Normal,

        /// <summary><c>LayerTypeTrop</c>: <c>SpriteBackTrop</c> tiles.</summary>
        Tropical,

        /// <summary><c>LayerTypeDesert</c>: <c>SpriteBackDesert</c> tiles.</summary>
        Desert,
    }
}
