using System;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType]
    public class Effort {
        public Int64 Id { get; set; }
        public Int64 AthleteId { get; set; }
        public Int64 ActivityId { get; set; }
        public Int64 SegmentId { get; set; }
        public Int32 ElapsedTime { get; set; }
        public DateTime StartDate { get; set; }
    }
}
