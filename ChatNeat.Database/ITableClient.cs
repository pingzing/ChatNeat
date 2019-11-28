using ChatNeat.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatNeat.Database
{
    public interface ITableClient
    {
        Task<IEnumerable<Group>> GetGroupList();
        Task<Group> AddGroup(Group newGroup);
        Task DeleteGroup(Guid groupId);
    }
}
