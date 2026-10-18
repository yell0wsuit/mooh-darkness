namespace Mooh.Core.Hazards
{
    /// <summary>Identifies an entity's role in the recovered fire-contact rule.</summary>
    /// <remarks>
    /// The original expresses the rule with two separate declarations. Fire itself registers
    /// <c>EvObjHere=_ObjHere_Fire</c>, so the handler runs whenever another object enters the
    /// fire's cell; a type that should perish together with the fire instead sets the
    /// <c>bFireKill</c> flag its handler reads. Both are modelled here, because they are two
    /// halves of one interaction and no placed type carries both.
    /// </remarks>
    public enum FireContact
    {
        /// <summary>
        /// The entity is fire. Another entity arriving in its cell runs the recovered handler.
        /// </summary>
        Source,

        /// <summary>
        /// The recovered <c>bFireKill</c> flag: contact destroys the entity and the fire
        /// together. The bubble is the only placed carrier.
        /// </summary>
        MutualDestruction,
    }
}
