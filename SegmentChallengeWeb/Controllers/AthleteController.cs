using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SegmentChallengeWeb.Configuration;
using SegmentChallengeWeb.Models;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb.Controllers {
    [ApiController]
    [Route("api/athletes")]
    public class AthleteController : ControllerBase {
        private readonly IOptions<SegmentChallengeConfiguration> siteConfiguration;
        private readonly IOptions<SegmentChallengeConfiguration> challengeConfiguration;
        private readonly Func<DbConnection> dbConnectionFactory;

        public AthleteController(
            IOptions<SegmentChallengeConfiguration> siteConfiguration,
            IOptions<SegmentChallengeConfiguration> challengeConfiguration,
            Func<DbConnection> dbConnectionFactory) {

            this.siteConfiguration = siteConfiguration;
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

            if (athlete == null) {
                // Odd
                return NotFound();
            } else {
                return new JsonResult(new AthleteProfile {
                    Username = athlete.Username,
                    FirstName = athlete.FirstName,
                    LastName = athlete.LastName,
                    BirthDate = athlete.BirthDate,
                    Gender = athlete.Gender,
                    Email = athlete.Email,
                    IsAdmin = this.siteConfiguration.Value.Administrators != null &&
                        this.siteConfiguration.Value.Administrators.Contains(identity.UserId)
                });
            }
        }

        [HttpPost("self")]
        public async Task<IActionResult> UpdateSelf(
            [FromBody] AthleteProfile profile,
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

            if (athlete == null) {
                // Odd
                return NotFound();
            } else {
                var emailInUse =
                    await athleteTable.Where(a => a.Email == profile.Email).Select(_ => true).SingleOrDefaultAsync(cancellationToken);
                if (emailInUse) {
                    return BadRequest(new ProblemDetails {
                        Detail = "The email address you entered is already in use by another user.",
                        Type = ErrorTypes.EmailAddressInUse
                    });
                }

                athlete.BirthDate = profile.BirthDate;
                athlete.Gender = profile.Gender;
                athlete.Email = profile.Email;

                await dbContext.SaveChangesAsync(cancellationToken);

                return ReturnAthleteProfileWithCookie(athlete);
            }
        }

        private IActionResult ReturnAthleteProfileWithCookie(Athlete athlete) {
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
                Gender = athlete.Gender,
                Email = athlete.Email
            });
        }
    }

    public class AthleteProfile {
        public String Username { get; set; }
        public String FirstName { get; set; }
        public String LastName { get; set; }
        public Char? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public String Email { get; set; }
        public Boolean IsAdmin { get; set; }
    }

    public class AthleteSignUp {
        public String Email { get; set; }
        public String Password { get; set; }
        public String Username { get; set; }
        public String FirstName { get; set; }
        public String LastName { get; set; }
        public Char Gender { get; set; }
        public DateTime BirthDate { get; set; }
    }
}
