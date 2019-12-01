using System;

namespace ChatNeat.API.Database.Entities
{
    public class UserEntity
    {
        public string Name { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}
