using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
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

        [Column("ChallengeType")]
        public ChallengeType Type { get; set; }

        public Boolean UseMovingTime { get; set; }

        // There's no reason to send this to the client.
        [JsonIgnore]
        public String GpxData { get; set; }

        // Don't expose publicly
        [JsonIgnore]
        public String InviteCode { get; set; }

        public String RegistrationLink { get; set; }

        // Fetch this in a more cache-able way
        [JsonIgnore]
        public Byte[] RouteMapImage { get; set; }

        [NotMapped]
        public Boolean HasRouteMap => this.RouteMapImage != null;

        [NotMapped]
        public Boolean RequiresInviteCode => !String.IsNullOrEmpty(this.InviteCode);
    }

    public enum ChallengeType {
        Fastest = 0,
        MostLaps = 1
    }
}
