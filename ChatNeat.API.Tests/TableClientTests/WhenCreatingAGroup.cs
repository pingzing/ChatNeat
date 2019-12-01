using ChatNeat.API.Database.Entities;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading.Tasks;

namespace ChatNeat.API.Tests.TableClientTests
{
    [TestClass]
    public class WhenCreatingAGroup : TableClientTestBase
    {
        protected override Task Arrange()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.CreateIfNotExistsAsync()).ReturnsAsync(true);
            return Task.CompletedTask;
        }

        [TestMethod]
        public async Task ShouldRollbackOnFailure()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(It.IsAny<TableOperation>()))
                .ReturnsAsync(new TableResult
                {
                    HttpStatusCode = StatusCodes.Status400BadRequest,
                    Result = null
                });

            var result = await _tableClient.CreateGroup("testString");
            Mock.Get(_mockCloudTable).Verify(x => x.DeleteAsync());
        }

        [DataTestMethod]
        [DataRow("Test")]
        [DataRow("NöwWithÜmläüts")]
        [DataRow("田中さんにあげて下さい")]
        [DataRow("表")]
        [DataRow("👩🏽")]
        [DataRow("💙")]
        public async Task NameShouldRemainUnmangled(string expected)
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(It.IsAny<TableOperation>()))
                .ReturnsAsync(new TableResult
                {
                    HttpStatusCode = StatusCodes.Status201Created,
                    Result = new TableEntityAdapter<GroupMetadata>
                    {
                        OriginalEntity = new GroupMetadata
                        {
                            CreationTime = DateTime.UtcNow,
                            Name = expected
                        }
                    }
                });

            var createdGroup = await _tableClient.CreateGroup(expected);
            Assert.AreEqual(expected, createdGroup.Name);
        }

        [TestMethod]
        public async Task ShouldReturnGroupOnSuccess()
        {
            string groupName = "Test!";
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(It.IsAny<TableOperation>()))
                .ReturnsAsync(new TableResult
                {
                    HttpStatusCode = StatusCodes.Status201Created,
                    Result = new TableEntityAdapter<GroupMetadata>
                    {
                        OriginalEntity = new GroupMetadata
                        {
                            CreationTime = DateTime.UtcNow,
                            Name = groupName
                        }
                    }
                });

            Group group = await _tableClient.CreateGroup(groupName);
            Assert.IsNotNull(group);
            Assert.AreNotEqual(default(DateTimeOffset), group.CreationTime);
            Assert.AreNotEqual(default(Guid), group.Id);
            Assert.AreEqual(groupName, group.Name);
        }
    }
}
