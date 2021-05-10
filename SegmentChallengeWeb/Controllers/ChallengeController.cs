using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SegmentChallengeWeb.Configuration;
using SegmentChallengeWeb.Gpx;
using SegmentChallengeWeb.Models;
using SegmentChallengeWeb.Persistence;
using SegmentChallengeWeb.Utils;

namespace SegmentChallengeWeb.Controllers {
    [ApiController]
    [Route("api/challenges")]
    public class ChallengeController : ControllerBase {
        private static readonly Random rand = new Random();

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
                        .OrderBy(c => c.StartDate)
                        .ToListAsync(cancellationToken: cancellationToken)
            );
        }

        [HttpGet("{name}/route_map")]
        public async Task<IActionResult> GetRouteMap(
            String name,
            CancellationToken cancellationToken) {

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null || !challenge.HasRouteMap) {
                return NotFound();
            }

            // TODO check and set ETAG

            return new FileContentResult(
                challenge.RouteMapImage,
                "image/png"
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

        [HttpGet("{name}/special_categories")]
        public async Task<IActionResult> GetSpecialCategories(
            String name,
            CancellationToken cancellationToken) {

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();
            var specialCategoryTable = dbContext.Set<SpecialCategory>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            return new JsonResult(
                await specialCategoryTable
                    .Where(cat => cat.ChallengeId == challenge.Id)
                    .ToListAsync(cancellationToken)
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
            [FromQuery] String inviteCode,
            [FromQuery] Int32? specialCategory,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);

            var challengeTable = dbContext.Set<Challenge>();
            var registrationTable = dbContext.Set<ChallengeRegistration>();
            var specialCategoryTable = dbContext.Set<SpecialCategory>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            if (!String.IsNullOrEmpty(challenge.InviteCode)) {
                if (!String.Equals(challenge.InviteCode, inviteCode?.Trim(), StringComparison.OrdinalIgnoreCase)) {
                    return BadRequest(new ProblemDetails {
                        Detail = "This challenge requires an invite code in order to join. Follow the registration link to receive an invite code.",
                        Type = "urn:segment-challenge-app:invalid-invite-code"
                    });
                }
            }

            var registration = await registrationTable.SingleOrDefaultAsync(
                r => r.ChallengeId == challenge.Id && r.AthleteId == identity.UserId,
                cancellationToken
            );

            if (registration == null) {
                if (specialCategory.HasValue) {
                    var category =
                        await specialCategoryTable.SingleOrDefaultAsync(
                            sc => sc.ChallengeId == challenge.Id && sc.SpecialCategoryId == specialCategory,
                            cancellationToken
                        );

                    if (category == null) {
                        return BadRequest(new ProblemDetails {
                            Detail = "The specified special category was not found for this challenge.",
                            Type = "urn:segment-challenge-app:special-category-not-found"
                        });
                    }
                }

                await registrationTable.AddAsync(
                    new ChallengeRegistration {
                        ChallengeId = challenge.Id,
                        AthleteId = identity.UserId,
                        SpecialCategoryId = specialCategory
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
            var specialCategoryTable = dbContext.Set<SpecialCategory>();
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

            var results = new List<(Effort Effort, Athlete Athlete, Int32 LapCount, ChallengeRegistration registration)>();

            if (challenge.Type == ChallengeType.MostLaps) {
                foreach (var effortGroup in efforts.GroupBy(e => e.Athlete.Id)) {
                    var (effort, athlete, lapCount, registration) =
                        effortGroup.Aggregate(
                            (effort: (Effort)null, athlete: (Athlete)null, lapCount: 0, registration: (ChallengeRegistration)null),
                            (total, nextEffort) => {
                                if (total.effort == null) {
                                    return (nextEffort.Effort, nextEffort.Athlete, 1, nextEffort.Registration);
                                } else {
                                    return (total.effort.WithElapsedTime(total.effort.ElapsedTime + nextEffort.Effort.ElapsedTime),
                                        total.athlete,
                                        total.lapCount + 1,
                                        total.registration);
                                }
                            });
                    results.Add((effort, effortGroup.First().Athlete, lapCount, registration));
                }
            } else {
                Athlete currentAthlete = null;
                ChallengeRegistration currentRegistration = null;
                Effort bestEffort = null;

                foreach (var effort in efforts.Append(null)) {
                    if (effort == null || effort.Athlete.Id != currentAthlete?.Id) {
                        if (bestEffort != null) {
                            results.Add((bestEffort, currentAthlete, 1, currentRegistration));
                        }

                        if (effort != null) {
                            currentAthlete = effort.Athlete;
                            currentRegistration = effort.Registration;
                            bestEffort = effort.Effort;
                        }
                    } else if (bestEffort == null ||
                        effort.Effort.ElapsedTime < bestEffort.ElapsedTime) {
                        bestEffort = effort.Effort;
                    }
                }
            }

            var specialCategoryLookup =
                await specialCategoryTable.Where(sc => sc.ChallengeId == challenge.Id)
                    .ToDictionaryAsync(sc => sc.SpecialCategoryId, cancellationToken);

            String GetCategory(Athlete athlete, ChallengeRegistration registration) {
                if (registration.SpecialCategoryId.HasValue &&
                    specialCategoryLookup.TryGetValue(registration.SpecialCategoryId.Value, out var cat)) {
                    return $"{athlete.Gender} - {cat.CategoryName}";
                } else {
                    var birthDateYear =
                        (athlete.BirthDate?.Year).GetValueOrDefault(DateTime.UtcNow.Year - 90);
                    var age = DateTime.UtcNow.Year - birthDateYear;

                    var ageGroup = ageGroups.SkipWhile(ag => age > ag.MaximumAge).First();

                    return $"{athlete.Gender} - {ageGroup.MaximumAge}";
                }
            }

            var resultsByCategory = new List<(Effort Effort, Athlete Athlete, Int32 LapCount, Boolean IsKOM, Int32? SpecialCategoryId)>();

            String currentCategory = null;

            var orderedResults =
                results.OrderBy(e => GetCategory(e.Athlete, e.registration))
                    .ThenByDescending(e => e.LapCount)
                    .ThenBy(e => e.Effort.ElapsedTime);
            foreach (var (effort, athlete, lapCount, reg) in orderedResults) {
                var category = GetCategory(athlete, reg);
                if (category != currentCategory) {
                    resultsByCategory.Add((effort, athlete, lapCount, true, reg.SpecialCategoryId));
                    currentCategory = category;
                } else if (resultsByCategory.Count > 0 &&
                    resultsByCategory[^1].LapCount == lapCount &&
                    resultsByCategory[^1].Effort.ElapsedTime == effort.ElapsedTime) {
                    // Tie for first
                    resultsByCategory.Add((effort, athlete, lapCount, true, reg.SpecialCategoryId));
                } else {
                    resultsByCategory.Add((effort, athlete, lapCount, false, reg.SpecialCategoryId));
                }
            }

            return new JsonResult(
                resultsByCategory
                    .OrderByDescending(e => e.LapCount)
                    .ThenBy(e => e.Effort.ElapsedTime)
                    .ThenBy(e => e.Effort.StartDate)
                    .Select(e =>
                        new {
                            id = e.Effort.Id,
                            athleteId = e.Athlete.Id,
                            athleteName = e.Athlete.GetDisplayName(),
                            athleteGender = e.Athlete.Gender,
                            athleteAge = e.Athlete.BirthDate?.ToRacingAge(challenge.StartDate),
                            activityId = e.Effort.ActivityId,
                            lapCount = e.LapCount,
                            elapsedTime = e.Effort.ElapsedTime,
                            startDate = e.Effort.StartDate,
                            isKOM = e.IsKOM,
                            specialCategoryId = e.SpecialCategoryId
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
                    .ToListAsync(cancellationToken);

            return new JsonResult(
                athletes
                    .Where(a => a.Athlete.Gender.HasValue && a.Athlete.BirthDate.HasValue)
                    .Select(a => new {
                        id = a.Athlete.Id,
                        displayName = a.Athlete.GetDisplayName(),
                        gender = a.Athlete.Gender.ToString(),
                        age = challenge.StartDate.Year - a.Athlete.BirthDate.Value.Year,
                        specialCategoryId = a.Registration.SpecialCategoryId
                    })
            );
        }

        [HttpPost("{name}/refresh_all")]
        public async Task<IActionResult> RefreshAllEfforts(
            String name,
            CancellationToken cancellationToken) {
            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            } else if (!IsAdmin(identity)) {
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

            var update = updatesTable.Add(new Update { ChallengeId = challenge.Id });
            await dbContext.SaveChangesAsync(cancellationToken);

            var updateId = update.Entity.Id;

            this.taskService.QueueTask<EffortRefresher>(
                (service, taskCancellationToken) => service.RefreshEfforts(updateId, name, taskCancellationToken)
            );

            return new JsonResult(new { updateId });
        }

        [HttpPost("toggle_auto_refresh")]
        public IActionResult ToggleAutoRefresh(
            [FromQuery] Boolean? enabled,
            CancellationToken cancellationToken) {
            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            } else if (!IsAdmin(identity)) {
                return Forbid();
            }

            Boolean result;
            if (enabled.HasValue) {
                result = AutoRefreshService.RefreshEnabled = enabled.Value;
            } else {
                result = AutoRefreshService.RefreshEnabled = !AutoRefreshService.RefreshEnabled;
            }

            return Ok(new {
                Status = result,
                Message = $"Segment Auto-Refresh {(result ? "enabled" : "disabled")}"
            });
        }

        [HttpPost("{name}/refresh")]
        public async Task<IActionResult> RefreshEfforts(
            String name,
            [FromQuery] Int64? athlete,
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

            if (challenge.StartDate.AddDays(-1) > DateTime.UtcNow) {
                return Ok(new {
                    updateId = -1,
                    message = "This challenge has not started yet."
                });
            }

            var athleteId = athlete ?? identity.UserId;

            var update = await updatesTable.AddAsync(
                new Update {
                    AthleteId = athleteId,
                    ChallengeId = challenge.Id
                },
                cancellationToken
            );

            await dbContext.SaveChangesAsync(cancellationToken);

            var updateId = update.Entity.Id;

            this.taskService.QueueTask<EffortRefresher>(
                (service, taskCancellationToken) =>
                    service.RefreshAthleteEfforts(updateId, name, athleteId, taskCancellationToken)
            );

            return new JsonResult(new { updateId });
        }

        // Matching points must be within 20 meters
        private const Double DefaultTolerance = 20.0;

        // Allow a rider to skip a maximum of 10% of the route if they go off course.
        private const Double DefaultMaxSkip = 0.1;

        private const Int32 MaximumInteractions = 1000000;

        private static readonly XNamespace svgns = "http://www.w3.org/2000/svg";

        [HttpPost("{name}/upload_activity")]
        public async Task<IActionResult> UploadActivity(
            String name,
            [FromQuery] Int64? athlete,
            [FromQuery] Boolean debug,
            [FromQuery] Boolean image,
            [FromQuery(Name = "tolerance")] Double? toleranceOverride,
            [FromQuery(Name = "max-skip")] Double? maxSkipOverride,
            [FromQuery(Name = "moving-time")] Boolean forceUseMovingTime,
            [FromForm] IFormFile gpxFile,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            if (gpxFile == null) {
                return BadRequest("A file must be provided");
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);
            var challengeTable = dbContext.Set<Challenge>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            if (athlete.HasValue && athlete.Value != identity.UserId && !IsAdmin(identity)) {
                // Non-admins cannot upload results for other users.
                return Forbid();
            }

            if (String.IsNullOrEmpty(challenge.GpxData)) {
                return BadRequest("This challenge does not have segment GPX data available.");
            }

            var tolerance = toleranceOverride ?? DefaultTolerance;
            var maximumSkip = maxSkipOverride ?? DefaultMaxSkip;

            var athleteId = athlete ?? identity.UserId;

            var gpxSerializer = new XmlSerializer(typeof(GpxData));
            var route = (GpxData)gpxSerializer.Deserialize(new StringReader(challenge.GpxData));

            await using var gpxFileStream = gpxFile.OpenReadStream();
            var ride = (GpxData)gpxSerializer.Deserialize(gpxFileStream);

            var currentSegmentIx = 0;
            var currentPointIx = 0;

            TrackPoint start = null;
            TrackPoint end = null;
            var gaps = TimeSpan.Zero;
            var skippedPoints = 0;
            var match = false;

            var startDistanceList = new List<Int32>();
            var skippedPointList = new List<TrackPoint>();
            var matchDistanceList = new List<Int32>();
            var matchIxList = new List<Int32>();
            var gapList = new List<Double>();

            var routePath = new StringBuilder();
            var skippedPath = new StringBuilder();
            var ridePath = new StringBuilder();
            var matchPath = new StringBuilder();

            var skipping = false;
            var matchCount = 1;

            var iterations = 0;

            var routePoints = route.Track.Segments.SelectMany(seg => seg.Points).ToArray();
            var startPoint = routePoints[0];

            String renderPoint(TrackPoint point) {
                return $"{(point.Longitude - startPoint.Longitude) * 1000:f3},{(point.Latitude - startPoint.Latitude) * -1000:f3}";
            }

            Console.Error.WriteLine($"Total route points: {routePoints.Length}");
            for (var routePointIx = 0; routePointIx < routePoints.Length && iterations < MaximumInteractions; routePointIx++) {
                iterations++;
                var routePoint = routePoints[routePointIx];

                if (skipping && (skippedPoints > routePoints.Length * maximumSkip || routePointIx == routePoints.Length - 1)) {
                    // We've skipped 10% of the course, or we're in danger of "skipping" off the end of the course

                    // The current point is obviously no good, try advancing forward 1 point
                    if (currentPointIx + 1 < ride.Track.Segments[currentSegmentIx].Points.Count) {
                        currentPointIx++;
                    } else if (currentSegmentIx + 1 < ride.Track.Segments.Count) {
                        currentSegmentIx++;
                        currentPointIx = 0;
                    } else {
                        break;
                    }

                    Console.Error.WriteLine($"Resetting to start point search at position {currentSegmentIx}:{currentPointIx}");
                    // try looking for the start point again (maybe the rider double-crossed the start)
                    start = null;
                    // skippedPath.Clear();
                    matchPath.Clear();
                    routePath.Append($" M {renderPoint(startPoint)} L");
                    routePointIx = 0;
                    routePoint = routePoints[routePointIx];
                    skippedPoints = 0;
                    skippedPointList.Clear();
                    startDistanceList.Clear();
                    matchDistanceList.Clear();
                    matchIxList.Clear();
                    gapList.Clear();
                    gaps = TimeSpan.Zero;
                    matchCount = 1;
                }

                if (start == null) {
                    // Scan forward until we find a point within 20 meters of the start point
                    while (currentSegmentIx < ride.Track.Segments.Count &&
                        currentPointIx < ride.Track.Segments[currentSegmentIx].Points.Count &&
                        Distance(routePoint, ride.Track.Segments[currentSegmentIx].Points[currentPointIx]) > tolerance) {

                        var currentPoint = ride.Track.Segments[currentSegmentIx].Points[currentPointIx];
                        ridePath.Append(ridePath.Length == 0 ? $"M {renderPoint(currentPoint)} L" : $" {renderPoint(currentPoint)}");
                        startDistanceList.Add((Int32)Distance(routePoint, currentPoint));
                        if (currentPointIx + 1 < ride.Track.Segments[currentSegmentIx].Points.Count) {
                            currentPointIx++;
                        } else {
                            currentSegmentIx++;
                            currentPointIx = 0;
                        }
                    }

                    if (currentSegmentIx < ride.Track.Segments.Count && currentPointIx < ride.Track.Segments[currentSegmentIx].Points.Count) {
                        match = true;
                        start = ride.Track.Segments[currentSegmentIx].Points[currentPointIx];

                        ridePath.Append(ridePath.Length == 0 ? $"M {renderPoint(start)} L" : $" {renderPoint(start)}");
                        routePath.Append($"M {renderPoint(routePoint)} L");
                        matchPath.Append($"M {renderPoint(routePoint)} L {renderPoint(start)}");

                        Console.Error.WriteLine($"Rider reached the start point at {currentSegmentIx}:{currentPointIx}");
                    } else {
                        // Unable to find start, give up
                        Console.Error.WriteLine($"FAIL: checked {startDistanceList.Count} points with no start.");
                        break;
                    }
                } else {
                    // Scan forward until we find the next point, keeping track of gaps
                    Int32 nextSegmentIx;
                    Int32 nextPointIx;
                    if (Distance(routePoint, ride.Track.Segments[currentSegmentIx].Points[currentPointIx]) < tolerance) {
                        // just accept the current point as valid for this route point
                        nextSegmentIx = currentSegmentIx;
                        nextPointIx = currentPointIx;
                    } else {
                        if (currentPointIx + 1 < ride.Track.Segments[currentSegmentIx].Points.Count) {
                            nextPointIx = currentPointIx + 1;
                            nextSegmentIx = currentSegmentIx;
                        } else if (currentSegmentIx + 1 < ride.Track.Segments.Count) {
                            nextSegmentIx = currentSegmentIx + 1;
                            // We assume that each segment has at least one point
                            nextPointIx = 0;
                        } else {
                            // End of the ride
                            nextSegmentIx = -1;
                            nextPointIx = -1;
                            Console.Error.WriteLine($"{routePointIx} - Reached the end of the ride!");
                        }
                    }

                    var gap = TimeSpan.Zero;
                    TrackPoint nextPoint = null;
                    if (nextPointIx >= 0) {
                        nextPoint = ride.Track.Segments[nextSegmentIx].Points[nextPointIx];

                        while (Distance(routePoint, nextPoint) > tolerance && Distance(routePoint, nextPoint) < tolerance * 100 &&
                            iterations < MaximumInteractions) {
                            // if (!skipping) {
                            //     Console.Error.WriteLine(
                            //         $"Ride point {nextPointIx} is {Distance(routePoint, nextPoint)} m from route point {routePointIx} which is > tolerance {tolerance}"
                            //     );
                            // }

                            iterations++;
                            if (skipping && Distance(startPoint, nextPoint) <= tolerance) {
                                // The track went off course and returned to the start!
                                Console.Error.WriteLine($"{routePointIx} - Rider returned to the start at position {nextSegmentIx}:{nextPointIx}");
                                skippedPath.Clear();
                                // ridePath.Clear();
                                matchPath.Clear();
                                start = nextPoint;
                                routePath.Append($" M {renderPoint(startPoint)} L");
                                matchPath.Append($"M {renderPoint(routePoint)} L {renderPoint(start)}");
                                ridePath.Append($" {renderPoint(nextPoint)}");
                                routePointIx = 0;
                                routePoint = startPoint;
                                skipping = false;
                                // Don't dock the user for points skipped before now;
                                skippedPoints = 0;
                                skippedPointList.Clear();
                                matchDistanceList.Clear();
                                matchIxList.Clear();
                                gapList.Clear();
                                gaps = gap = TimeSpan.Zero;
                                matchCount = 1;

                                break;
                            }

                            if (nextPointIx + 1 < ride.Track.Segments[nextSegmentIx].Points.Count) {
                                nextPointIx++;
                            } else if (nextSegmentIx + 1 < ride.Track.Segments.Count) {
                                nextSegmentIx++;
                                nextPointIx = 0;
                            } else {
                                break;
                            }

                            var previousPoint = nextPoint;
                            nextPoint = ride.Track.Segments[nextSegmentIx].Points[nextPointIx];
                            ridePath.Append($" {renderPoint(nextPoint)}");
                            var interval = nextPoint.Time.Subtract(previousPoint.Time).TotalSeconds;
                            if (interval > 0) {
                                var speed = Distance(previousPoint, nextPoint) / interval;
                                // If travelling at less that 0.5 mph don't count this time interval
                                // if (speed < 0.22352)
                                // If travelling at less that 1 mph don't count this time interval
                                if (speed < 0.44704) {
                                    gap = gap.Add(TimeSpan.FromSeconds(interval));
                                }
                            }
                        }
                    }

                    if (nextPoint != null && Distance(routePoint, nextPoint) <= tolerance) {
                        match = true;
                        matchDistanceList.Add((Int32)Distance(routePoint, nextPoint));
                        matchIxList.Add(nextPointIx);
                        if (gap > TimeSpan.Zero) {
                            gapList.Add(gap.TotalSeconds);
                            Console.Error.WriteLine($"Adding a gap of {gap.TotalSeconds:f0} seconds");
                            gaps = gaps.Add(gap);
                        }

                        if (skipping) {
                            Console.Error.WriteLine($"{routePointIx} - Switched to matching mode at ride point {nextSegmentIx}:{nextPointIx}");
                            skippedPath.Append($" {renderPoint(routePoint)}");
                            routePath.Append($" M {renderPoint(routePoint)} L");
                            skipping = false;
                        } else {
                            routePath.Append($" {renderPoint(routePoint)}");
                        }

                        if (matchCount % 10 == 1) {
                            matchPath.Append($"M {renderPoint(routePoint)} L {renderPoint(nextPoint)}");
                        }

                        currentPointIx = nextPointIx;
                        currentSegmentIx = nextSegmentIx;
                        matchCount++;
                    } else {
                        // If distance goes over Tolerance * 10 meters, try skipping this track point.
                        match = false;
                        skippedPointList.Add(routePoint);
                        skippedPoints++;

                        if (skipping) {
                            // continue skip mode
                            skippedPath.Append($" {renderPoint(routePoint)}");
                            Console.Error.WriteLine($"{routePointIx} - Skipped");
                        } else {
                            // Switch to skip mode
                            Console.Error.WriteLine($"{routePointIx} - Switched to skip mode at ride point {currentSegmentIx}:{currentPointIx}");
                            routePath.Append($" {renderPoint(routePoint)}");
                            skippedPath.Append($" M {renderPoint(routePoint)} L");
                            skipping = true;
                        }
                    }
                }
            }

            if (iterations >= MaximumInteractions) {
                Console.Error.WriteLine("That looked like an infinite loop.");
            }

            if (image) {
                return new ContentResult {
                    Content =
                        new XDocument(
                            new XDeclaration("1.0", "utf-8", "yes"),
                            new XElement(
                                svgns + "svg",
                                new XElement(svgns + "path",
                                    new XAttribute("style", "stroke: purple; stroke-width: 0.1; fill: none"),
                                    new XAttribute("d", routePath.ToString())),
                                new XElement(svgns + "path",
                                    new XAttribute("style", "stroke: red; stroke-width: 0.1; fill: none"),
                                    new XAttribute("d", skippedPath.ToString())),
                                new XElement(svgns + "path",
                                    new XAttribute("style", "stroke: green; stroke-width: 0.1; fill: none"),
                                    new XAttribute("d", ridePath.ToString())),
                                new XElement(svgns + "path",
                                    new XAttribute("style", "stroke: blue; stroke-width: 0.1; fill: none"),
                                    new XAttribute("d", matchPath.ToString()))
                            )
                        ).ToString(),
                    ContentType = "image/svg+xml",
                    StatusCode = match ? 200 : 400
                };
            } else if (match && start != null) {
                end = ride.Track.Segments[currentSegmentIx].Points[currentPointIx];
                var elapsed = end.Time.Subtract(start.Time);
                var moving = elapsed.Subtract(gaps);

                var effortsTable = dbContext.Set<Effort>();
                // currently we only support uploads for single lap activities.
                var effort =
                    await effortsTable.SingleOrDefaultAsync(
                        e => e.SegmentId == challenge.SegmentId && e.AthleteId == athleteId,
                        cancellationToken);
                if (effort != null) {
                    // Update existing.
                    effort.StartDate = start.Time;
                    effort.ElapsedTime =
                        challenge.UseMovingTime || forceUseMovingTime ?
                            (Int32)moving.TotalSeconds :
                            (Int32)elapsed.TotalSeconds;
                } else {
                    Int64 id;
                    lock (rand) {
                        id = rand.Next(Int32.MinValue, -1) << 31 + rand.Next(Int32.MinValue, -1);
                    }

                    var newEffort = await effortsTable.AddAsync(
                        new Effort {
                            Id = id,
                            ActivityId = -1,
                            AthleteId = athleteId,
                            ElapsedTime =
                                challenge.UseMovingTime ?
                                    (Int32)moving.TotalSeconds :
                                    (Int32)elapsed.TotalSeconds,
                            SegmentId = challenge.SegmentId,
                            StartDate = start.Time
                        },
                        cancellationToken
                    );

                    effort = newEffort.Entity;
                }

                if (!debug && !image) {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return Ok(new {
                    Effort = effort,
                    Start = start,
                    End = end,
                    Elapsed = elapsed.ToString(),
                    Moving = moving.ToString(),
                    Stopped = gaps.ToString()
                });
            } else {
                return BadRequest("The uploaded ride does not match the segment data for this challenge.");
            }
        }

        private const Double R = 6.371e6; // earth's radius in meters

        private Double Distance(TrackPoint point1, TrackPoint point2) {
            var lat1 = (Double)point1.Latitude;
            var lat2 = (Double)point2.Latitude;
            var lon1 = (Double)point1.Longitude;
            var lon2 = (Double)point2.Longitude;

            var φ1 = lat1 * Math.PI / 180; // φ, λ in radians
            var φ2 = lat2 * Math.PI / 180;
            var Δφ = φ2 - φ1;
            var Δλ = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                Math.Cos(φ1) * Math.Cos(φ2) *
                Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // distance in meters
        }

        [HttpPost("{name}/set_gpx")]
        public async Task<IActionResult> SetGpxData(
            String name,
            [FromForm] IFormFile gpxFile,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            if (gpxFile == null) {
                return BadRequest("A file must be provided");
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);
            var challengeTable = dbContext.Set<Challenge>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            if (!IsAdmin(identity)) {
                return Forbid();
            }

            await using var fileStream = gpxFile.OpenReadStream();

            XDocument gpxData;
            try {
                gpxData = XDocument.Load(fileStream);
            } catch (Exception ex) {
                return BadRequest($"Unable to load GPX file: {ex.Message}");
            }

            challenge.GpxData = gpxData.ToString();

            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok();
        }

        [HttpPost("{name}/set_image")]
        public async Task<IActionResult> SetRouteImage(
            String name,
            [FromForm] IFormFile imageFile,
            CancellationToken cancellationToken) {

            if (!(User is JwtCookiePrincipal identity)) {
                return Unauthorized();
            }

            if (imageFile == null) {
                return BadRequest("A file must be provided");
            }

            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);
            var challengeTable = dbContext.Set<Challenge>();

            var challenge = await challengeTable.SingleOrDefaultAsync(
                c => c.Name == name,
                cancellationToken
            );

            if (challenge == null) {
                return NotFound();
            }

            if (!IsAdmin(identity)) {
                return Forbid();
            }

            await using var fileStream = imageFile.OpenReadStream();

            challenge.RouteMapImage = await fileStream.ReadAllBytesAsync(cancellationToken: cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok();
        }

        [HttpGet("invite_code")]
        public IActionResult GenerateInviteCode() {
            lock (rand) {
                var word1 = InviteCodeWordList[rand.Next(0, InviteCodeWordList.Length)];
                var subWordList = InviteCodeWordList.Where(w => !w.StartsWith(word1.Substring(0, 1), StringComparison.OrdinalIgnoreCase)).ToList();
                var word2 = subWordList[rand.Next(0, subWordList.Count)];
                return Ok(String.Join("", Capitalize(word1), Capitalize(word2), InviteCodeNumberList[rand.Next(0, InviteCodeNumberList.Length)].ToString()));
            }
        }

        private Boolean IsAdmin(JwtCookiePrincipal identity) {
            return this.siteConfiguration.Value.Administrators != null &&
                this.siteConfiguration.Value.Administrators.Contains(identity.UserId);
        }

        private static String Capitalize(String word) {
            return $"{word.Substring(0, 1).ToUpperInvariant()}{word.Substring(1)}";
        }

        private static readonly String[] InviteCodeWordList = new[] {
            "aero",
            "air",
            "all-rounder",
            "alleycat",
            "anchor",
            "ano",
            "apex",
            "attack",
            "auger",
            "autobus",
            "backie",
            "bacon",
            "bagger",
            "bail",
            "bars",
            "basecamp",
            "bead",
            "beat",
            "beater",
            "beta",
            "betty",
            "bidon",
            "biff",
            "bike",
            "biopace",
            "blast",
            "blocking",
            "bog",
            "boing",
            "bomb",
            "bonk",
            "boost",
            "booties",
            "boulder",
            "bracket",
            "brain",
            "brake",
            "brakes",
            "braze-ons",
            "break",
            "breakaway",
            "brick",
            "bridge",
            "broom",
            "bully",
            "bunch",
            "bunny",
            "burrito",
            "bust",
            "buzz",
            "cadence",
            "campy",
            "cantilever",
            "captain",
            "caravan",
            "carve",
            "cashed",
            "cassette",
            "category",
            "century",
            "ceramic",
            "chain",
            "chase",
            "chicane",
            "chunder",
            "chute",
            "classic",
            "clean",
            "cleat",
            "climber",
            "climber",
            "clincher",
            "clip",
            "cluster",
            "cog",
            "col",
            "commissaire",
            "components",
            "counter",
            "crack",
            "crank",
            "crater",
            "crayon",
            "creamed",
            "criterium",
            "dab",
            "derailleur",
            "descender",
            "DFL",
            "dialed",
            "diesel",
            "digger",
            "dirt",
            "dishing",
            "domestique",
            "doubletrack",
            "downhill",
            "draft",
            "drafting",
            "drillium",
            "drop",
            "drop-off",
            "dropout",
            "drops",
            "dual-track",
            "echelon",
            "echlon",
            "endo",
            "enduro",
            "extreme",
            "face-plant",
            "false-flat",
            "fast",
            "feed-zone",
            "field",
            "fishtail",
            "fixed",
            "fixie",
            "flail",
            "flash",
            "flat",
            "flex",
            "flick",
            "follow",
            "fork",
            "frame",
            "free-ride",
            "gap",
            "gear",
            "giblets",
            "gnarly",
            "gonzo",
            "granny",
            "grate",
            "grindies",
            "gripped",
            "group",
            "grunt",
            "gruppetto",
            "guttered",
            "half",
            "hammer",
            "hammered",
            "hammerhead",
            "handicap",
            "hanging",
            "hardcore",
            "hardtail",
            "head",
            "header",
            "headset",
            "hill",
            "honking",
            "hop",
            "hub",
            "hucker",
            "hybrid",
            "hydraulic",
            "hyperglide",
            "jump",
            "kack",
            "keirin",
            "kicker",
            "kit",
            "knock",
            "knurled",
            "KOM",
            "lead-out",
            "leech",
            "lid",
            "limit",
            "line",
            "lug",
            "madison",
            "manual",
            "Marin",
            "mash",
            "mechanic",
            "modulation",
            "moto",
            "marshal",
            "mountain",
            "MTB",
            "musette",
            "neo-pro",
            "NORBA",
            "off-camber",
            "omnium",
            "overgeared",
            "overlap",
            "paceline",
            "pack",
            "palmares",
            "panache",
            "panic",
            "pannier",
            "pass",
            "pedaling",
            "peloton",
            "pep",
            "phat",
            "pinch",
            "pitch",
            "pogo",
            "portage",
            "poser",
            "poursuivant",
            "power",
            "powerslide",
            "prang",
            "Presta",
            "prime",
            "prologue",
            "prune",
            "pull",
            "pump",
            "pumped",
            "race",
            "railing",
            "rake",
            "rally",
            "randonee",
            "rash",
            "relay",
            "rigid",
            "ring",
            "road",
            "roadie",
            "rock",
            "rollers",
            "saddle",
            "Schraeder",
            "schwag",
            "scratch",
            "scream",
            "screamer",
            "seat",
            "post",
            "stay",
            "sew-ups",
            "shapes",
            "shelled",
            "shifter",
            "singles",
            "singletrack",
            "sitting-on",
            "sketching",
            "slicks",
            "soigneur",
            "specialist",
            "spider",
            "spike",
            "spin",
            "spinout",
            "splatter",
            "sportif",
            "sprint",
            "sprinter",
            "sprints",
            "spuds",
            "squares",
            "squirrel",
            "stack",
            "stage",
            "stair-gap",
            "stand",
            "stayer",
            "steed",
            "steerer",
            "stem",
            "sticky",
            "stoked",
            "stoned",
            "suck",
            "superman",
            "swag",
            "swingoff",
            "table",
            "taco",
            "team",
            "tech",
            "technical",
            "tempo",
            "tester",
            "thrash",
            "ti",
            "tifosi",
            "time-trial",
            "top",
            "topo",
            "tornado",
            "track",
            "trail",
            "train",
            "trainer",
            "trainer",
            "trials",
            "triangle",
            "tube",
            "tubular",
            "tuck",
            "turn",
            "tweak",
            "UCI",
            "upstroke",
            "urban",
            "USAC",
            "USCF",
            "valve",
            "velo",
            "velodrome",
            "wagon",
            "wall",
            "wash-out",
            "washboard",
            "water",
            "weight",
            "wheelie",
            "wheel",
            "winky",
            "wipeout",
            "wrench",
            "yellow",
            "zone",
            "zonk",
        };

        private static readonly Int32[] InviteCodeNumberList = new[] {
            5,
            9,
            10,
            11,
            20,
            40,
            29,
            49,
            48,
            50,
            51,
            52,
            53,
            54,
            55,
            56,
            100,
            112,
            700,
            650,
        };
    }
}
