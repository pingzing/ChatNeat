using System;

namespace ChatNeat.API.Database.Entities
{
    internal class MessageEntity
    {
        public Guid SenderId { get; set; }
        public string Contents { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
