using Aliencube.AzureFunctions.Extensions.OpenApi.Attributes;
using ChatNeat.API.Database;
using ChatNeat.API.Database.Extensions;
using ChatNeat.API.Services;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace ChatNeat.API
{

    public class GroupFunctions
    {
        private const string ChatHubName = "chatneat";
        private readonly IChatService _chatService;
        private readonly ILogger<GroupFunctions> _logger;

        public GroupFunctions(IChatService chatService, ILogger<GroupFunctions> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [OpenApiOperation]
        [FunctionName("getgroupslist")]
        public async Task<IActionResult> GetGroupsList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]HttpRequest req)
        {
            var groups = await _chatService.GetGroupList();
            if (groups == null)
            {
                _logger.LogError("Failed to retrieve any groups.");
                return new InternalServerErrorResult();
            }
            return new OkObjectResult(groups);
        }

        [OpenApiOperation]
        [FunctionName("addgroup")]
        public async Task<IActionResult> CreateGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]string newGroupName)
        {
            Group addedGroup = await _chatService.CreateGroup(newGroupName);
            if (addedGroup == null)
            {
                _logger.LogError($"Failed to add group with name {newGroupName}");
                return new BadRequestResult();
            }

            return new OkObjectResult(addedGroup);
        }

        [OpenApiOperation]
        [FunctionName("deletegroup")]
        public async Task<IActionResult> DeleteGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete")]string groupId,
            [SignalR(HubName = ChatHubName)]IAsyncCollector<SignalRGroupAction> groupActions)
        {
            if (!Guid.TryParse(groupId, out Guid groupIdGuid))
            {
                _logger.LogError($"Could not parse {groupId} as a valid GUID.");
                return new BadRequestResult();
            }

            // Speedup opportunity here: Transform into a bunch of tasks, do Task.WhenAll
            var usersInDeletedGroup = await _chatService.DeleteGroup(groupIdGuid);
            foreach (var groupAction in usersInDeletedGroup.Select(x => new SignalRGroupAction
            {
                Action = GroupAction.Remove,
                GroupName = groupIdGuid.ToIdString(),
                UserId = x.Id.ToIdString(),
            }))
            {
                await groupActions.AddAsync(groupAction);
            }

            return new OkResult();
        }

        [OpenApiOperation]
        [FunctionName("joingroup")]
        public async Task<IActionResult> JoinGroup(
             [HttpTrigger(AuthorizationLevel.Anonymous, "post")]JoinGroupRequest request,
             [SignalR(HubName = ChatHubName)]IAsyncCollector<SignalRGroupAction> groupActions)
        {
            var success = await _chatService.AddUserToGroup(request.User, request.GroupId);

            IActionResult earlyReturn = success switch
            {
                ServiceResult.Success => null,
                ServiceResult.InvalidArguments => new BadRequestResult(),
                ServiceResult.NotFound => new NotFoundResult(),
                _ => new InternalServerErrorResult()
            };
            if (earlyReturn != null)
            {
                return earlyReturn;
            }

            await groupActions.AddAsync(
                new SignalRGroupAction
                {
                    UserId = request.User.Id.ToIdString(),
                    Action = GroupAction.Add,
                    GroupName = request.GroupId.ToIdString()
                });

            return new OkResult();
        }

        [OpenApiOperation]
        [FunctionName("leavegroup")]
        public async Task<IActionResult> LeaveGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]LeaveGroupRequest request)
        {
            var success = await _chatService.LeaveGroup(request.UserId, request.GroupId);
            return success switch
            {
                ServiceResult.Success => new OkResult(),
                ServiceResult.NotFound => new NotFoundResult(),
                _ => new InternalServerErrorResult()
            };
        }

        [OpenApiOperation]
        [FunctionName("reconnect")]
        public async Task<IActionResult> ReconnectToGroups(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]string userId,
            [SignalR(HubName = ChatHubName)]IAsyncCollector<SignalRGroupAction> groupActions)
        {
            if (!(Guid.TryParse(userId, out Guid userIdGuid)))
            {
                _logger.LogError($"Could not parse '{userId}' as a GUID.");
                return new BadRequestResult();
            }

            // Speedup opportunity here: Transform into a bunch of tasks, do Task.WhenAll
            IEnumerable<Group> groups = await _chatService.GetUserMembership(userIdGuid);
            foreach (var action in groups.Select(x => new SignalRGroupAction
            {
                Action = GroupAction.Add,
                GroupName = x.Id.ToIdString(),
                UserId = userIdGuid.ToIdString(),
            }
            ))
            {
                await groupActions.AddAsync(action);
            }

            return new OkResult();
        }
    }
}
