using System;

namespace SegmentChallengeWeb {
    public static class HelperFunctions {
        private static readonly DateTime
            Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static Int32 ToRacingAge(this DateTime birthDate, DateTime eventDate) {
            return eventDate.Year - birthDate.Year;
        }

        public static Int64 ToUnixTime(this DateTime dateTime) {
            return (Int64)dateTime.ToUniversalTime().Subtract(Epoch).TotalSeconds;
        }
    }
}
