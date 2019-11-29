﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChatNeat.API.Database;
using ChatNeat.API.Database.Extensions;
using ChatNeat.Models;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

        public Task<IEnumerable<Group>> GetUserMembership(Guid userId)
        {
            // TODO: Get all groups a user belongs to
            throw new NotImplementedException();
        }

        public Task<ServiceResult> AddUserToGroup(User user, Guid groupId)
        {
            return _tableClient.AddUserToGroup(user, groupId);
        }

        public Task<ServiceResult> LeaveGroup(Guid userId, Guid groupId)
        {
            return _tableClient.LeaveGroup(userId, groupId);
        }

        public async Task<SignalRMessage> SendMessage(MessagePayload payload)
        {
            // Do DB things, then...
            var success = await _tableClient.StoreMessage(payload);
            if (success != ServiceResult.Success)
            {
                return null;
            }

            // return SignalR stuff
            return new SignalRMessage
            {
                GroupName = payload.GroupId.ToIdString(),
                Target = "newMessage",
                Arguments = new[] { JsonConvert.SerializeObject(payload) }
            };
        }
    }
}