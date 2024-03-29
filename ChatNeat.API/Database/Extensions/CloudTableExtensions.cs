﻿using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatNeat.API.Database.Extensions
{
    public static class CloudTableExtensions
    {
        public static async Task<IEnumerable<T>> ExecuteQueryAsync<T>(this CloudTable table, TableQuery<T> query, CancellationToken ct = default, Action<IEnumerable<T>> onProgress = null)
            where T : ITableEntity, new()
        {
            var items = new List<T>();
            TableContinuationToken token = null;

            do
            {
                TableQuerySegment<T> segment = await table.ExecuteQuerySegmentedAsync(query, token);
                token = segment.ContinuationToken;
                items.AddRange(segment);
                onProgress?.Invoke(items);
            } while (token != null && !ct.IsCancellationRequested);

            return items;
        }
    }
}
