using ChatNeat.API.Database.Entities;
using ChatNeat.API.Services;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading.Tasks;

namespace ChatNeat.API.Tests.TableClientTests
{
    [TestClass]
    public class WhenDeletingAGroup : TableClientTestBase
    {
        protected override Task Arrange()
        {
            return base.Arrange();
        }

        [TestMethod]
        public async Task ShouldReturnNotFoundIfGroupDoesntExist()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(false);
            ServiceResult result = await _tableClient.DeleteGroup(Guid.NewGuid());
            Assert.AreEqual(ServiceResult.NotFound, result);
        }

        [TestMethod]
        public async Task ShouldReturnSuccessOnSuccess() // naw, really?
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(It.IsAny<TableOperation>()))
                .ReturnsAsync(new TableResult
                {
                    Result = new TableEntityAdapter<AllGroupsGroupEntry> { ETag = "*" }
                });

            ServiceResult result = await _tableClient.DeleteGroup(Guid.NewGuid());
            Assert.AreEqual(ServiceResult.Success, result);
        }
    }
}
