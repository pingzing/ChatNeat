using ChatNeat.API.Database;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ChatNeat.API.Tests.TableClientTests
{
    public abstract class TableClientTestBase
    {
        protected CloudTableClient _mockTableClient;
        protected CloudTable _mockCloudTable;
        protected ILogger<ChatNeatTableClient> _mockLogger;

        protected ChatNeatTableClient _tableClient;

        [TestInitialize]
        public async Task Setup()
        {
            var mockCloudTable = new Mock<CloudTable>(MockBehavior.Strict, new object[] { new Uri("http://fakeurl.com"), null });
            _mockCloudTable = mockCloudTable.Object;

            var mockTableClient = new Mock<CloudTableClient>(MockBehavior.Strict, new object[]
            {
                new StorageUri(new Uri("http://fakeurl.com")),
                new StorageCredentials("fakeaccountname", "fakeKeyValue")
            });
            _mockTableClient = mockTableClient.Object;
            Mock.Get(_mockTableClient).Setup(x => x.GetTableReference(It.IsAny<string>()))
                .Returns(_mockCloudTable);

            _mockLogger = Mock.Of<ILogger<ChatNeatTableClient>>();

            await Arrange();

            _tableClient = new ChatNeatTableClient(_mockTableClient, _mockLogger);
        }

        protected virtual Task Arrange()
        {
            return Task.CompletedTask;
        }

        // Incredibly, this is the easiest way to 'mock' this.
        protected TableQuerySegment<T> GetMockTableQuerySegment<T>(IEnumerable<T> items, bool withContinuation = false)
        {
            if (!withContinuation)
            {
                var ctor = typeof(TableQuerySegment<T>)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(c => c.GetParameters().Count() == 1);

                return ctor.Invoke(new object[] { new List<T>(items) }) as TableQuerySegment<T>;
            }
            else
            {
                var ctor = typeof(TableQuerySegment<T>)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Skip(1) // The second constructor is the one that takes a ResultSegment.
                    .FirstOrDefault(c => c.GetParameters().Count() == 1);

                var resultSegmentCtor = typeof(ResultSegment<T>)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(c => c.GetParameters().Count() == 1);

                var resultSegment = resultSegmentCtor.Invoke(new object[] { new List<T>(items) }) as ResultSegment<T>;
                var tokenProperty = typeof(ResultSegment<T>)
                    .GetProperty(nameof(ResultSegment<T>.ContinuationToken));
                tokenProperty.SetValue(resultSegment, new TableContinuationToken(), BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);

                return ctor.Invoke(new object[] { resultSegment }) as TableQuerySegment<T>;
            }
        }
    }
}
