using System;

namespace ChatNeat.Database.Entities
{
    internal class UserEntity
    {
        public string Name { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}
