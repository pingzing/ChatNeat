using System;

namespace ChatNeat.API.Database.Extensions
{
    public static class GuidExtensions
    {
        /// <summary>
        /// Returns this GUID in .ToString("N") format (all lowercase, no dashes).
        /// </summary>
        public static string ToIdString(this Guid idGuid)
        {
            return idGuid.ToString("N");
        }
    }
}
