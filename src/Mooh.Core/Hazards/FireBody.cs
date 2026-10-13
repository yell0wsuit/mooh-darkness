using System;

namespace Mooh.Core.Hazards
{
    /// <summary>Describes an entity's part in the recovered fire-contact rule.</summary>
    /// <remarks>
    /// <para>
    /// The recovered <c>_ObjHere_Fire</c> has exactly three outcomes, decided by who arrived.
    /// The player is burned for <c>Compl.HFire</c> and the fire is spent; a
    /// <see cref="FireContact.MutualDestruction"/> carrier and the fire destroy each other;
    /// anything else is consumed while the fire stays. An entity without this component takes
    /// the last branch, so nothing has to be declared for the ordinary case.
    /// </para>
    /// <para>
    /// The handler is the fire's own event, so it never runs when the fire itself arrives
    /// somewhere. Fire falling onto a standing player harms nobody.
    /// </para>
    /// </remarks>
    /// <param name="contact">The entity's role in the rule.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="contact"/> is undefined.</exception>
    public sealed class FireBody(FireContact contact)
    {
        /// <summary>Gets the entity's role in the recovered fire-contact rule.</summary>
        public FireContact Contact
        {
            get;
        } = Enum.IsDefined(contact)
            ? contact
            : throw new ArgumentOutOfRangeException(nameof(contact), contact, null);
    }
}
