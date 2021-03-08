using System;

namespace SegmentChallengeWeb.Configuration {
    public class StravaConfiguration {
        public String ClientId { get; set; }
        public String ClientSecret { get; set; }

        public Int32 MaximumRequestsPer15Minutes { get; set; } = 500;
        // TODO
        // public Int32 MaximumRequestsPerDay { get; set; } = 100;
    }
}
