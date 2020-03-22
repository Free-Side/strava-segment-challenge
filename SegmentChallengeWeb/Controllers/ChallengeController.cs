using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
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
    [Route("api/challenges")]
    public class ChallengeController : ControllerBase {
        private readonly IOptions<SegmentChallengeConfiguration> siteConfiguration;
        private readonly Func<DbConnection> dbConnectionFactory;
        private readonly BackgroundTaskService taskService;

        public ChallengeController(
            IOptions<SegmentChallengeConfiguration> siteConfiguration,
            Func<DbConnection> dbConnectionFactory,
            BackgroundTaskService taskService) {

            this.siteConfiguration = siteConfiguration;
            this.dbConnectionFactory = dbConnectionFactory;
            this.taskService = taskService;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken cancellationToken) {
            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();

            return new JsonResult(
                await
                    challengeTable
                        .OrderByDescending(c => c.StartDate)
                        .ToListAsync(cancellationToken: cancellationToken)
            );
        }

        [HttpGet("{name}/age_groups")]
        public async Task<IActionResult> GetAgeGroups(
            String name,
            CancellationToken cancellationToken) {

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();
            var ageGroupsTable = dbContext.Set<AgeGroup>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            return new JsonResult(
                await ageGroupsTable
                    .Where(ag => ag.ChallengeId == challenge.Id).ToListAsync(cancellationToken)
            );
        }

        [HttpGet("{name}/registration")]
        public async Task<IActionResult> GetRegistrationStatus(
            String name,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();
            var registrationTable = dbContext.Set<ChallengeRegistration>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            var registration = await registrationTable.SingleOrDefaultAsync(
                r => r.ChallengeId == challenge.Id && r.AthleteId == identity.UserId,
                cancellationToken
            );

            return new JsonResult(new {
                registered = registration != null
            });
        }

        [HttpPost("{name}/register")]
        public async Task<IActionResult> Register(
            String name,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();
            var registrationTable = dbContext.Set<ChallengeRegistration>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            var registration = await registrationTable.SingleOrDefaultAsync(
                r => r.ChallengeId == challenge.Id && r.AthleteId == identity.UserId,
                cancellationToken
            );

            if (registration == null) {
                await registrationTable.AddAsync(
                    new ChallengeRegistration {
                        ChallengeId = challenge.Id,
                        AthleteId = identity.UserId
                    },
                    cancellationToken
                );

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return new JsonResult(new {
                registered = true
            });
        }

        [HttpGet("{name}/efforts")]
        public async Task<IActionResult> GetEfforts(
            String name,
            CancellationToken cancellationToken) {

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();
            var registrationTable = dbContext.Set<ChallengeRegistration>();
            var ageGroupsTable = dbContext.Set<AgeGroup>();
            var effortsTable = dbContext.Set<Effort>();
            var athleteTable = dbContext.Set<Athlete>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            var ageGroups = await
                ageGroupsTable
                    .Where(ag => ag.ChallengeId == challenge.Id)
                    .OrderBy(ag => ag.MaximumAge)
                    .ToListAsync(cancellationToken);

            var efforts = await
                effortsTable
                    .Where(e =>
                        e.SegmentId == challenge.SegmentId &&
                        e.StartDate >= challenge.StartDate &&
                        e.StartDate <= challenge.EndDate)
                    .Join(
                        athleteTable,
                        e => e.AthleteId,
                        a => a.Id,
                        (e, a) => new {
                            Effort = e,
                            Athlete = a
                        })
                    .Join(
                        registrationTable,
                        row => row.Athlete.Id,
                        r => r.AthleteId,
                        (row, reg) =>
                            new {
                                Effort = row.Effort,
                                Athlete = row.Athlete,
                                Registration = reg
                            })
                    .Where(row => row.Registration.ChallengeId == challenge.Id)
                    .OrderBy(row => row.Athlete.Id)
                    .ToListAsync(cancellationToken: cancellationToken);

            var results = new List<(Effort Effort, Athlete Athlete)>();

            Athlete currentAthlete = null;
            Effort bestEffort = null;

            foreach (var effort in efforts.Append(null)) {
                if (effort == null || effort.Athlete.Id != currentAthlete?.Id) {
                    if (bestEffort != null) {
                        results.Add((bestEffort, currentAthlete));
                    }

                    if (effort != null) {
                        currentAthlete = effort.Athlete;
                        bestEffort = effort.Effort;
                    }
                } else if (bestEffort == null ||
                    effort.Effort.ElapsedTime < bestEffort.ElapsedTime) {
                    bestEffort = effort.Effort;
                }
            }

            (String Gender, Int32 MaxAge) GetCategory(Athlete athlete) {
                var birthDateYear =
                    (athlete.BirthDate?.Year).GetValueOrDefault(DateTime.UtcNow.Year - 90);
                var age = DateTime.UtcNow.Year - birthDateYear;

                var ageGroup = ageGroups.SkipWhile(ag => age > ag.MaximumAge).First();

                return (athlete.Gender.GetValueOrDefault('M').ToString(), ageGroup.MaximumAge);
            }

            var resultsByCategory = new List<(Effort Effort, Athlete Athlete, Boolean IsKOM)>();

            (String Gender, Int32 MaxAge) currentCategory = (null, 0);

            foreach (var (effort, athlete) in results.OrderBy(e => GetCategory(e.Athlete)).ThenBy(e => e.Effort.ElapsedTime)) {
                var category = GetCategory(athlete);
                if (category != currentCategory) {
                    resultsByCategory.Add((effort, athlete, true));
                    currentCategory = category;
                } else if (resultsByCategory.Count > 0 &&
                           resultsByCategory[^1].Effort.ElapsedTime == effort.ElapsedTime) {
                    resultsByCategory.Add((effort, athlete, true));
                } else {
                    resultsByCategory.Add((effort, athlete, false));
                }
            }

            return new JsonResult(
                resultsByCategory
                    .OrderBy(e => e.Effort.ElapsedTime)
                    .ThenBy(e => e.Effort.StartDate)
                    .Select(e =>
                        new {
                            id = e.Effort.Id,
                            athleteId = e.Athlete.Id,
                            athleteName = e.Athlete.GetDisplayName(),
                            athleteGender = e.Athlete.Gender,
                            athleteAge = e.Athlete.BirthDate?.ToRacingAge(challenge.StartDate),
                            activityId = e.Effort.ActivityId,
                            elapsedTime = e.Effort.ElapsedTime,
                            startDate = e.Effort.StartDate,
                            isKOM = e.IsKOM
                        })
            );
        }

        [HttpGet("{name}/athletes")]
        public async Task<IActionResult> AllAthletes(String name, CancellationToken cancellationToken) {
            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();
            var athleteTable = dbContext.Set<Athlete>();
            var registrationTable = dbContext.Set<ChallengeRegistration>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            var athletes = await
                athleteTable
                    .Join(
                        registrationTable,
                        a => a.Id,
                        r => r.AthleteId,
                        (a, r) => new { Athlete = a, Registration = r })
                    .Where(row => row.Registration.ChallengeId == challenge.Id)
                    .Select(row => row.Athlete)
                    .ToListAsync(cancellationToken);

            return new JsonResult(
                athletes
                    .Where(a => a.Gender.HasValue && a.BirthDate.HasValue)
                    .Select(a => new {
                        id = a.Id,
                        displayName = a.GetDisplayName(),
                        gender = a.Gender.ToString(),
                        age = challenge.StartDate.Year - a.BirthDate.Value.Year
                    })
            );
        }

        [HttpPost("{name}/refresh_all")]
        public async Task<IActionResult> RefreshAllEfforts(
            String name,
            CancellationToken cancellationToken) {
            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            } else if (this.siteConfiguration.Value.Administrators == null ||
                !this.siteConfiguration.Value.Administrators.Contains(identity.UserId)) {
                return Forbid();
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);
            var challengeTable = dbContext.Set<Challenge>();
            var updatesTable = dbContext.Set<Update>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            var pendingUpdates = await
                updatesTable
                    .Where(u => u.EndTime == null && u.AthleteId == null)
                    .CountAsync(cancellationToken);

            if (pendingUpdates > 0) {
                Response.StatusCode = (Int32)HttpStatusCode.ServiceUnavailable;
                return Content("Another update is already running.");
            }

            var update = updatesTable.Add(new Update { ChallengeId = challenge.Id});
            await dbContext.SaveChangesAsync(cancellationToken);

            var updateId = update.Entity.Id;

            this.taskService.QueueTask<EffortRefresher>(
                (service, taskCancellationToken) => service.RefreshEfforts(updateId, name, taskCancellationToken)
            );

            return new JsonResult(new { updateId });
        }

        [HttpPost("{name}/refresh")]
        public async Task<IActionResult> RefreshEfforts(
            String name,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);
            var challengeTable = dbContext.Set<Challenge>();
            var updatesTable = dbContext.Set<Update>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            var update = updatesTable.Add(new Update {
                AthleteId = identity.UserId,
                ChallengeId = challenge.Id
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            var updateId = update.Entity.Id;

            this.taskService.QueueTask<EffortRefresher>(
                (service, taskCancellationToken) =>
                    service.RefreshAthleteEfforts(updateId, name, identity.UserId, taskCancellationToken)
            );

            return new JsonResult(new { updateId });
        }
    }
}
