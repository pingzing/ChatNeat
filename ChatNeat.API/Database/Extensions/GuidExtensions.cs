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

        // Tables must begin with a letter. GUIDs don't always start with letters.
        public const string TablePrefix = "T";

        /// <summary>
        /// Returns this GUID in .ToString("N") format (all lowercase, no dashes), prefixed with <see cref="TablePrefix"/>.
        /// </summary>
        public static string ToTableString(this Guid idString)
        {
            return $"{TablePrefix}{idString.ToString("N")}";
        }
    }
}
