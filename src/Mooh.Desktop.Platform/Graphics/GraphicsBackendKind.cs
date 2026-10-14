namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>A desktop renderer, named as the window title and the console print it.</summary>
    public enum GraphicsBackendKind
    {
        /// <summary>Skia on Metal, through SDL's Metal view (macOS).</summary>
        Metal,

        /// <summary>Skia on a core OpenGL 3.2 context from the system driver.</summary>
        OpenGL,

        /// <summary>Skia drawing on the CPU, presented through SDL's software renderer.</summary>
        Software,
    }
}
