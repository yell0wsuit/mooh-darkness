namespace Mooh.Core.Enemies
{
    /// <summary>Identifies the current blue-monster behavior phase.</summary>
    public enum BlueMonsterState
    {
        /// <summary>The monster is ready to choose its next action.</summary>
        Deciding,

        /// <summary>The monster is ready for the restricted decision following a left turn.</summary>
        DecidingAfterLeftTurn,

        /// <summary>The monster is walking toward an adjacent cell.</summary>
        Walking,

        /// <summary>The monster is turning toward its left-hand cell.</summary>
        TurningLeft,

        /// <summary>The monster is turning toward its right-hand cell.</summary>
        TurningRight,

        /// <summary>The monster is winding up a strike.</summary>
        AttackWindup,

        /// <summary>The monster is recovering and turning around after a strike.</summary>
        AttackRecovery,

        /// <summary>The monster is completing the final facing-changing turnaround phase.</summary>
        AttackTurnaround,
    }
}
