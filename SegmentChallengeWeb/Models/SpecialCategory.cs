using System;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType(nameof(ChallengeId), nameof(SpecialCategoryId))]
    public class SpecialCategory {
        public Int64 ChallengeId { get; set; }
        public Int32 SpecialCategoryId { get; set; }
        public String CategoryName { get; set; }
    }
}
