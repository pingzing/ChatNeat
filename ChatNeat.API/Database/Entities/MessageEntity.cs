using System;

namespace ChatNeat.API.Database.Entities
{
    internal class MessageEntity
    {
        public string Contents { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
