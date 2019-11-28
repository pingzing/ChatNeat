using System;

namespace ChatNeat.Models
{
    public class MessagePayload
    {
        public Guid GroupId { get; set; }
        public Guid SenderId { get; set; }
        public string Message { get; set; }
    }
}
