using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Grid;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the recovered animation tables load into typed clips.</summary>
    public sealed class AnimationLibraryTests
    {
        private static readonly Lazy<AnimationLibrary> Library = new(() =>
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "animations", "animations.json"));
            return AnimationLibrary.Load(stream);
        });

        /// <summary>Verifies a player walk state carries its frames, flying frames, timing and loop.</summary>
        [Fact]
        public void APlayerWalkStateCarriesItsRecoveredClip()
        {
            AnimationTable player = Library.Value.Table(AnimationLibrary.PlayerTable);

            AnimationClip walk = player.States["st_wlkleft"];

            Assert.Equal("SpritePlayer", player.Page);
            Assert.Equal([.. Enumerable.Range(10, 16).Select(index => $"OWlkLeft{index:00}")], walk.Variants.Single());
            Assert.Equal(10, walk.FlyVariants!.Single().Count);
            Assert.Equal([25], walk.FrameMilliseconds);
            Assert.Equal("st_wlkleft", walk.Next);
            Assert.Equal(Direction.Left, walk.Direction);
        }

        /// <summary>Verifies the bio mass idle keeps its three per-type variants and its two speeds.</summary>
        [Fact]
        public void TheBioMassIdleKeepsItsVariantsAndSpeeds()
        {
            AnimationClip stay = Library.Value.Table(AnimationLibrary.BioMassTable).States["st_stay"];

            Assert.Equal(3, stay.Variants.Count);
            Assert.All(stay.Variants, variant => Assert.Equal(20, variant.Count));
            Assert.Equal([45, 55], stay.FrameMilliseconds);
            Assert.Equal("_AEnd_SLook", stay.EndHandler);
        }

        /// <summary>Verifies a placed object's table comes from its archival registration.</summary>
        [Fact]
        public void APlacedObjectTableComesFromItsRegistration()
        {
            AnimationTable bubble = Library.Value.Table("OBubble");

            Assert.Equal("SpriteObjBoublle", bubble.Page);
            Assert.Equal(40, bubble.States["st_stay"].Variants.Single().Count);
            Assert.Equal([50], bubble.States["st_stay"].FrameMilliseconds);
        }

        /// <summary>
        /// Verifies the message table, whose <c>CGameObjTable</c> inherits its constructors from
        /// <c>CGameObjFSABase</c>, still carries its idle loop and its still read frame.
        /// </summary>
        [Fact]
        public void TheMessageTableKeepsItsInheritedStates()
        {
            AnimationTable table = Library.Value.Table("OTable");

            Assert.Equal("SpriteObjTable", table.Page);
            Assert.Equal(10, table.States["st_stay"].Variants.Single().Count);
            Assert.Equal([75], table.States["st_stay"].FrameMilliseconds);
            Assert.Equal(["OSt00"], table.States["st_work"].Variants.Single());
            Assert.True(table.States["st_work"].IsStill);
        }

        /// <summary>
        /// Verifies a registration's look: terrain's random depth shift, <c>fShZ=CellSh</c>, and the
        /// backdrop it votes for, <c>BackType</c>.
        /// </summary>
        [Fact]
        public void ARegistrationCarriesItsLook()
        {
            Assert.True(Library.Value.TryGetLook("JCellsEat01", out ObjectLook jungle));
            Assert.Equal(BackdropStyle.Tropical, jungle.Backdrop);
            Assert.Equal((-0.05, 0.05), jungle.DepthRange);

            Assert.True(Library.Value.TryGetLook("CellsMetal01", out ObjectLook metal));
            Assert.Null(metal.Backdrop);
            _ = Assert.NotNull(metal.DepthRange);

            Assert.True(Library.Value.TryGetLook("OBubble", out ObjectLook bubble));
            Assert.Null(bubble.Backdrop);
            Assert.Null(bubble.DepthRange);
        }

        /// <summary>
        /// Verifies a state's shadow, its <c>DInds</c>: the player's and a pickup's name the
        /// game module's <c>SfxShMed</c> and <c>SfxShBig</c> on <c>SpriteParts</c>; the tunnel
        /// states carry none.
        /// </summary>
        [Fact]
        public void AStateCarriesItsShadow()
        {
            AnimationClip stand = Library.Value.Table(AnimationLibrary.PlayerTable).States["st_stbottom"];
            AnimationClip tunnel = Library.Value.Table(AnimationLibrary.PlayerTable).States["st_tonmoveleft"];
            AnimationClip bubble = Library.Value.Table("OBubble").States["st_stay"];

            Assert.Equal("SpriteParts", stand.ShadowPage);
            Assert.Equal([.. Enumerable.Range(1, 11).Select(index => $"ShMed{index:00}")], stand.Shadow);
            Assert.Null(tunnel.Shadow);
            Assert.Equal("ShBig01", bubble.Shadow![0]);
        }

        /// <summary>
        /// Verifies a type's glows: a weapon's <c>GlowAnm</c> on its own page, a bonus's shared
        /// <c>SfxGlowBon</c>, and food's <c>OverAnm</c>; and that fall objects start at a random
        /// phase while doors and tables do not.
        /// </summary>
        [Fact]
        public void ATypeCarriesItsGlows()
        {
            Assert.True(Library.Value.TryGetLook("WBomb", out ObjectLook bomb));
            Assert.Equal("SpriteObjBombs", bomb.Glow!.Page);
            Assert.Equal(14, bomb.Glow.Frames.Count);
            Assert.Equal("GlowGr00", bomb.Glow.Frames[0]);
            Assert.Equal(0.02, bomb.Glow.FramesPerMillisecond);
            Assert.True(bomb.RandomStartPhase);

            Assert.True(Library.Value.TryGetLook("BCola", out ObjectLook cola));
            Assert.Equal(("SpriteGlow", "OGlowBon01"), (cola.Glow!.Page, cola.Glow.Frames[0]));

            Assert.True(Library.Value.TryGetLook("OEat01", out ObjectLook food));
            Assert.Equal(("SpriteObjEat", "P1_00"), (food.Overlay!.Page, food.Overlay.Frames[0]));

            Assert.True(Library.Value.TryGetLook("OTable", out ObjectLook table));
            Assert.False(table.RandomStartPhase);
            Assert.Null(table.Glow);
        }

        /// <summary>Verifies the types that receive shadows carry <c>CNT_CMaskResRShadow</c>.</summary>
        [Fact]
        public void TerrainRocksAndKeyDoorsReceiveShadows()
        {
            Assert.All(
                (string[])["CellsEat01", "JCellsSolid01", "CellsMetal01", "CellsWdTop", "ORock01", "OKDDoor2"],
                type => Assert.True(Library.Value.TryGetLook(type, out ObjectLook look) && look.ReceivesShadow, type));
            Assert.True(Library.Value.TryGetLook("OBubble", out ObjectLook bubble));
            Assert.False(bubble.ReceivesShadow);
        }

        /// <summary>Verifies terrain is one still state whose variants the editor index picks.</summary>
        [Fact]
        public void TerrainIsOneStillStateWithVariants()
        {
            AnimationClip earth = Library.Value.Table("CellsEat01").States[AnimationLibrary.StillState];

            Assert.Equal(5, earth.Variants.Count);
            Assert.All(earth.Variants, variant => Assert.Single(variant));
            Assert.True(earth.IsStill);
        }

        /// <summary>Verifies every frame of every clip exists on its sprite page.</summary>
        [Fact]
        public void EveryClipFrameExistsOnItsPage()
        {
            Dictionary<string, SpritePage> pages = [];
            List<string> missing = [];
            foreach (string id in Library.Value.TableIds)
            {
                AnimationTable table = Library.Value.Table(id);
                foreach ((string state, AnimationClip clip) in table.States)
                {
                    string pageName = clip.Page ?? table.Page;
                    if (!pages.TryGetValue(pageName, out SpritePage? page))
                    {
                        using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "sprites", pageName + ".seq"));
                        page = SpritePage.Load(pageName, stream);
                        pages[pageName] = page;
                    }

                    IEnumerable<string> frames = clip.Variants.SelectMany(variant => variant)
                        .Concat(clip.FlyVariants?.SelectMany(variant => variant) ?? []);
                    missing.AddRange(frames.Where(frame => !page.Contains(frame)).Select(frame => $"{id}/{state}/{frame}"));
                    if (clip.Shadow is IReadOnlyList<string> shadow)
                    {
                        SpritePage parts = Page(pages, clip.ShadowPage!);
                        missing.AddRange(shadow.Where(frame => !parts.Contains(frame)).Select(frame => $"{id}/{state}/shadow/{frame}"));
                    }
                }
            }

            Assert.Empty(missing);
            Assert.True(Library.Value.TableIds.Count > 60);
        }

        private static SpritePage Page(Dictionary<string, SpritePage> pages, string name)
        {
            if (!pages.TryGetValue(name, out SpritePage? page))
            {
                using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "sprites", name + ".seq"));
                page = SpritePage.Load(name, stream);
                pages[name] = page;
            }

            return page;
        }
    }
}
