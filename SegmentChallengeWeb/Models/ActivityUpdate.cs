using System;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType(nameof(ChallengeId), nameof(ActivityId))]
    public class ActivityUpdate {
        public Int32 ChallengeId { get; set; }
        public Int64 ActivityId { get; set; }
        public Int64 AthleteId { get; set; }
        public Int32 UpdateId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
