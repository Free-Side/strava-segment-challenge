using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SegmentChallengeWeb.Configuration;
using SegmentChallengeWeb.Models;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Controllers {
    [ApiController]
    [Route("api/athletes")]
    public class AthleteController : ControllerBase {
        private readonly IOptions<SegmentChallengeConfiguration> challengeConfiguration;
        private readonly Func<DbConnection> dbConnectionFactory;

        public AthleteController(
            IOptions<SegmentChallengeConfiguration> challengeConfiguration,
            Func<DbConnection> dbConnectionFactory) {

            this.challengeConfiguration = challengeConfiguration;
            this.dbConnectionFactory = dbConnectionFactory;
        }

        // GET
        [HttpGet("self")]
        public async Task<IActionResult> GetSelf(CancellationToken cancellationToken) {
            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var athleteTable = dbContext.Set<Athlete>();

            var athlete =
                await athleteTable.FindAsync(new Object[] { identity.UserId }, cancellationToken);

            if (athlete == null ) {
                // Odd
                return NotFound();
            } else {
                return new JsonResult(new AthleteProfile {
                    Username = athlete.Username,
                    FirstName = athlete.FirstName,
                    LastName = athlete.LastName,
                    BirthDate = athlete.BirthDate,
                    Gender = athlete.Gender
                });
            }
        }

        [HttpPost("self")]
        public async Task<IActionResult> UpdateSelf(
            [FromBody]AthleteProfile profile,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var athleteTable = dbContext.Set<Athlete>();

            var athlete =
                await athleteTable.FindAsync(new Object[] { identity.UserId }, cancellationToken);

            if (athlete == null ) {
                // Odd
                return NotFound();
            } else {
                athlete.BirthDate = profile.BirthDate;
                athlete.Gender = profile.Gender;

                await dbContext.SaveChangesAsync(cancellationToken);

                Response.Cookies.Append(
                    "id_token",
                    StravaConnectController.CreateAthleteJwt(
                        this.challengeConfiguration.Value,
                        athlete
                    )
                );

                return new JsonResult(new AthleteProfile {
                    Username = athlete.Username,
                    FirstName = athlete.FirstName,
                    LastName = athlete.LastName,
                    BirthDate = athlete.BirthDate,
                    Gender = athlete.Gender
                });
            }
        }
    }

    public class AthleteProfile {
        public String Username { get; set; }
        public String FirstName { get; set; }
        public String LastName { get; set; }
        public Char? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
    }
}
