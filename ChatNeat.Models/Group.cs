using System;

namespace ChatNeat.Models
{
    public class Group
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public DateTimeOffset CreationTime { get; set; }
    }
}
