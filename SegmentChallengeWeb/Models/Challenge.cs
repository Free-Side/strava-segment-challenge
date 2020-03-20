using System;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType]
    public class Challenge {
        public Int32 Id { get; set; }
        public String Name { get; set; }
        public String DisplayName { get; set; }
        public String Description { get; set; }
        public Int64 SegmentId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
