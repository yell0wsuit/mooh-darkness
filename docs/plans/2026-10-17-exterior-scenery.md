# Exterior scenery implementation plan

**Goal:** Fill visible space outside the authored level with continuous terrain and scattered rocks, entirely in memory.

**Architecture:** A presentation-only generator samples the level's terrain palette and boundary cells. A coordinate hash selects stable variants and pockets containing decorative rocks, using an independent seed. The camera determines the finite region to generate; the gameplay grid and JSON remain unchanged. Scenery joins the ordinary sprite sort and follows world rotation.

**Tech stack:** C#/.NET 10, shared desktop/browser presentation, xUnit.

1. Add regression tests in `tests/Mooh.Presentation.Tests/ExteriorSceneryTests.cs` for exterior-only coverage in wide and tall views, mixed terrain and rocks, stable resizing, unchanged simulation state, and completed/in-flight rotation. Run the presentation suite and confirm the missing feature fails.
2. Implement `src/Mooh.Presentation/Scene/ExteriorScenery.cs`. Use only static terrain and rock animation frames; preserve nearby edge materials, use the sampled terrain palette further out, and never create simulation entities. Generate only the visible region plus sprite overscan. Run the new tests.
3. Integrate with `ScenePresenter` using an optional camera and initialize the generator in `DesktopGame`, shared by desktop and browser. Keep the existing presenter behavior for callers without scenery. Test the integrated draw list and run presentation and desktop regressions.
4. Build desktop, run formatting/diff checks, and inspect rendered captures at wide, tall, and original aspect ratios. Update the README with the runtime-only behavior and report verification limits.
