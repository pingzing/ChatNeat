using ChatNeat.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatNeat.API.Services
{
    public interface IChatService
    {
        Task<IEnumerable<Group>> GetGroupList();
        Task<IEnumerable<Group>> GetUserMembership(Guid userId);
        Task<IEnumerable<User>> GetUsers(Guid groupId);
        Task<ServiceResult> AddUserToGroup(User userId, Guid groupId);
        Task<Group> CreateGroup(string newGroupName);
        Task<ServiceResult> LeaveGroup(Guid userId, Guid groupId);
        Task<IEnumerable<User>> DeleteGroup(Guid groupId);
        Task<Message> StoreMessage(Message payload);
        Task<IEnumerable<Message>> GetMessages(Guid groupId);
    }
}
