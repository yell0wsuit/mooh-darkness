using System;
using System.Collections.Generic;

namespace Mooh.Presentation.Screens
{
    /// <summary>Shows the original instruction image, loading wipe and ready prompt.</summary>
    public sealed class InstructionsPresentation
    {
        private readonly Func<string, string> _localize;
        private double _clockSeconds;

        /// <summary>Creates the instruction screen with localized loading and key prompts.</summary>
        /// <param name="localize">Looks up the original text identifiers.</param>
        public InstructionsPresentation(Func<string, string> localize)
        {
            ArgumentNullException.ThrowIfNull(localize);
            _localize = localize;
            Rebuild();
        }

        /// <summary>Gets the externally reported loading fraction.</summary>
        public double Progress
        {
            get; private set;
        }

        /// <summary>Gets whether loading finished and the screen is waiting for a key press.</summary>
        public bool IsReady
        {
            get; private set;
        }

        /// <summary>Gets the image, clipped loading frames, prompt and optional walking beetle.</summary>
        public IReadOnlyList<ScreenElement> Elements { get; private set; } = [];

        /// <summary>Updates actual loading progress independently from animation time.</summary>
        /// <param name="progress">The loading fraction, clamped between zero and one.</param>
        /// <param name="isReady">Whether the loader finished and input may continue.</param>
        public void SetProgress(double progress, bool isReady = false)
        {
            if (!double.IsFinite(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            IsReady = isReady;
            Progress = isReady ? 1 : Math.Clamp(progress, 0, 1);
            Rebuild();
        }

        /// <summary>Advances only the loading beetle's sixteen-frame walk.</summary>
        /// <param name="seconds">The elapsed animation seconds.</param>
        public void Advance(double seconds)
        {
            if (!double.IsFinite(seconds) || seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            _clockSeconds = (_clockSeconds + seconds) % 1.2;
            Rebuild();
        }

        private void Rebuild()
        {
            double split = 99 + Math.Round(Progress * 586);
            List<ScreenElement> elements =
            [
                new ScreenImage("Instr.jpg", 0, 0, 800, 600),
                new ScreenClip(0, 0, split, 600,
                [
                    new ScreenSprite("SpriteInstr", "Bk_OLoad10"),
                    new ScreenSprite("SpriteInstr", "Bk_OLoad11"),
                ]),
                new ScreenClip(split, 0, 800 - split, 600,
                [
                    new ScreenSprite("SpriteInstr", "Bk_OLoad00"),
                    new ScreenSprite("SpriteInstr", "Bk_OLoad01"),
                ]),
                new ScreenText(_localize(IsReady ? "GameInstrKey" : "GameInstrLoad"), 400, 565, "MenuFntEncTxt"),
            ];
            if (!IsReady)
            {
                int frame = 10 + ((int)Math.Floor((_clockSeconds * 1000 / 75) + 1e-10) % 16);
                elements.Add(new ScreenSprite("SpriteInstr", $"BWlk{frame:D2}", split, 575));
            }

            Elements = elements.AsReadOnly();
        }
    }
}
