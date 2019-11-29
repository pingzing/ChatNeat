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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="req"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        [FunctionName("negotiate")]
        public SignalRConnectionInfo Negotiate(
            [HttpTrigger(AuthorizationLevel.Anonymous)]HttpRequest req,
            [SignalRConnectionInfo(HubName = ChatHubName)]SignalRConnectionInfo info)
        {
            return info;
        }

        [FunctionName("sendmessage")]
        public async Task<IActionResult> SendMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]MessagePayload payload,
            [SignalR(HubName = ChatHubName)]IAsyncCollector<SignalRMessage> signalRMessages)
        {
            var signalRMessage = await _chatService.SendMessage(payload);
            if (signalRMessage == null)
            {
                // TODO: do we need to be smarter in error checking here?
                return new BadRequestResult();
            }
            await signalRMessages.AddAsync(signalRMessage);
            return new OkResult();
        }


    }
}
