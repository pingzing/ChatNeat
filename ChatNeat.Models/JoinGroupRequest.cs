using System;

namespace ChatNeat.Models
{
    public class JoinGroupRequest
    {
        public User User { get; set; }
        public Guid GroupId { get; set; }
    }
}
