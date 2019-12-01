using System;

namespace ChatNeat.Models
{
    public class Message
    {
        public Guid GroupId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; }
        public string Contents { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
