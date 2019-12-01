using ChatNeat.API.Services;
using ChatNeat.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatNeat.API.Database
{
    public interface IChatNeatTableClient
    {
        Task<IEnumerable<Group>> GetGroupList();
        Task<IEnumerable<User>> GetUsers(Guid groupId);

        /// <summary>
        /// Create a new group with the given name.
        /// </summary>
        /// <param name="newGroupName">The name for the new group.</param>
        /// <returns>On success, the created <see cref="Group"/> object. On failure, null.</returns>
        Task<Group> CreateGroup(string newGroupName);
        Task<ServiceResult> DeleteGroup(Guid groupId);

        /// <summary>
        /// Add user to group with the given ID. No effect if the user is already in that group.
        /// </summary>
        /// <param name="user">The user to add to the given group.</param>
        /// <param name="groupId">ID of the group to add the user to.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<ServiceResult> AddUserToGroup(User user, Guid groupId);
        Task<IEnumerable<Guid>> GetGroups(Guid userId);
        Task<ServiceResult> LeaveGroup(Guid userId, Guid groupId);
        Task<ServiceResult> RemoveFromUserGroups(Guid userId, Guid groupId);
        Task<Message> StoreMessage(Message message);
        Task<IEnumerable<Message>> GetMessages(Guid groupId);
    }
}
