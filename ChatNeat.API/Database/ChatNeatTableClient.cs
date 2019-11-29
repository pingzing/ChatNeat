using ChatNeat.API.Database.Entities;
using ChatNeat.API.Database.Extensions;
using ChatNeat.Models;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatNeat.API.Database
{
    // Notes to self on resilience and performance:
    // Table Storage has a retry policy which, by default, retries most transient failures
    // (i.e., it won't retry a 404) with exponential backoff.
    public class ChatNeatTableClient : IChatNeatTableClient
    {
        private readonly CloudTableClient _tableClient;
        private readonly ILogger<ChatNeatTableClient> _logger;

        // Basic data model: each group is a table
        // each table contains a collection of UserEntities and MessageEntities
        public ChatNeatTableClient(CloudTableClient tableClient, ILogger<ChatNeatTableClient> logger)
        {
            _logger = logger;
            _tableClient = tableClient;
        }

        public async Task<IEnumerable<Group>> GetGroupList()
        {
            var allGroupsTable = _tableClient.GetTableReference(TableNames.AllGroups);

            TableQuery<TableEntityAdapter<AllGroupsGroupEntry>> query = new TableQuery<TableEntityAdapter<AllGroupsGroupEntry>>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionNames.Group));

            IEnumerable<TableEntityAdapter<AllGroupsGroupEntry>> groups =
                await allGroupsTable.ExecuteQueryAsync(query);
            return groups.Select(x => new Group
            {
                Id = Guid.Parse(x.RowKey),
                Count = x.OriginalEntity.Count,
                Name = x.OriginalEntity.Name
            });
        }

        private async Task AddToGroupsList(Group newGroup)
        {
            var allGroupsTable = _tableClient.GetTableReference(TableNames.AllGroups);

            var groupEntry = new AllGroupsGroupEntry
            {
                Name = newGroup.Name,
                Count = newGroup.Count,
                CreationTime = newGroup.CreationTime.UtcDateTime
            };
            var addToTableOp = TableOperation.Insert(
                new TableEntityAdapter<AllGroupsGroupEntry>(groupEntry, PartitionNames.Group, newGroup.Id.ToString("N")));
            TableResult result = await allGroupsTable.ExecuteAsync(addToTableOp);
            if (!(result.Result is TableEntityAdapter<AllGroupsGroupEntry> addedEntry))
            {
                // This isn't fatal. We'll catch it next time we do a group update.
                _logger.LogWarning($"Failed to update group list metadata table with new group: ID - {newGroup.Id}, Name - {newGroup.Name}");
            }
        }

        private async Task DeleteFromGroupsList(Guid groupId)
        {
            var allGroupsTable = _tableClient.GetTableReference(TableNames.AllGroups);

            TableOperation getGroupOperation = TableOperation.Retrieve<TableEntityAdapter<AllGroupsGroupEntry>>(PartitionNames.Group, groupId.ToString("N"));
            TableResult getResult = await allGroupsTable.ExecuteAsync(getGroupOperation);
            if (getResult.Result is TableEntityAdapter<AllGroupsGroupEntry> groupEntry)
            {
                TableOperation deleteOperation = TableOperation.Delete(groupEntry);
                TableResult deleteResult = await allGroupsTable.ExecuteAsync(deleteOperation);
                if (deleteResult.HttpStatusCode != 204)
                {
                    _logger.LogError($"Located, but could not delete, group entry with ID {groupId} from the groups list. Status code: {deleteResult.HttpStatusCode}");
                }
            }
            else
            {
                _logger.LogWarning($"Could not find group entry with ID {groupId} while trying to delete it from the groups list. Status code: {getResult.HttpStatusCode}");
            }
        }

        public async Task<Group> AddGroup(Group newGroup)
        {
            // Give the new group an ID. Ignore whatever we're given, we're creating a new group.
            newGroup.Id = Guid.NewGuid();
            var groupTable = _tableClient.GetTableReference(newGroup.Id.ToString("N"));
            await groupTable.CreateAsync();

            var metadata = new GroupMetadata { Name = newGroup.Name, CreationTime = DateTime.UtcNow };
            TableOperation addMetadataOp = TableOperation.Insert(new TableEntityAdapter<GroupMetadata>(metadata, PartitionNames.Metadata, PartitionNames.Metadata));
            TableResult addMetadataResult = await groupTable.ExecuteAsync(addMetadataOp);
            if (addMetadataResult.Result is TableEntityAdapter<GroupMetadata> groupMetaData)
            {
                await AddToGroupsList(newGroup);

                _logger.LogInformation($"Group with ID {newGroup.Id} and name {newGroup.Name} added.");
                return new Group
                {
                    Count = 0,
                    CreationTime = groupMetaData.OriginalEntity.CreationTime,
                    Id = Guid.Parse(groupMetaData.RowKey),
                    Name = groupMetaData.OriginalEntity.Name
                };
            }
            else
            {
                await groupTable.DeleteAsync();
                _logger.LogError($"Unable to add table named {newGroup.Name} to database.");
                return null;
            }
        }

        public async Task DeleteGroup(Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToString("N"));
            await groupTable.DeleteAsync(); // TODO: _can_ this fail? It can probably timeout, which likely throws an exception...
            await DeleteFromGroupsList(groupId);
            _logger.LogInformation($"Group with ID {groupId} deleted.");
        }
    }
}
