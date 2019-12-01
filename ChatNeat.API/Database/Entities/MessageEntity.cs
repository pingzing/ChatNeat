using System;

namespace ChatNeat.API.Database.Entities
{
    public class MessageEntity
    {
        public Guid SenderId { get; set; }
        public string Contents { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        // Note: With these three properties, Contents would still fit into a Table Storage
        // entity with up to 524,196 characters.
        // SignalR, however, has a max limit size of about 32 KiB, or 16,384 UTF-16 characters.
        // That's about 4 or 5 pages of English text.
        public static int MaxMessageSize => 16_000;
    }
}
