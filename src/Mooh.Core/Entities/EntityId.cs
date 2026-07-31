using System;

namespace Mooh.Core.Entities
{
    public readonly record struct EntityId(Guid Value)
    {
        public static EntityId New()
        {
            return new EntityId(Guid.NewGuid());
        }
    }
}
