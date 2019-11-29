using System;

namespace ChatNeat.API.Database.Entities
{
    internal class UserEntity
    {
        public string Name { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}
