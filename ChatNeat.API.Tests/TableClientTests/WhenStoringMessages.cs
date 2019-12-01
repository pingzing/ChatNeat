using ChatNeat.API.Database.Entities;
using ChatNeat.API.Services;
using ChatNeat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ChatNeat.API.Tests.TableClientTests
{
    [TestClass]
    public class WhenStoringMessages : TableClientTestBase
    {
        [TestMethod]
        public async Task ShouldReturnNotFoundIfGroupDoesntExist()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(false);

            ServiceResult result = await _tableClient.StoreMessage(new Message { Contents = "Test!" });
            Assert.AreEqual(ServiceResult.NotFound, result);
        }

        [TestMethod]
        public async Task ShouldReturnNotFoundIfNotInGroup()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult());

            ServiceResult result = await _tableClient.StoreMessage(new Message { Contents = "Test!" });
            Assert.AreEqual(ServiceResult.NotFound, result);
        }

        [TestMethod]
        public async Task ShouldReturnServerErrorIfAddingFails()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult
            {
                Result = new TableEntityAdapter<UserEntity>()
            });

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Insert))
            ).ReturnsAsync(new TableResult { HttpStatusCode = StatusCodes.Status400BadRequest });

            ServiceResult result = await _tableClient.StoreMessage(new Message { Contents = "Test!" });
            Assert.AreEqual(ServiceResult.ServerError, result);
        }

        [TestMethod]
        public async Task ShouldReturnSuccessOnSuccess()
        {
            Mock.Get(_mockCloudTable).Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Retrieve))
            ).ReturnsAsync(new TableResult
            {
                Result = new TableEntityAdapter<UserEntity>()
            });

            Mock.Get(_mockCloudTable).Setup(x => x.ExecuteAsync(
                It.Is<TableOperation>(y => y.OperationType == TableOperationType.Insert))
            ).ReturnsAsync(new TableResult { HttpStatusCode = StatusCodes.Status204NoContent });

            ServiceResult result = await _tableClient.StoreMessage(new Message { Contents = "Test!" });
            Assert.AreEqual(ServiceResult.Success, result);
        }
    }
}
