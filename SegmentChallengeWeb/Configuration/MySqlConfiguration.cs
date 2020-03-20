using System;

namespace SegmentChallengeWeb.Configuration {
    public class MySqlConfiguration {
        public String Host { get; set; }
        public UInt32 Port { get; set; }
        public String Database { get; set; }
        public String User { get; set; }
        public String Password { get; set; }
    }
}
