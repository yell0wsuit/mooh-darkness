using System;

namespace Mooh.Core.Interactions
{
    [Flags]
    public enum InteractionMask
    {
        None = 0,
        BlocksMovement = 1 << 0,
        PlayerPushable = 1 << 1,
        CanBePushedThrough = 1 << 2,
        PlayerEdible = 1 << 3,
        MovementLocked = 1 << 4,
    }
}
