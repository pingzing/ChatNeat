using ChatNeat.API.Database;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Web.Http;

namespace ChatNeat.API
{

    public class GroupFunctions
    {
        private readonly ITableClient _tableClient;
        private readonly ILogger<GroupFunctions> _logger;

        public GroupFunctions(ITableClient tableClient, ILogger<GroupFunctions> logger)
        {
            _tableClient = tableClient;
            _logger = logger;
        }

        [FunctionName("getgroupslist")]
        public async Task<IActionResult> GetGroupsList(
            [HttpTrigger(AuthorizationLevel.Anonymous)]HttpRequest req)
        {
            var groups = await _tableClient.GetGroupList();
            if (groups == null)
            {
                _logger.LogError("Failed to retrieve any groups.");
                return new InternalServerErrorResult();
            }
            return new OkObjectResult(groups);
        }

        [FunctionName("addgroup")]
        public async Task<IActionResult> AddGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]Group newGroup)
        {
            Group addedGroup = await _tableClient.AddGroup(newGroup);
            if (addedGroup == null)
            {
                _logger.LogError($"Failed to add group with name {newGroup.Name}");
                return new BadRequestResult();
            }

            return new OkObjectResult(addedGroup);
        }

        [FunctionName("deletegroup")]
        public async Task<IActionResult> DeleteGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete")]string groupId)
        {
            Guid groupIdGuid = Guid.Parse(groupId);
            await _tableClient.DeleteGroup(groupIdGuid);
            return new OkResult();
        }
    }
}
