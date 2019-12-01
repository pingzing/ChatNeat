using ChatNeat.API.Database;
using ChatNeat.API.Database.Entities;
using ChatNeat.Models;
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
    public class WhenGettingGroupList : TableClientTestBase
    {
        private List<TableEntityAdapter<AllGroupsGroupEntry>> _groupEntries;
        private List<TableEntityAdapter<AllGroupsGroupEntry>> _groupEntriesPart2;

        protected override Task Arrange()
        {
            _groupEntries = ToGroupEntries(new[]
            {
                new Group
                {
                    Name = "Test group 1",
                    Id = Guid.NewGuid(),
                    Count = 4,
                    CreationTime = DateTime.UtcNow
                },
                new Group
                {
                    Name = "Test group 2",
                    Id = Guid.NewGuid(),
                    Count = 14,
                    CreationTime = DateTime.UtcNow
                },
                new Group
                {
                    Name = "Test group 3",
                    Id = Guid.NewGuid(),
                    Count = 1,
                    CreationTime = DateTime.UtcNow
                }
            }).ToList();

            _groupEntriesPart2 = ToGroupEntries(new[]
{
                new Group
                {
                    Name = "Test group 4",
                    Id = Guid.NewGuid(),
                    Count = 3,
                    CreationTime = DateTime.UtcNow
                },
                new Group
                {
                    Name = "Test group 5",
                    Id = Guid.NewGuid(),
                    Count = 9,
                    CreationTime = DateTime.UtcNow
                },
                new Group
                {
                    Name = "Test group 6",
                    Id = Guid.NewGuid(),
                    Count = 18,
                    CreationTime = DateTime.UtcNow
                }
            }).ToList();

            Mock.Get(_mockCloudTable).Setup(x => x.CreateIfNotExistsAsync()).ReturnsAsync(true);
            Mock.Get(_mockCloudTable).SetupSequence(x => x.ExecuteQuerySegmentedAsync(
                It.IsAny<TableQuery<TableEntityAdapter<AllGroupsGroupEntry>>>(), It.IsAny<TableContinuationToken>()))
                .ReturnsAsync(GetMockTableQuerySegment(_groupEntries, true))
                .ReturnsAsync(GetMockTableQuerySegment(_groupEntriesPart2, false));

            return Task.CompletedTask;
        }

        private IEnumerable<TableEntityAdapter<AllGroupsGroupEntry>> ToGroupEntries(IEnumerable<Group> groups)
        {
            return groups.Select(x =>
            {
                var entry = new AllGroupsGroupEntry
                {
                    Name = x.Name,
                    Count = x.Count,
                    CreationTime = x.CreationTime.UtcDateTime
                };
                return new TableEntityAdapter<AllGroupsGroupEntry>(entry, PartitionNames.Group, x.Id.ToString("N"));
            });
        }

        [TestMethod]
        public async Task ShouldGetAllGroups()
        {
            var groups = await _tableClient.GetGroupList();
            Assert.AreEqual(_groupEntries.Count + _groupEntriesPart2.Count, groups.Count());
        }

        [TestMethod]
        public async Task ShouldContainAllIds()
        {
            var groups = await _tableClient.GetGroupList();
            Assert.IsTrue(groups.All(x =>
                _groupEntries.Concat(_groupEntriesPart2)
                    .Any(y => Guid.Parse(y.RowKey) == x.Id)));
        }

        [TestMethod]
        public async Task ShouldNotResultInDuplicates()
        {
            var groups = await _tableClient.GetGroupList();
            var distinctGroups = groups.GroupBy(x => x.Id).Select(x => x.First());
            Assert.AreEqual(groups.Count(), distinctGroups.Count());
        }
    }
}
