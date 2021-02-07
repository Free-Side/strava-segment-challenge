using System;
using System.Text;
using System.Text.Json.Serialization;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Models {
    [DatabaseType]
    public class Athlete {
        public Int64 Id { get; set; }
        public String Username { get; set; }
        public String FirstName { get; set; }
        public String LastName { get; set; }
        public Char? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public String Email { get; set; }
        public String ProfilePicture { get; set; }
        public String AccessToken { get; set; }
        public String RefreshToken { get; set; }
        public DateTime TokenExpiration { get; set; }

        [JsonIgnore]
        public String PasswordHash { get; set; }

        public String GetDisplayName() {
            var displayName = new StringBuilder();

            if (!String.IsNullOrEmpty(this.FirstName)) {
                displayName.Append(this.FirstName);
            }

            if (!String.IsNullOrEmpty(this.LastName)) {
                if (displayName.Length > 0) {
                    displayName.Append(' ');
                }

                displayName.Append(this.LastName);
            }

            if (displayName.Length == 0) {
                displayName.Append(
                    !String.IsNullOrEmpty(this.Username) ?
                        this.Username :
                        $"User {this.Id}"
                );
            }

            return displayName.ToString();
        }
    }
}
