using System;
using System.Collections.Generic;
using System.Text;

namespace ChatNeat.API.Database
{
    public class TableNames
    {
        public const string AllGroups = "AllGroups";
        // All other tables are named by their group ID.
    }

    public class PartitionNames
    {
        // Used by the AllGroups table
        public const string Group = "Group";
        // All other tables (aka groups)
        public const string Metadata = "Metadata";
        public const string User = "User";
        public const string Message = "Message";
    }

    public class RowKeys
    {
        // AllGroups table, part of the "Table" partition
        public const string GroupInfo = "GroupInfo";

        // Group table, part of the "Group" partition
        public const string GroupId = "GroupId";

        // Group table, part of the "User" partition
        public const string UserId = "UserId";
    }
}
