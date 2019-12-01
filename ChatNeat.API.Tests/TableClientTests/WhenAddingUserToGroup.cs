using ChatNeat.API.Database;
using ChatNeat.API.Database.Entities;
using ChatNeat.API.Services;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatNeat.API.Tests.TableClientTests
{
    [TestClass]
    public class WhenAddingUserToGroup : TableClientTestBase
    {
        private IEnumerable<TableEntityAdapter<UserEntity>> _tooManyUsers;
        private IEnumerable<TableEntityAdapter<UserEntity>> _smallNumberOfUsers;

        protected override Task Arrange()
        {
            _tooManyUsers = Enumerable.Range(0, (int)ChatNeatTableClient.MaxGroupSize + 5)
                .Select(_ => new TableEntityAdapter<UserEntity>());

            _smallNumberOfUsers = Enumerable.Range(0, 5)
                .Select(_ => new TableEntityAdapter<UserEntity>());

            return Task.CompletedTask;
        }

        private void ArrangeSuccess()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);

            Mock.Get(_mockCloudTable).SetupSequence(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve)))
                .ReturnsAsync(new TableResult { Result = null }) // This for the first GetUser()
                .ReturnsAsync(new TableResult // This corresponds to the call to GetGroupMetadata()
                {
                    Result = new TableEntityAdapter<GroupMetadata>()
                    {
                        OriginalEntity = new GroupMetadata { Name = "TestGroupName", CreationTime = DateTime.UtcNow }
                    }
                })
                .ReturnsAsync(new TableResult()); // This corresponds to the call inside AddToUserGroups()

            // Corresponds to the call to GetGroupCount()
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteQuerySegmentedAsync(
                It.IsAny<TableQuery<TableEntityAdapter<UserEntity>>>(), It.IsAny<TableContinuationToken>())
            ).ReturnsAsync(GetMockTableQuerySegment(_smallNumberOfUsers));

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Insert))
            ).ReturnsAsync(new TableResult { HttpStatusCode = StatusCodes.Status204NoContent });

            Mock.Get(_mockCloudTable).Setup(x => x.CreateIfNotExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.InsertOrReplace))
            ).ReturnsAsync(new TableResult());

            // The final two calls to AddOrUpdateToGroupsList() and AddToUserGroups()
            // both end up wandering down failure paths, but since we're 
            // not testing those methods (and their success doesn't affect the
            // reported success of this method) we don't care.
        }

        [TestMethod]
        public async Task ShouldReturnNotFoundIfGroupDoesntExist()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(false);
            ServiceResult result = await _tableClient.AddUserToGroup(new User(), Guid.NewGuid());
            Assert.AreEqual(ServiceResult.NotFound, result);
        }

        [TestMethod]
        public async Task ShouldReturnSuccessIfUserAlreadyInGroup()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable)
                .Setup(x => x.ExecuteAsync(It.IsAny<TableOperation>()))
                .ReturnsAsync(new TableResult
                {
                    Result = new TableEntityAdapter<UserEntity>()
                });
            ServiceResult result = await _tableClient.AddUserToGroup(new User(), Guid.NewGuid());
            Assert.AreEqual(ServiceResult.Success, result);
        }

        [TestMethod]
        public async Task ShouldReturnInvalidArgIfGroupIsFull()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                    It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
                ).ReturnsAsync(new TableResult { Result = null });

            // Corresponds to the call to GetGroupCount()
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteQuerySegmentedAsync(
                It.IsAny<TableQuery<TableEntityAdapter<UserEntity>>>(), It.IsAny<TableContinuationToken>()))
                .ReturnsAsync(GetMockTableQuerySegment(_tooManyUsers));

            var result = await _tableClient.AddUserToGroup(new User(), Guid.NewGuid());
            Assert.AreEqual(ServiceResult.InvalidArguments, result);
        }

        [TestMethod]
        public async Task ShouldReturnServerErrorIfAddFails()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult { Result = null });

            // Corresponds to the call to GetGroupCount()
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteQuerySegmentedAsync(
                It.IsAny<TableQuery<TableEntityAdapter<UserEntity>>>(), It.IsAny<TableContinuationToken>())
            ).ReturnsAsync(GetMockTableQuerySegment(_smallNumberOfUsers));

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Insert))
            ).ReturnsAsync(new TableResult { HttpStatusCode = StatusCodes.Status400BadRequest });

            var result = await _tableClient.AddUserToGroup(new User(), Guid.NewGuid());

            Assert.AreEqual(ServiceResult.ServerError, result);
        }

        [TestMethod]
        public async Task ShouldReturnSuccessOnSuccess()
        {
            ArrangeSuccess();

            var result = await _tableClient.AddUserToGroup(new User(), Guid.NewGuid());

            Assert.AreEqual(ServiceResult.Success, result);
        }

        [TestMethod]
        public async Task SuccessPathShouldUpdateGroupsList()
        {
            ArrangeSuccess();
            // The only time we call InsertOrReplace on this code path is when we're updating the Groups List
            // This test might be too fragile to be useful, but we'll see

            var result = await _tableClient.AddUserToGroup(new User(), Guid.NewGuid());

            Mock.Get(_mockCloudTable)
                .Verify(x => x.ExecuteAsync(
                    It.Is<TableOperation>(y => y.OperationType == TableOperationType.InsertOrReplace)));
        }
    }
}
