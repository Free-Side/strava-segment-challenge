using System;
using System.Data.Common;
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
    [Route("api/auth")]
    public class LoginController : ControllerBase {
        private readonly IOptions<SegmentChallengeConfiguration> challengeConfiguration;
        private readonly Func<DbConnection> dbConnectionFactory;

        public LoginController(
            IOptions<SegmentChallengeConfiguration> challengeConfiguration,
            Func<DbConnection> dbConnectionFactory) {

            this.challengeConfiguration = challengeConfiguration;
            this.dbConnectionFactory = dbConnectionFactory;
        }

        [HttpPost("login")]
        public async Task<IActionResult> LogIn(AthleteLogin login, CancellationToken cancellationToken) {
            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var athleteTable = dbContext.Set<Athlete>();

            var athlete = await athleteTable.SingleOrDefaultAsync(a => a.Email == login.Email, cancellationToken: cancellationToken);

            if (athlete == null ||
                String.IsNullOrEmpty(athlete.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(login.Password, athlete.PasswordHash)) {
                // Make em sweat
                await Task.Delay(2000, cancellationToken);
                return Unauthorized();
            } else {
                return ReturnAthleteProfileWithCookie(athlete);
            }
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp(
            AthleteSignUp signUp,
            CancellationToken cancellationToken) {

            if (this.User is JwtCookiePrincipal) {
                return BadRequest(new ProblemDetails {
                    Detail = "User is already signed in."
                });
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var athleteTable = dbContext.Set<Athlete>();

            var existingAthlete = await athleteTable.SingleOrDefaultAsync(a => a.Email == signUp.Email, cancellationToken: cancellationToken);

            if (existingAthlete != null) {
                return BadRequest(new ProblemDetails {
                    Detail = "The email address you entered is already in use by another user.",
                    Type = ErrorTypes.EmailAddressInUse
                });
            }

            var minId = await athleteTable.MinAsync(a => a.Id, cancellationToken: cancellationToken);

            var athlete = await athleteTable.AddAsync(
                new Athlete {
                    Id = Math.Min(0, minId) - 1,
                    Email = signUp.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(signUp.Password),
                    Username = signUp.Username,
                    FirstName = signUp.FirstName,
                    LastName = signUp.LastName,
                    BirthDate = signUp.BirthDate,
                    Gender = signUp.Gender
                },
                cancellationToken
            );

            await dbContext.SaveChangesAsync(cancellationToken);

            return ReturnAthleteProfileWithCookie(athlete.Entity);
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

    public class AthleteLogin {
        public String Email { get; set; }
        public String Password { get; set; }
    }
}
