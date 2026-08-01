namespace Mooh.Core.Combat
{
    /// <summary>Identifies the semantic source of health damage.</summary>
    public enum DamageKind
    {
        /// <summary>Damage caused by a direct enemy strike.</summary>
        Strike,

        /// <summary>Damage caused by fire contact.</summary>
        Fire,

        /// <summary>Damage caused by an ordinary shake or impact.</summary>
        Shake,

        /// <summary>Damage caused by a hard shake or impact.</summary>
        HardShake,

        /// <summary>Damage caused by an explosion.</summary>
        Explosion,
    }
}
