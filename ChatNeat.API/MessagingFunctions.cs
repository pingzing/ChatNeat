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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;

namespace ChatNeat.API
{
    public class MessagingFunctions
    {
        private const string ChatHubName = "chatneat";
        private readonly IChatService _chatService;
        private readonly ILogger<MessagingFunctions> _logger;

        public MessagingFunctions(IChatService chatService, ILogger<MessagingFunctions> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [OpenApiOperation]
        [FunctionName("negotiate")]
        public async Task<SignalRConnectionInfo> Negotiate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequest req,
            IBinder binder)
        {
            // Force override of the SignalR user ID.
            string userId = req.Headers["X-User-Id"];
            SignalRConnectionInfoAttribute attribute = new SignalRConnectionInfoAttribute
            {
                HubName = ChatHubName,
                UserId = userId
            };
            SignalRConnectionInfo info = await binder.BindAsync<SignalRConnectionInfo>(attribute);

            return info;
        }

        [OpenApiOperation]
        [OpenApiRequestBody("application/json", typeof(Message))]
        [FunctionName("sendmessage")]
        public async Task<IActionResult> SendMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]Message payload,
            [SignalR(HubName = ChatHubName)]IAsyncCollector<SignalRMessage> signalRMessages)
        {
            var result = await _chatService.StoreMessage(payload);
            IActionResult earlyReturn = result switch
            {
                ServiceResult.Success => null,
                ServiceResult.NotFound => new NotFoundResult(),
                ServiceResult.ServerError => new InternalServerErrorResult(),
                _ => new InternalServerErrorResult()
            };
            if (earlyReturn != null)
            {
                return earlyReturn;
            }

            await signalRMessages.AddAsync(new SignalRMessage
            {
                Arguments = new object[] { payload },
                GroupName = payload.GroupId.ToIdString(),
                Target = SignalRMessages.NewMessage
            });
            return new OkResult();
        }


    }
}
