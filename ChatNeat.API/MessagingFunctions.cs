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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatNeat.API
{
    public class MessagingFunctions
    {
        private readonly IChatService _chatService;
        private readonly ILogger<MessagingFunctions> _logger;

        public MessagingFunctions(IChatService chatService, ILogger<MessagingFunctions> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [OpenApiOperation(Summary = "Connect a user to the SignalR messaging system.",
            Description = "Call this with the user's GUID-based ID to connect to the Azure SignalR messaging backend. " +
            "If the user ID is valid, this will return an access URL and an AccessToken that can be used to communicate with SignalR.")]
        [OpenApiParameter("X-User-Id", Description = "The user's GUID-based ID.", In = ParameterLocation.Header, Required = true, Type = typeof(Guid))]
        [OpenApiResponseBody(System.Net.HttpStatusCode.BadRequest, "", typeof(int))]
        [OpenApiResponseBody(System.Net.HttpStatusCode.OK, "application/json", typeof(SignalRConnectionInfo))]
        [FunctionName("negotiate")]
        public async Task<IActionResult> Negotiate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "messaging/negotiate")]HttpRequest req,
            IBinder binder)
        {
            string userId = req.Headers["X-User-Id"];
            if (!Guid.TryParse(userId, out Guid userIdGuid))
            {
                _logger.LogError($"User call to /negotiate did not include a valid GUID in the header. We found '{userId}' in the X-User-Id header.");
                return new BadRequestResult();
            }

            // Force override of the SignalR user ID.
            // Normally this method would be decorated with a [SignalRConnectionInfo] attribute, which would be
            // where we could set the UserId attribute by using function dot-binding, i.e. UserId = "{headers.x-user-id}".
            // Unfortunately, the only headers visible to the function seem to be the Azure Auth-based claims headers,
            // and we're sending up a custom user ID.
            // So instead, we short-circuit the normal binding process, forcibly create our own binding attribute by hand,
            // at a point in time where we have access to our user ID, and then tell the binder to use that instead.
            SignalRConnectionInfoAttribute attribute = new SignalRConnectionInfoAttribute
            {
                HubName = Constants.ChatHubName,
                UserId = userIdGuid.ToIdString(),
            };
            SignalRConnectionInfo info = await binder.BindAsync<SignalRConnectionInfo>(attribute);

            return new OkObjectResult(info);
        }

        [OpenApiOperation(Summary = "(Re)associates a client's SignalR connection instance with all their groups.",
            Description = "(Re)associates the given user ID with all the groups they belong to in SignalR. Should be called " +
            "by the client after /negotiate, or any time after the SignalR connection is interrupted.")]
        [OpenApiParameter("userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
        [FunctionName("reconnect")]
        public async Task<IActionResult> Reconnect(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "messaging/reconnect/{userId}")]HttpRequest req,
            string userId,
            [SignalR(HubName = Constants.ChatHubName)]IAsyncCollector<SignalRGroupAction> groupActions)
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
