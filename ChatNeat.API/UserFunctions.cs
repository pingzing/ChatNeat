using Aliencube.AzureFunctions.Extensions.OpenApi.Attributes;
using ChatNeat.API.Services;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ChatNeat.API
{
    public class UserFunctions
    {
        private readonly IChatService _chatService;
        private readonly ILogger<UserFunctions> _logger;

        public UserFunctions(IChatService chatService, ILogger<UserFunctions> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [OpenApiOperation(Summary = "Returns a list of all the groups this user belongs to.")]
        [OpenApiParameter("userId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
        [OpenApiResponseBody(HttpStatusCode.OK, "application/json", typeof(Group[]), Description = "Returned on success.")]
        [OpenApiResponseBody(HttpStatusCode.BadRequest, "", typeof(int), Description = "Returned if user ID is invalid.")]
        [OpenApiResponseBody(HttpStatusCode.NotFound, "", typeof(int), Description = "Returned if user could not be found, or had no groups.")]
        [FunctionName("getusergroups")]
        public async Task<IActionResult> GetGroups(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/{userId}/groups")]HttpRequest req,
            string userId)
        {
            if (!(Guid.TryParse(userId, out Guid userIdGuid)))
            {
                _logger.LogError($"Could not parse '{userId}' as a GUID.");
                return new BadRequestResult();
            }

            var groups = await _chatService.GetUserMembership(userIdGuid);
            if (groups == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(groups.ToArray());
        }
    }
}
