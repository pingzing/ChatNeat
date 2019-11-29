using System;

namespace ChatNeat.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset CreationDate { get; set; }
    }
}
