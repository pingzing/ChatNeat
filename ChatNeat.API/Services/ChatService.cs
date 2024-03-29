﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatNeat.API.Database;
using ChatNeat.Models;
using Microsoft.Extensions.Logging;

namespace ChatNeat.API.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatNeatTableClient _tableClient;
        private readonly ILogger<ChatService> _logger;

        public ChatService(IChatNeatTableClient tableClient, ILogger<ChatService> logger)
        {
            _tableClient = tableClient;
            _logger = logger;
        }

        public Task<Group> CreateGroup(string newGroupName)
        {
            return _tableClient.CreateGroup(newGroupName);
        }

        public async Task<IEnumerable<User>> DeleteGroup(Guid groupId)
        {
            var usersInGroup = await _tableClient.GetUsers(groupId);
            // Warning, possible concurrency issue: what if a user gets added between these statements?
            // Impact: minor. User sees they're in a group that no longer exists
            var deleteResult = await _tableClient.DeleteGroup(groupId);
            if (deleteResult != ServiceResult.Success)
            {
                return null;
            }

            // Space for a speedup here, too. We don't have to await these one-by-one.
            foreach (var user in usersInGroup)
            {
                var result = await _tableClient.RemoveFromUserGroups(user.Id, groupId);
                if (result != ServiceResult.Success)
                {
                    _logger.LogWarning($"Failed to remove group {groupId} from user {user.Id}'s group list when the group was deleted.");
                }
            }

            return usersInGroup;
        }

        public Task<IEnumerable<Group>> GetGroupList()
        {
            return _tableClient.GetGroupList();
        }

        public async Task<IEnumerable<Group>> GetUserMembership(Guid userId)
        {
            var allGroupsTask = _tableClient.GetGroupList();
            var userGroupsTask = _tableClient.GetGroups(userId);
            await Task.WhenAll(allGroupsTask, userGroupsTask);
            IEnumerable<Group> allGroups = allGroupsTask.Result;
            IEnumerable<Guid> userGroups = userGroupsTask.Result;
            if (allGroups == null)
            {
                _logger.LogError($"Unable to get all groups for user with ID {userId}");
                return null;
            }
            if (userGroups == null)
            {
                _logger.LogError($"Unable to get group membership for user with ID {userId}");
                return null;
            }

            return userGroups.Select(x => allGroups.SingleOrDefault(y => y.Id == x))
                .Where(x => x != null);

        }

        public Task<IEnumerable<User>> GetUsers(Guid groupId)
        {
            return _tableClient.GetUsers(groupId);
        }

        public Task<ServiceResult> AddUserToGroup(User user, Guid groupId)
        {
            return _tableClient.AddUserToGroup(user, groupId);
        }

        public Task<ServiceResult> LeaveGroup(Guid userId, Guid groupId)
        {
            return _tableClient.LeaveGroup(userId, groupId);
        }

        public async Task<Message> StoreMessage(Message payload)
        {
            return await _tableClient.StoreMessage(payload);
        }

        public Task<IEnumerable<Message>> GetMessages(Guid groupId)
        {
            return _tableClient.GetMessages(groupId);
        }
    }
}
