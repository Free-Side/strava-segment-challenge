using System;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType(nameof(ChallengeId), nameof(MaximumAge))]
    public class AgeGroup {
        public Int64 ChallengeId { get; set; }
        public Int32 MaximumAge { get; set; }
        public String Description { get; set; }
    }
}
