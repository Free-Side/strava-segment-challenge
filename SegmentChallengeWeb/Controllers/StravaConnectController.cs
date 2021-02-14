using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using SegmentChallengeWeb.Configuration;
using SegmentChallengeWeb.Models;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Controllers {
    [Route("api/connect")]
    public class StravaConnectController : ControllerBase {
        private static readonly Random rand = new Random();

        private readonly IOptions<SegmentChallengeConfiguration> challengeConfiguration;
        private readonly IOptions<StravaConfiguration> stravaConfiguration;
        private readonly Func<DbConnection> dbConnectionFactory;
        private readonly StravaApiHelper apiHelper;
        private readonly ILogger<StravaConnectController> logger;

        public StravaConnectController(
            IOptions<SegmentChallengeConfiguration> challengeConfiguration,
            IOptions<StravaConfiguration> stravaConfiguration,
            Func<DbConnection> dbConnectionFactory,
            StravaApiHelper apiHelper,
            ILogger<StravaConnectController> logger) {
            this.challengeConfiguration = challengeConfiguration;
            this.stravaConfiguration = stravaConfiguration;
            this.dbConnectionFactory = dbConnectionFactory;
            this.apiHelper = apiHelper;
            this.logger = logger;
        }

        // GET
        [HttpGet("login")]
        public IActionResult Login([FromQuery] String returnUrl) {
            Int32 state;
            lock (rand) {
                state = rand.Next();
            }

            Response.Cookies.Append("authentication_state", state.ToString());
            return Redirect(BuildAuthenticationRedirectUri(state, returnUrl).ToString());
        }

        [HttpGet("authorize")]
        public async Task<IActionResult> Authorize(
            [FromQuery] String state,
            [FromQuery] String code,
            [FromQuery] String scope,
            [FromQuery] String returnUrl,
            CancellationToken cancellationToken) {
            var expected_state = Request.Cookies["authentication_state"];
            if (!String.Equals(state, expected_state)) {
                this.logger.LogWarning(
                    "The state {ActualState} did not match the expected authentication state {ExpectedState}",
                    state,
                    expected_state
                );
            }

            var codeExchangeClient =
                new HttpClient {
                    BaseAddress = new Uri("https://www.strava.com")
                };

            codeExchangeClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response =
                await this.apiHelper.MakeThrottledApiRequest(
                    () => codeExchangeClient.PostAsync(
                        "/api/v3/oauth/token",
                        new FormUrlEncodedContent(new Dictionary<string, string> {
                            { "client_id", this.stravaConfiguration.Value.ClientId },
                            { "client_secret", this.stravaConfiguration.Value.ClientSecret },
                            { "code", code },
                            { "grant_type", "authorization_code" }
                        }),
                        cancellationToken
                    ),
                    cancellationToken);

            if (response.IsSuccessStatusCode) {
                var session =
                    await response.Content.ReadAsAsync<StravaSession>(cancellationToken);

                await using var connection = this.dbConnectionFactory();
                await connection.OpenAsync(cancellationToken);

                await using var dbContext = new SegmentChallengeDbContext(connection);

                var athleteTable = dbContext.Set<Athlete>();
                // Does user exist? If not create them.
                var existingAthlete =
                    await athleteTable.SingleOrDefaultAsync(a => a.Id == session.Athlete.Id,
                        cancellationToken);

                EntityEntry<Athlete> newAthlete = null;
                if (existingAthlete == null) {
                    newAthlete = await athleteTable.AddAsync(
                        new Athlete {
                            Id = session.Athlete.Id,
                            Username = session.Athlete.Username,
                            FirstName = session.Athlete.FirstName,
                            LastName = session.Athlete.LastName,
                            Gender = !String.IsNullOrEmpty(session.Athlete.Sex) ?
                                session.Athlete.Sex[0] :
                                (Char?)null,
                            ProfilePicture =
                                session.Athlete.ProfileMedium ?? session.Athlete.Profile,
                            AccessToken = session.AccessToken,
                            RefreshToken = session.RefreshToken,
                            TokenExpiration =
                                StravaApiHelper.DateTimeFromUnixTime(session.ExpiresAt)
                        },
                        cancellationToken
                    );
                } else {
                    existingAthlete.Username = session.Athlete.Username;
                    existingAthlete.FirstName = session.Athlete.FirstName;
                    existingAthlete.LastName = session.Athlete.LastName;
                    if (!String.IsNullOrEmpty(session.Athlete.Sex)) {
                        existingAthlete.Gender = session.Athlete.Sex[0];
                    }

                    existingAthlete.ProfilePicture =
                        session.Athlete.ProfileMedium ?? session.Athlete.Profile;
                    existingAthlete.AccessToken = session.AccessToken;
                    existingAthlete.RefreshToken = session.RefreshToken;
                    existingAthlete.TokenExpiration =
                        StravaApiHelper.DateTimeFromUnixTime(session.ExpiresAt);

                    athleteTable.Update(existingAthlete);
                }

                var changes = await dbContext.SaveChangesAsync(cancellationToken);

                if (changes != 1) {
                    logger.LogWarning(
                        $"Unexpected number of rows changed {(existingAthlete == null ? "creating" : "updating")} Athlete {{AthleteId}} ({{RowsChanged}})",
                        session.Athlete.Id,
                        changes
                    );
                }

                Response.Cookies.Append(
                    "id_token",
                    CreateAthleteJwt(
                        this.challengeConfiguration.Value,
                        existingAthlete ?? newAthlete?.Entity),
                    new CookieOptions {
                        Expires = DateTime.UtcNow.AddDays(this.challengeConfiguration.Value.TokenExpiration)
                    }
                );


                return Redirect(String.IsNullOrEmpty(returnUrl) ? "/" : HttpUtility.UrlDecode(returnUrl));
            } else {
                logger.LogError(
                    "Authentication Failed with HTTP Status {StatusCode}: {Content}",
                    response.StatusCode,
                    await response.Content.ReadAsStringAsync()
                );

                return this.Problem(
                    $"An unexpected error occurred. Please contact {this.challengeConfiguration.Value.SupportContact}");
            }
        }

        // GET
        [HttpGet("logout")]
        public IActionResult Logout() {
            Response.Cookies.Append("id_token", "",
                new CookieOptions { Expires = DateTime.UtcNow.AddDays(-1) });
            return Redirect("/");
        }

        public static String CreateAthleteJwt(
            SegmentChallengeConfiguration configuration,
            Athlete athlete) {

            var tokenHandler = new JwtSecurityTokenHandler();
            var claims = CreateAthleteClaims(configuration, athlete);

            // Create JWToken
            var token = tokenHandler.CreateJwtSecurityToken(
                issuer: configuration.BaseUrl,
                audience: configuration.BaseUrl,
                subject: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(configuration.TokenExpiration),
                signingCredentials:
                new SigningCredentials(
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration.SecretKey)),
                    SecurityAlgorithms.HmacSha256Signature)
            );

            return tokenHandler.WriteToken(token);
        }

        private Uri BuildAuthenticationRedirectUri(Int32 state, String returnUrl = null) {
            var baseUrl =
                $"{this.challengeConfiguration.Value.BaseUrl}{this.challengeConfiguration.Value.CallbackUrlPrefix}/api/connect/authorize";

            var redirectUri =
                String.IsNullOrEmpty(returnUrl) ?
                    baseUrl :
                    $"{baseUrl}?returnUrl={HttpUtility.UrlEncode(returnUrl)}";

            var uriBuilder = new UriBuilder {
                Scheme = "https",
                Host = "www.strava.com",
                Path = "/oauth/mobile/authorize",
                Query = ToQueryString(new Dictionary<String, String> {
                    { "client_id", this.stravaConfiguration.Value.ClientId },
                    { "redirect_uri", redirectUri },
                    { "response_type", "code" },
                    { "approval_prompt", "auto" },
                    { "scope", "activity:read" },
                    { "state", state.ToString() }
                })
            };

            return uriBuilder.Uri;
        }

        private static String ToQueryString(Dictionary<String, String> parameters) {
            return String.Join(
                "&",
                parameters.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}")
            );
        }

        public static ClaimsIdentity CreateAthleteClaims(
            SegmentChallengeConfiguration configuration,
            Athlete athlete) {

            var claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim("sub", athlete.Id.ToString()));
            claimsIdentity.AddClaim(new Claim("name", athlete.GetDisplayName()));

            claimsIdentity.AddClaim(new Claim("user_data", JsonConvert.SerializeObject(new {
                profile_picture = athlete.ProfilePicture,
                birth_date = athlete.BirthDate?.ToString("yyyy-MM-dd"),
                gender = athlete.Gender,
                email = athlete.Email,
                is_admin = configuration.Administrators.Contains(athlete.Id)
            })));

            return claimsIdentity;
        }
    }

    public class StravaSession {
        [JsonProperty("token_type")]
        public String TokenType { get; set; }

        [JsonProperty("expires_at")]
        public Int32 ExpiresAt { get; set; }

        [JsonProperty("expires_in")]
        public Int32 ExpiresIn { get; set; }

        [JsonProperty("refresh_token")]
        public String RefreshToken { get; set; }

        [JsonProperty("access_token")]
        public String AccessToken { get; set; }

        [JsonProperty("athlete")]
        public StravaAthlete Athlete { get; set; }
    }

    public class StravaAthlete {
        [JsonProperty("id")]
        public Int64 Id { get; set; }

        [JsonProperty("username")]
        public String Username { get; set; }

        [JsonProperty("resource_state")]
        public Int32 ResourceState { get; set; }

        [JsonProperty("firstname")]
        public String FirstName { get; set; }

        [JsonProperty("lastname")]
        public String LastName { get; set; }

        [JsonProperty("city")]
        public String City { get; set; }

        [JsonProperty("state")]
        public String State { get; set; }

        [JsonProperty("sex")]
        public String Sex { get; set; }

        [JsonProperty("premium")]
        public Boolean Premium { get; set; }

        [JsonProperty("summit")]
        public Boolean Summit { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("badge_type_id")]
        public Int32 BadgeTypeId { get; set; }

        [JsonProperty("profile_medium")]
        public String ProfileMedium { get; set; }

        [JsonProperty("profile")]
        public String Profile { get; set; }
    }
}
