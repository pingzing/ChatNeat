using ChatNeat.Models;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatNeat.API.Services
{
    public interface IChatService
    {
        Task<IEnumerable<Group>> GetGroupList();
        Task<IEnumerable<Group>> GetUserMembership(Guid userId);
        Task<ServiceResult> AddUserToGroup(User userId, Guid groupId);
        Task<Group> CreateGroup(string newGroupName);
        Task<ServiceResult> LeaveGroup(Guid userId, Guid groupId);
        Task<IEnumerable<User>> DeleteGroup(Guid groupId);
        Task<SignalRMessage> SendMessage(MessagePayload payload);
    }
}
