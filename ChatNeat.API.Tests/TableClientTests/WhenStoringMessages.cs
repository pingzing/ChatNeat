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
    public class WhenStoringMessages : TableClientTestBase
    {
        [TestMethod]
        public async Task ShouldReturnNullIfGroupDoesntExist()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(false);

            Message result = await _tableClient.StoreMessage(new Message { Contents = "Test!" });
            Assert.AreEqual((Message)null, result);
        }

        [TestMethod]
        public async Task ShouldReturnNotFoundIfNotInGroup()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult());

            Message result = await _tableClient.StoreMessage(new Message { Contents = "Test!" });
            Assert.AreEqual((Message)null, result);
        }

        [TestMethod]
        public async Task ShouldReturnNullIfAddingFails()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult
            {
                Result = new TableEntityAdapter<UserEntity>(new UserEntity
                {
                    Name = "TestUserDBName"
                })
            });

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Insert))
            ).ReturnsAsync(new TableResult { HttpStatusCode = StatusCodes.Status400BadRequest });

            Message result = await _tableClient.StoreMessage(new Message { Contents = "Test!" });
            Assert.AreEqual((Message)null, result);
        }

        [TestMethod]
        public async Task ShouldReturnSuccessOnSuccess()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult
            {
                Result = new TableEntityAdapter<UserEntity>(new UserEntity
                {
                    Name = "TestUserDBName"
                })
            });

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Insert))
            ).ReturnsAsync(new TableResult { HttpStatusCode = StatusCodes.Status204NoContent });

            Message sentMessage = new Message { Contents = "Test!" };

            Message result = await _tableClient.StoreMessage(sentMessage);
            Assert.AreEqual(sentMessage.Contents, result.Contents);
            Assert.AreNotEqual(default(DateTimeOffset), sentMessage.Timestamp);
        }

        [TestMethod]
        public async Task ShouldIgnoreUserSentNameIfDatabaseDisagrees()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult
            {
                Result = new TableEntityAdapter<UserEntity>(new UserEntity
                {
                    Name = "TestUserDBName"
                })
            });

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Insert))
            ).ReturnsAsync(new TableResult { HttpStatusCode = StatusCodes.Status204NoContent });

            Message sentMessage = new Message { Contents = "Test!", SenderName = "User spoof attempt!" };

            Message result = await _tableClient.StoreMessage(sentMessage);
            Assert.AreNotEqual("User spoof attempt!", result.SenderName);
            Assert.AreEqual("TestUserDBName", result.SenderName);
        }
    }
}
