using ChatNeat.API.Database.Entities;
using ChatNeat.API.Database.Extensions;
using ChatNeat.API.Services;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
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
        // Business rule.
        public const uint MaxGroupSize = 20;

        private readonly CloudTableClient _tableClient;
        private readonly ILogger<ChatNeatTableClient> _logger;

        public ChatNeatTableClient(CloudTableClient tableClient, ILogger<ChatNeatTableClient> logger)
        {
            _logger = logger;
            _tableClient = tableClient;
        }

        public async Task<IEnumerable<Group>> GetGroupList()
        {
            var allGroupsTable = _tableClient.GetTableReference(TableNames.AllGroups);
            await allGroupsTable.CreateIfNotExistsAsync();

            TableQuery<TableEntityAdapter<AllGroupsGroupEntry>> query = new TableQuery<TableEntityAdapter<AllGroupsGroupEntry>>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionNames.Group));

            IEnumerable<TableEntityAdapter<AllGroupsGroupEntry>> groups =
                await allGroupsTable.ExecuteQueryAsync(query);
            return groups.Select(x => new Group
            {
                Id = Guid.Parse(x.RowKey),
                Count = x.OriginalEntity.Count,
                Name = x.OriginalEntity.Name,
                CreationTime = x.OriginalEntity.CreationTime,
            });
        }

        public async Task<IEnumerable<User>> GetUsers(Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToTableString());
            if (!(await groupTable.ExistsAsync()))
            {
                _logger.LogError($"Could not find any group with ID {groupId}.");
                return null;
            }

            TableQuery<TableEntityAdapter<UserEntity>> query = new TableQuery<TableEntityAdapter<UserEntity>>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionNames.User));

            IEnumerable<TableEntityAdapter<UserEntity>> users = await groupTable.ExecuteQueryAsync(query);
            return users.Select(x => new User
            {
                Id = Guid.Parse(x.RowKey),
                Name = x.OriginalEntity.Name
            });
        }

        public async Task<Group> CreateGroup(string newGroupName)
        {
            Guid newGroupId = Guid.NewGuid();
            var groupTable = _tableClient.GetTableReference(newGroupId.ToTableString());
            await groupTable.CreateAsync();

            var metadata = new GroupMetadata { Name = newGroupName, CreationTime = DateTime.UtcNow };
            TableOperation addMetadataOp = TableOperation.Insert(new TableEntityAdapter<GroupMetadata>(metadata, PartitionNames.Metadata, PartitionNames.Metadata));
            TableResult addMetadataResult = await groupTable.ExecuteAsync(addMetadataOp);
            if (addMetadataResult.Result is TableEntityAdapter<GroupMetadata> groupMetaData)
            {
                await AddOrUpdateToGroupsList(groupMetaData.OriginalEntity, newGroupId, 0);

                _logger.LogInformation($"Group with ID {newGroupId} and name {groupMetaData.OriginalEntity.Name} added.");
                return new Group
                {
                    Count = 0,
                    CreationTime = groupMetaData.OriginalEntity.CreationTime,
                    Id = newGroupId,
                    Name = groupMetaData.OriginalEntity.Name
                };
            }
            else
            {
                await groupTable.DeleteAsync();
                _logger.LogError($"Unable to add table named {newGroupName} to database.");
                return null;
            }
        }

        public async Task<ServiceResult> DeleteGroup(Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToTableString());
            if (!await groupTable.ExistsAsync())
            {
                _logger.LogError($"Could not find any group with ID {groupId}.");
                return ServiceResult.NotFound;
            }

            await groupTable.DeleteAsync(); // TODO: _can_ this fail? It can probably timeout, which likely throws an exception...
            await DeleteFromGroupsList(groupId);
            _logger.LogInformation($"Group with ID {groupId} deleted.");
            return ServiceResult.Success;
        }

        public async Task<ServiceResult> AddUserToGroup(User user, Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToTableString());
            if (!await groupTable.ExistsAsync())
            {
                _logger.LogError($"Could not find any group with ID {groupId}.");
                return ServiceResult.NotFound;
            }

            TableOperation getExistingUser = TableOperation.Retrieve<TableEntityAdapter<UserEntity>>(PartitionNames.User, user.Id.ToIdString());
            TableResult existsResult = await groupTable.ExecuteAsync(getExistingUser);
            if (existsResult.Result is TableEntityAdapter<UserEntity>)
            {
                // User already exists. We're done!
                return ServiceResult.Success;
            }

            // Verify group isn't currently full.
            int groupCount = await GetGroupCount(groupId);
            if (groupCount >= MaxGroupSize)
            {
                _logger.LogInformation($"Unable to add user {user.Name}-{user.Id} to group ID {groupId}. Group already has {groupCount} users.");
                return ServiceResult.InvalidArguments;
            }

            var userEntity = new TableEntityAdapter<UserEntity>(
                new UserEntity
                {
                    Name = user.Name,
                    LastSeen = DateTime.UtcNow
                }, PartitionNames.User, user.Id.ToIdString());
            TableOperation addUser = TableOperation.Insert(userEntity);
            TableResult addResult = await groupTable.ExecuteAsync(addUser);
            if (addResult.HttpStatusCode != StatusCodes.Status204NoContent)
            {
                _logger.LogError($"Unable to add user {user.Name}-{user.Id} to group {groupId}. Result code: {addResult.HttpStatusCode}");
                return ServiceResult.ServerError;
            }

            // User added. Update group count in AllGroups table
            GroupMetadata metadata = await GetGroupMetadata(groupId);
            await AddOrUpdateToGroupsList(metadata, groupId, groupCount + 1);
            await AddToUserGroups(user.Id, groupId, DateTime.UtcNow);
            return ServiceResult.Success;
        }

        public async Task<IEnumerable<Guid>> GetGroups(Guid userId)
        {
            var userTable = _tableClient.GetTableReference(userId.ToTableString());
            if (!await userTable.ExistsAsync())
            {
                _logger.LogError($"No user with the ID {userId} found.");
                return null;
            }

            TableQuery<DynamicTableEntity> query = new TableQuery<DynamicTableEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionNames.Group));
            return (await userTable.ExecuteQueryAsync(query))
                .Select(x => Guid.Parse(x.RowKey));
        }

        public async Task<ServiceResult> LeaveGroup(Guid userId, Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToTableString());
            if (!await groupTable.ExistsAsync())
            {
                _logger.LogError($"Could not find any group with ID {groupId}.");
                return ServiceResult.NotFound;
            }

            TableOperation getExistingUser = TableOperation.Retrieve<TableEntityAdapter<UserEntity>>(PartitionNames.User, userId.ToIdString());
            TableResult existsResult = await groupTable.ExecuteAsync(getExistingUser);
            if (!(existsResult.Result is TableEntityAdapter<UserEntity> user))
            {
                _logger.LogError($"Could not find any user with ID {userId} in group with ID {groupId}");
                return ServiceResult.NotFound;
            }

            TableOperation deleteUserOp = TableOperation.Delete(user);
            TableResult deleteResult = await groupTable.ExecuteAsync(deleteUserOp);
            if (deleteResult.HttpStatusCode != StatusCodes.Status204NoContent)
            {
                _logger.LogError($"Failed to remove user {user.OriginalEntity.Name}-{userId} from group with ID {groupId}. Status code: {deleteResult.HttpStatusCode}");
                return ServiceResult.ServerError;
            }

            int groupCount = await GetGroupCount(groupId);
            var groupMetadata = await GetGroupMetadata(groupId);
            await AddOrUpdateToGroupsList(groupMetadata, groupId, groupCount);
            await RemoveFromUserGroups(userId, groupId);
            return ServiceResult.Success;
        }

        public async Task<ServiceResult> RemoveFromUserGroups(Guid userId, Guid groupId)
        {
            var userTable = _tableClient.GetTableReference(userId.ToTableString());
            if (!await userTable.ExistsAsync())
            {
                _logger.LogError($"User table for {userId} does not exist.");
                return ServiceResult.NotFound;
            }
            TableOperation existsCheckOp = TableOperation.Retrieve<DynamicTableEntity>(PartitionNames.Group, groupId.ToIdString());
            TableResult existsResult = await userTable.ExecuteAsync(existsCheckOp);
            if (!(existsResult.Result is DynamicTableEntity entity))
            {
                // Already deleted, bail out.
                return ServiceResult.Success;
            }

            TableOperation deleteOp = TableOperation.Delete(entity);
            TableResult deleteResult = await userTable.ExecuteAsync(deleteOp);
            if (deleteResult.HttpStatusCode != StatusCodes.Status204NoContent)
            {
                _logger.LogError($"Failed to delete group {groupId} from user {userId}. Status code: {deleteResult.HttpStatusCode}");
                return ServiceResult.ServerError;
            }

            return ServiceResult.Success;
        }

        public async Task<Message> StoreMessage(Message message)
        {
            if (message.Contents == null)
            {
                _logger.LogError($"Message contents cannot be null.");
                return null;
            }
            if (message.Contents.Length > MessageEntity.MaxMessageSize)
            {
                _logger.LogError($"Message contents are too large. It was {message.Contents.Length}, but the max size is {MessageEntity.MaxMessageSize}");
                return null;
            }
            var groupTable = _tableClient.GetTableReference(message.GroupId.ToTableString());
            if (!await groupTable.ExistsAsync())
            {
                _logger.LogError($"Could not find any group with ID {message.GroupId}.");
                return null;
            }

            // Make sure the user belongs to the group
            TableOperation getUserOp = TableOperation.Retrieve<TableEntityAdapter<UserEntity>>(PartitionNames.User, message.SenderId.ToIdString());
            TableResult getUserResult = await groupTable.ExecuteAsync(getUserOp);
            if (!(getUserResult.Result is TableEntityAdapter<UserEntity> user))
            {
                _logger.LogError($"User ID {message.SenderId} does not belong to group ID {message.GroupId}.");
                return null;
            }

            Guid messageId = Guid.NewGuid();
            message.Timestamp = DateTime.UtcNow;
            message.SenderName = user.OriginalEntity.Name;
            var messageEntity = new TableEntityAdapter<MessageEntity>(new MessageEntity
            {
                SenderId = message.SenderId,
                Contents = message.Contents,
                SentTimestamp = message.Timestamp,
                SenderName = message.SenderName,
            }, PartitionNames.Message, messageId.ToIdString());
            TableOperation insertOp = TableOperation.Insert(messageEntity);
            TableResult insertResult = await groupTable.ExecuteAsync(insertOp);
            if (insertResult.HttpStatusCode != StatusCodes.Status204NoContent)
            {
                _logger.LogError($"Failed to add message from sender {message.SenderId} to group {message.GroupId}. Status code: {insertResult.HttpStatusCode}");
                return null;
            }

            return message;
        }

        public async Task<IEnumerable<Message>> GetMessages(Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToTableString());
            if (!(await groupTable.ExistsAsync()))
            {
                _logger.LogError($"Could not find any group with ID {groupId}.");
                return null;
            }

            TableQuery<TableEntityAdapter<MessageEntity>> getMessagesQuery = new TableQuery<TableEntityAdapter<MessageEntity>>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionNames.Message));
            // Perf warning here: this definitely gets too large once message history gets too long
            return (await groupTable.ExecuteQueryAsync(getMessagesQuery))
                .Select(x => new Message
                {
                    Contents = x.OriginalEntity.Contents,
                    GroupId = groupId,
                    SenderId = x.OriginalEntity.SenderId,
                    Timestamp = x.OriginalEntity.SentTimestamp,
                    SenderName = x.OriginalEntity.SenderName,
                });
        }

        private async Task AddOrUpdateToGroupsList(GroupMetadata group, Guid groupId, int count)
        {
            var allGroupsTable = _tableClient.GetTableReference(TableNames.AllGroups);
            await allGroupsTable.CreateIfNotExistsAsync();

            var groupEntry = new AllGroupsGroupEntry
            {
                Name = group.Name,
                Count = count,
                CreationTime = group.CreationTime
            };
            var addOrUpdateOperation = TableOperation.InsertOrReplace(
                new TableEntityAdapter<AllGroupsGroupEntry>(groupEntry, PartitionNames.Group, groupId.ToIdString()));
            TableResult result = await allGroupsTable.ExecuteAsync(addOrUpdateOperation);
            if (!(result.Result is TableEntityAdapter<AllGroupsGroupEntry>))
            {
                // This isn't fatal. We'll catch it next time we do a group update.
                _logger.LogWarning($"Failed to update group list metadata table with new group: ID - {groupId}, Name - {group.Name}. Status code: {result.HttpStatusCode}");
            }
        }

        // Utility methods and helpers
        private async Task DeleteFromGroupsList(Guid groupId)
        {
            var allGroupsTable = _tableClient.GetTableReference(TableNames.AllGroups);

            TableOperation getGroupOperation = TableOperation.Retrieve<TableEntityAdapter<AllGroupsGroupEntry>>(PartitionNames.Group, groupId.ToIdString());
            TableResult getResult = await allGroupsTable.ExecuteAsync(getGroupOperation);
            if (getResult.Result is TableEntityAdapter<AllGroupsGroupEntry> groupEntry)
            {
                TableOperation deleteOperation = TableOperation.Delete(groupEntry);
                TableResult deleteResult = await allGroupsTable.ExecuteAsync(deleteOperation);
                if (deleteResult.HttpStatusCode != StatusCodes.Status204NoContent)
                {
                    _logger.LogError($"Located, but could not delete, group entry with ID {groupId} from the groups list. Status code: {deleteResult.HttpStatusCode}");
                }
            }
            else
            {
                _logger.LogWarning($"Could not find group entry with ID {groupId} while trying to delete it from the groups list. Status code: {getResult.HttpStatusCode}");
            }
        }

        private async Task<int> GetGroupCount(Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToTableString());
            // Filter on 'Users' and project only the RowKey, to speed up query.
            TableQuery<TableEntityAdapter<UserEntity>> query = new TableQuery<TableEntityAdapter<UserEntity>>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionNames.User))
                .Select(new List<string> { "RowKey" });
            var results = await groupTable.ExecuteQueryAsync(query);
            return results.Count();
        }

        private async Task<GroupMetadata> GetGroupMetadata(Guid groupId)
        {
            var groupTable = _tableClient.GetTableReference(groupId.ToTableString());
            TableOperation getMetadataOp = TableOperation.Retrieve<TableEntityAdapter<GroupMetadata>>(PartitionNames.Metadata, PartitionNames.Metadata);
            TableResult getResult = await groupTable.ExecuteAsync(getMetadataOp);
            if (getResult.Result is TableEntityAdapter<GroupMetadata> metadata)
            {
                return metadata.OriginalEntity;
            }
            else
            {
                return null;
            }
        }

        private async Task AddToUserGroups(Guid userId, Guid groupId, DateTime joinTime)
        {
            var userTable = _tableClient.GetTableReference(userId.ToTableString());
            await userTable.CreateIfNotExistsAsync();
            TableOperation existsCheckOp = TableOperation.Retrieve(PartitionNames.Group, groupId.ToIdString());
            TableResult existsResult = await userTable.ExecuteAsync(existsCheckOp);
            if (existsResult.Result != null)
            {
                // Already exists, bail out.
                return;
            }

            DynamicTableEntity entry = new DynamicTableEntity(PartitionNames.Group, groupId.ToIdString());
            entry.Properties = new Dictionary<string, EntityProperty>
            {
                {"JoinTime", new EntityProperty(joinTime) }
            };
            TableOperation insertGroupOp = TableOperation.Insert(entry);
            TableResult insertResult = await userTable.ExecuteAsync(insertGroupOp);
            if (insertResult.HttpStatusCode != StatusCodes.Status204NoContent)
            {
                _logger.LogError($"Failed to add group {groupId} to user table for user {userId}. Status code: {insertResult.HttpStatusCode}");
            }
        }
    }
}
