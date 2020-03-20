using System;

namespace SegmentChallengeWeb.Persistence {
    [AttributeUsage(AttributeTargets.Class)]
    public class DatabaseTypeAttribute : Attribute {
        public String[] PrimaryKey { get; set; }

        public DatabaseTypeAttribute(params String[] primaryKey) {
            if (primaryKey != null && primaryKey.Length > 0) {
                this.PrimaryKey = primaryKey;
            }
        }
    }
}
