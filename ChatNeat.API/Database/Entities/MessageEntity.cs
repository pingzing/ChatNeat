using System;

namespace ChatNeat.Database.Entities
{
    internal class MessageEntity
    {
        public string Contents { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
