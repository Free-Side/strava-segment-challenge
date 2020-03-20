using System;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType(nameof(ChallengeId), nameof(AthleteId))]
    public class ChallengeRegistration {
        public Int32 ChallengeId { get; set; }
        public Int64 AthleteId { get; set; }
    }
}
