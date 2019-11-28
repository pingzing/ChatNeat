using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;

namespace ChatNeat.API
{
    public static class SignalRFunctions
    {
        private const string ChatHubName = "chatneat";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="req"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        [FunctionName("negotiate")]
        public static SignalRConnectionInfo Negotiate(
            [HttpTrigger(AuthorizationLevel.Anonymous)]HttpRequest req,
            [SignalRConnectionInfo(HubName = ChatHubName)]SignalRConnectionInfo info)
        {
            return info;
        }

        // todo: message should contain user, timestamp and message
        [FunctionName("sendmessage")]
        public static Task SendMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]MessagePayload payload,
            [SignalR(HubName = ChatHubName)]IAsyncCollector<SignalRMessage> signalRMessages)
        {
            // Store message in DB, and broadcast to all connected users in group.

            return signalRMessages.AddAsync(
                new SignalRMessage
                {
                    GroupName = payload.GroupId.ToString("N"),
                    Target = "newMessage",
                    Arguments = new[] { payload }
                }
            );
        }
    }
}
