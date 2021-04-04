using System;

namespace SegmentChallengeWeb.Configuration {
    public class SegmentChallengeConfiguration {
        public String BaseUrl { get; set; }

        /// <summary>
        /// This prefix is appended to the URL used for Strava authentication
        /// callbacks. It can be used if you need to customize the path where
        /// the API is reached.
        /// </summary>
        /// <remarks>
        /// The SegmentChallenge server does not handle this prefix during
        /// routing, so if this is used it is necessary to have a URL rewriting
        /// proxy handling requests.
        /// </remarks>
        public String CallbackUrlPrefix { get; set; }

        /// <summary>
        /// The expiration time for login tokens, specified in days.
        /// </summary>
        public Int32 TokenExpiration { get; set; }
        public String SecretKey { get; set; }
        public String SupportContact { get; set; }
        public Int64[] Administrators { get; set; }

        public Boolean AutoRefresh { get; set; }

        /// <summary>
        /// The interval between effort refreshes in minutes.
        /// </summary>
        public Int32 RefreshInterval { get; set; } = 30;
    }
}
