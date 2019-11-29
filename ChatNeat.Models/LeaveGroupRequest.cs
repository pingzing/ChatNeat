using System;

namespace ChatNeat.Models
{
    public class LeaveGroupRequest
    {
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
    }
}
