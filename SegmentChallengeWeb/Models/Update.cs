using System;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType]
    public class Update {
        public Int32 Id { get; set; }
        public Int32 AthleteCount { get; set; }
        public Int32 ActivityCount { get; set; }
        public Int32 SkippedActivityCount { get; set; }
        public Int32 EffortCount { get; set; }
        public Int32 ErrorCount { get; set; }
        public Single Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public Int64? AthleteId { get; set; }
    }
}
