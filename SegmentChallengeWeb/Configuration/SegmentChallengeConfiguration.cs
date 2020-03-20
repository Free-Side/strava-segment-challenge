using System;

namespace SegmentChallengeWeb.Configuration {
    public class SegmentChallengeConfiguration {
        public String BaseUrl { get; set; }
        public Int32 TokenExpiration { get; set; }
        public String SecretKey { get; set; }
        public String SupportContact { get; set; }
        public Int64[] Administrators { get; set; }
    }
}
