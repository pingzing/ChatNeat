using System;

namespace ChatNeat.API.Database.Entities
{
    public class MessageEntity
    {
        public Guid SenderId { get; set; }
        public string SenderName { get; set; }
        public string Contents { get; set; }
        public DateTimeOffset SentTimestamp { get; set; }

        // Note: With these four properties, Contents would still fit into a Table Storage
        // entity with a max of around 500,000 characters. (provided SenderName is short)
        // SignalR, however, has a max limit size of about 32 KiB, or 16,384 UTF-16 characters.
        // That's about 4 or 5 pages of English text. Should be fine.
        public static int MaxMessageSize => 16_000;
    }
}
