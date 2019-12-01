using Aliencube.AzureFunctions.Extensions.OpenApi.Attributes;
using ChatNeat.API.Database.Extensions;
using ChatNeat.API.Services;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;

namespace ChatNeat.API
{

    public class GroupFunctions
    {
        private readonly IChatService _chatService;
        private readonly ILogger<GroupFunctions> _logger;

        public GroupFunctions(IChatService chatService, ILogger<GroupFunctions> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [OpenApiOperation(Summary = "Gets a list of all groups.")]
        [OpenApiResponseBody(HttpStatusCode.OK, "application/json", typeof(Group[]), Description = "Returned on success.")]
        [OpenApiResponseBody(HttpStatusCode.InternalServerError, "", typeof(int), Description = "Returned on failure.")]
        [FunctionName("getgroups")]
        public async Task<IActionResult> GetGroups(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "groups")]HttpRequest req)
        {
            var groups = await _chatService.GetGroupList();
            if (groups == null)
            {
                _logger.LogError("Failed to retrieve any groups.");
                return new InternalServerErrorResult();
            }
            return new OkObjectResult(groups.ToArray());
        }

        [OpenApiOperation(Summary = "Creates a new group with the given name.",
            Description = "Creates a new group with the given name. On success, returns the created group. " +
            "Note that it does NOT automatically add the user who created the group into the group.")]
        [OpenApiRequestBody("application/json", typeof(CreateGroupRequest))]
        [OpenApiResponseBody(HttpStatusCode.Created, "application/json", typeof(Group), Description = "Returned on success.")]
        [OpenApiResponseBody(HttpStatusCode.BadRequest, "", typeof(int),
            Description = "Returned on null or empty group name, or if creation otherwise fails.")]
        [FunctionName("creategroup")]
        public async Task<IActionResult> CreateGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "group")]CreateGroupRequest newGroupRequest)
        {
            if (string.IsNullOrWhiteSpace(newGroupRequest?.Name))
            {
                return new BadRequestResult();
            }
            Group addedGroup = await _chatService.CreateGroup(newGroupRequest.Name);
            if (addedGroup == null)
            {
                _logger.LogError($"Failed to add group with name {newGroupRequest.Name}");
                return new BadRequestResult();
            }

            return new CreatedResult("", addedGroup);
        }

        [OpenApiOperation(Summary = "Deletes the group with the given ID.",
            Description = "Deletes the group with the given ID. Also removes all user associations with that group, " +
            "and removes any SignalR association between users and the deleted group.")]
        [OpenApiParameter("groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
        [OpenApiResponseBody(HttpStatusCode.OK, "", typeof(int), Description = "Returned on success.")]
        [OpenApiResponseBody(HttpStatusCode.BadRequest, "", typeof(int),
            Description = "Returned if the group ID is invalid, or could not be found.")]
        [FunctionName("deletegroup")]
        public async Task<IActionResult> DeleteGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "group/{groupId}")]HttpRequest req,
            string groupId,
            [SignalR(HubName = Constants.ChatHubName)]IAsyncCollector<SignalRGroupAction> groupActions)
        {
            if (!Guid.TryParse(groupId, out Guid groupIdGuid))
            {
                _logger.LogError($"Could not parse {groupId} as a valid GUID.");
                return new BadRequestResult();
            }

            // Speedup opportunity here: Transform into a bunch of tasks, do Task.WhenAll
            var usersInDeletedGroup = await _chatService.DeleteGroup(groupIdGuid);
            if (usersInDeletedGroup == null)
            {
                return new BadRequestResult();
            }
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

        [OpenApiOperation(Summary = "Adds a user to the given group.",
            Description = "Adds the given user to the given group. Also associates their user ID with the group in SignalR.")]
        [OpenApiParameter("groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
        [OpenApiRequestBody("application/json", typeof(User), Description = "The user that will join the group.")]
        [OpenApiResponseBody(HttpStatusCode.OK, "", typeof(int), Description = "Returned on success.")]
        [OpenApiResponseBody(HttpStatusCode.BadRequest, "", typeof(int), Description = "Returned if the group ID is invalid, or the group is full.")]
        [OpenApiResponseBody(HttpStatusCode.NotFound, "", typeof(int), Description = "Returned if the group or user doesn't exist.")]
        [OpenApiResponseBody(HttpStatusCode.InternalServerError, "", typeof(int), Description = "Returned on failure.")]
        [FunctionName("joingroup")]
        public async Task<IActionResult> JoinGroup(
             [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "group/{groupId}/join")]User user,
             string groupId,
             [SignalR(HubName = Constants.ChatHubName)]IAsyncCollector<SignalRGroupAction> groupActions)
        {
            if (!Guid.TryParse(groupId, out Guid groupIdGuid))
            {
                _logger.LogError($"Could not parse {groupId} as a valid GUID.");
                return new BadRequestResult();
            }

            var success = await _chatService.AddUserToGroup(user, groupIdGuid);

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
                    UserId = user.Id.ToIdString(),
                    Action = GroupAction.Add,
                    GroupName = groupIdGuid.ToIdString()
                });

            return new OkResult();
        }

        [OpenApiOperation(Summary = "Removes the user from the given group.",
            Description = "Removes the user from the given group. Also disassociates their SignalR idenitty from the group. " +
            "Note: An empty request body should be sent.")]
        [OpenApiParameter("groupId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
        [OpenApiParameter("userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
        [OpenApiResponseBody(HttpStatusCode.OK, "", typeof(int), Description = "Returned on success.")]
        [OpenApiResponseBody(HttpStatusCode.NotFound, "", typeof(int), Description = "Returned if the group or user doesn't exist.")]
        [OpenApiResponseBody(HttpStatusCode.InternalServerError, "", typeof(int), Description = "Returned on failure.")]
        [FunctionName("leavegroup")]
        public async Task<IActionResult> LeaveGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "group/{groupId}/leave/{userId}")]HttpRequest request,
            string groupId,
            string userId)
        {
            if (!Guid.TryParse(groupId, out Guid groupIdGuid))
            {
                _logger.LogError($"Could not parse {groupId} as a valid GUID.");
                return new BadRequestResult();
            }

            if (!Guid.TryParse(userId, out Guid userIdGuid))
            {
                _logger.LogError($"Could not parse {userId} as a valid GUID.");
                return new BadRequestResult();
            }

            var success = await _chatService.LeaveGroup(userIdGuid, groupIdGuid);
            return success switch
            {
                ServiceResult.Success => new OkResult(),
                ServiceResult.NotFound => new NotFoundResult(),
                _ => new InternalServerErrorResult()
            };
        }

        [OpenApiOperation(Summary = "Get all users in the given group.",
            Description = "Get all users that belong to the given group. Does not reflect who is and is not online.")]
        [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
        [OpenApiResponseBody(HttpStatusCode.OK, "application/json", typeof(User[]), Description = "Returned on success.")]
        [OpenApiResponseBody(HttpStatusCode.BadRequest, "", typeof(int), Description = "Returned if the group ID is invalid, or the group could not be found.")]
        [FunctionName("getusers")]
        public async Task<IActionResult> GetUsers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "group/{groupId}/users")]HttpRequest req,
            string groupId)
        {
            if (!(Guid.TryParse(groupId, out Guid groupIdGuid)))
            {
                _logger.LogError($"Could not parse '{groupId}' as a GUID.");
                return new BadRequestResult();
            }

            var users = await _chatService.GetUsers(groupIdGuid);
            if (users == null)
            {
                // Could also be NotFound, but we don't know enough to guess at this point
                return new BadRequestResult();
            }

            return new OkObjectResult(users.ToArray());
        }

        [OpenApiOperation(Summary = "Sends a message to the group.",
            Description = "Sends a message to all currently online users in the group, " +
            "and stores the message in the group's message history. Ignores any user-set value in Timestamp and SenderName fields.")]
        [OpenApiRequestBody("application/json", typeof(Message))]
        [OpenApiResponseBody(HttpStatusCode.OK, "application/json", typeof(Message), Description = "Returns the filled message object on success.")]
        [OpenApiResponseBody(HttpStatusCode.BadRequest, "", typeof(int),
            Description = "Returned if the message is invalid, or the group or user could not be found.")]
        [FunctionName("sendmessage")]
        public async Task<IActionResult> SendMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "group/{group}/message")]HttpRequest req,
            [SignalR(HubName = Constants.ChatHubName)]IAsyncCollector<SignalRMessage> signalRMessages)
        {
            // Deserializing by hand, because the model binder has issues with DateTimeOffset
            string messageBody = await req.ReadAsStringAsync();
            Message payload;
            try
            {
                payload = JsonConvert.DeserializeObject<Message>(messageBody);
            }
            catch (JsonSerializationException ex)
            {
                _logger.LogError($"Unable to deserialize user-sent Message object. Details: {ex}");
                return new BadRequestResult();
            }

            payload = await _chatService.StoreMessage(payload);
            if (payload == null)
            {
                // Lots of other failure modes here, but no way to know which one we hit.
                return new BadRequestResult();
            }

            await signalRMessages.AddAsync(new SignalRMessage
            {
                Arguments = new object[] { payload },
                GroupName = payload.GroupId.ToIdString(),
                Target = SignalRMessages.NewMessage
            });
            return new OkObjectResult(payload);
        }

        [OpenApiOperation(Summary = "Returns the entire message history for a given group.")]
        [OpenApiParameter(name: "groupId", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
        [OpenApiResponseBody(HttpStatusCode.OK, "application/json", typeof(Message[]))]
        [OpenApiResponseBody(HttpStatusCode.BadRequest, "", typeof(int), Description = "Returned if the group ID is invalid.")]
        [OpenApiResponseBody(HttpStatusCode.NotFound, "", typeof(int), Description = "Returned if the group doesn't exist.")]
        [FunctionName("getmessages")]
        public async Task<IActionResult> GetMessages(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "group/{groupId}/messages")]HttpRequest req,
            string groupId)
        {
            if (!(Guid.TryParse(groupId, out Guid groupIdGuid)))
            {
                _logger.LogError($"Could not parse '{groupId}' as a GUID.");
                return new BadRequestResult();
            }

            IEnumerable<Message> messages = await _chatService.GetMessages(groupIdGuid);
            if (messages == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(messages.ToArray());
        }
    }
}
