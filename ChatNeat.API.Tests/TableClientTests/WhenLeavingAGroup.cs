using ChatNeat.API.Database.Entities;
using ChatNeat.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatNeat.API.Tests.TableClientTests
{
    [TestClass]
    public class WhenLeavingAGroup : TableClientTestBase
    {
        private IEnumerable<TableEntityAdapter<UserEntity>> _smallNumberOfUsers;

        protected override Task Arrange()
        {
            _smallNumberOfUsers = Enumerable.Range(0, 5)
                .Select(_ => new TableEntityAdapter<UserEntity>());

            return Task.CompletedTask;
        }

        [TestMethod]
        public async Task ShouldReturnNotFoundIfGroupDoesntExist()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(false);
            ServiceResult result = await _tableClient.LeaveGroup(Guid.NewGuid(), Guid.NewGuid());
            Assert.AreEqual(ServiceResult.NotFound, result);
        }

        [TestMethod]
        public async Task ShouldReturnNotFoundIfUserDoesntExist()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult());

            ServiceResult result = await _tableClient.LeaveGroup(Guid.NewGuid(), Guid.NewGuid());
            Assert.AreEqual(ServiceResult.NotFound, result);
        }

        [TestMethod]
        public async Task ShouldReturnServerErrorOnFailure()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult
            {
                Result = new TableEntityAdapter<UserEntity>
                {
                    ETag = "*",
                    OriginalEntity = new UserEntity { Name = "TestUser" }
                }
            });
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Delete))
            ).ReturnsAsync(new TableResult());

            ServiceResult result = await _tableClient.LeaveGroup(Guid.NewGuid(), Guid.NewGuid());
            Assert.AreEqual(ServiceResult.ServerError, result);
        }

        [TestMethod]
        public async Task ShouldReturnSuccessOnSuccess()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).SetupSequence(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult
            {
                Result = new TableEntityAdapter<UserEntity>
                {
                    ETag = "*",
                    OriginalEntity = new UserEntity { Name = "TestUser" }
                }
            })
            .ReturnsAsync(new TableResult // This corresponds to the call to GetGroupMetadata()
            {
                Result = new TableEntityAdapter<GroupMetadata>()
                {
                    OriginalEntity = new GroupMetadata { Name = "TestGroupName", CreationTime = DateTime.UtcNow }
                }
            })
            .ReturnsAsync(new TableResult());

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Delete))
            ).ReturnsAsync(new TableResult() { HttpStatusCode = StatusCodes.Status204NoContent });

            // Corresponds to the call to GetGroupCount()
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteQuerySegmentedAsync(
                It.IsAny<TableQuery<TableEntityAdapter<UserEntity>>>(), It.IsAny<TableContinuationToken>())
            ).ReturnsAsync(GetMockTableQuerySegment(_smallNumberOfUsers));
            Mock.Get(_mockCloudTable).Setup(x => x.CreateIfNotExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.InsertOrReplace))
            ).ReturnsAsync(new TableResult { Result = new object() });

            ServiceResult result = await _tableClient.LeaveGroup(Guid.NewGuid(), Guid.NewGuid());
            Assert.AreEqual(ServiceResult.Success, result);
        }

    }
}
