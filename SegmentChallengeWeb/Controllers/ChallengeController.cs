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

            var results = new List<(Effort Effort, Athlete Athlete, Int32 LapCount)>();

            if (challenge.Type == ChallengeType.MostLaps) {
                foreach (var effortGroup in efforts.GroupBy(e => e.Athlete.Id)) {
                    var (effort, athlete, lapCount) =
                        effortGroup.Aggregate(
                            (effort: (Effort)null, athlete: (Athlete)null, lapCount: 0),
                            (total, nextEffort) => {
                                if (total.effort == null) {
                                    return (nextEffort.Effort, nextEffort.Athlete, 1);
                                } else {
                                    return (total.effort.WithElapsedTime(total.effort.ElapsedTime + nextEffort.Effort.ElapsedTime),
                                        total.athlete,
                                        total.lapCount + 1);
                                }
                            });
                    results.Add((effort, effortGroup.First().Athlete, lapCount));
                }
            } else {
                Athlete currentAthlete = null;
                Effort bestEffort = null;

                foreach (var effort in efforts.Append(null)) {
                    if (effort == null || effort.Athlete.Id != currentAthlete?.Id) {
                        if (bestEffort != null) {
                            results.Add((bestEffort, currentAthlete, 1));
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
            }

            (String Gender, Int32 MaxAge) GetCategory(Athlete athlete) {
                var birthDateYear =
                    (athlete.BirthDate?.Year).GetValueOrDefault(DateTime.UtcNow.Year - 90);
                var age = DateTime.UtcNow.Year - birthDateYear;

                var ageGroup = ageGroups.SkipWhile(ag => age > ag.MaximumAge).First();

                return (athlete.Gender.GetValueOrDefault('M').ToString(), ageGroup.MaximumAge);
            }

            var resultsByCategory = new List<(Effort Effort, Athlete Athlete, Int32 LapCount, Boolean IsKOM)>();

            (String Gender, Int32 MaxAge) currentCategory = (null, 0);

            foreach (var (effort, athlete, lapCount) in results.OrderBy(e => GetCategory(e.Athlete)).ThenByDescending(e => e.LapCount)
                .ThenBy(e => e.Effort.ElapsedTime)) {
                var category = GetCategory(athlete);
                if (category != currentCategory) {
                    resultsByCategory.Add((effort, athlete, lapCount, true));
                    currentCategory = category;
                } else if (resultsByCategory.Count > 0 &&
                    resultsByCategory[^1].LapCount == lapCount &&
                    resultsByCategory[^1].Effort.ElapsedTime == effort.ElapsedTime) {
                    // Tie for first
                    resultsByCategory.Add((effort, athlete, lapCount, true));
                } else {
                    resultsByCategory.Add((effort, athlete, lapCount, false));
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

            var update = await updatesTable.AddAsync(
                new Update {
                    AthleteId = identity.UserId,
                    ChallengeId = challenge.Id
                },
                cancellationToken
            );

            await dbContext.SaveChangesAsync(cancellationToken);

            var updateId = update.Entity.Id;

            this.taskService.QueueTask<EffortRefresher>(
                (service, taskCancellationToken) =>
                    service.RefreshAthleteEfforts(updateId, name, identity.UserId, taskCancellationToken)
            );

            return new JsonResult(new { updateId });
        }

        private const Double Tolerance = 20.0;
        private static readonly XNamespace svgns = "http://www.w3.org/2000/svg";

        [HttpPost("{name}/upload_activity")]
        public async Task<IActionResult> UploadActivity(
            String name,
            [FromQuery] Int64? athlete,
            [FromQuery] Boolean debug,
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

            if (athlete.HasValue && !IsAdmin(identity)) {
                return Forbid();
            }

            if (String.IsNullOrEmpty(challenge.GpxData)) {
                return BadRequest("This challenge does not have segment GPX data available.");
            }

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

            String renderPoint(TrackPoint point) {
                return $"{(point.Longitude - start.Longitude) * 1000:f3},{(point.Latitude - start.Latitude) * -1000:f3}";
            }

            foreach (var routePoint in route.Track.Segments.SelectMany(seg => seg.Points)) {
                if (skippedPoints > 400) {
                    // Give up
                    break;
                }

                if (start == null) {
                    // Scan forward until we find a point within 20 meters of the start point
                    while (currentSegmentIx < ride.Track.Segments.Count && currentPointIx < ride.Track.Segments[currentSegmentIx].Points.Count &&
                        Distance(routePoint, ride.Track.Segments[currentSegmentIx].Points[currentPointIx]) > Tolerance) {

                        startDistanceList.Add((Int32)Distance(routePoint, ride.Track.Segments[currentSegmentIx].Points[currentPointIx]));
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

                        routePath.Append($"M {renderPoint(routePoint)} L");
                        ridePath.Append($"M {renderPoint(start)} L");
                        matchPath.Append($"M {renderPoint(routePoint)} L {renderPoint(start)}");
                    } else {
                        // Unable to find start, give up
                        Console.WriteLine($"WTF, checked {startDistanceList.Count} points with no start.");
                        break;
                    }
                } else {
                    // Scan forward until we find the next point, keeping track of gaps
                    var nextSegmentIx = currentSegmentIx;
                    var nextPointIx = currentPointIx;
                    var nextPoint = ride.Track.Segments[nextSegmentIx].Points[nextPointIx];

                    var gap = TimeSpan.Zero;
                    while (Distance(routePoint, nextPoint) > Tolerance && Distance(routePoint, nextPoint) < 2000) {
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
                        var interval = nextPoint.Time.Subtract(previousPoint.Time).TotalSeconds;
                        if (interval > 0) {
                            var speed = Distance(previousPoint, nextPoint) / interval;
                            // If travelling at less that 0.5 mph don't count this time interval
                            // if (speed < 0.22352) {
                            // If travelling at less that 1 mph don't count this time interval
                            if (speed < 0.44704) {
                                gap = gap.Add(TimeSpan.FromSeconds(interval));
                            }
                        }
                    }


                    if (Distance(routePoint, nextPoint) <= Tolerance) {
                        match = true;
                        matchDistanceList.Add((Int32)Distance(routePoint, nextPoint));
                        matchIxList.Add(nextPointIx);
                        if (gap > TimeSpan.Zero) {
                            gapList.Add(gap.TotalSeconds);
                            gaps = gaps.Add(gap);
                        }

                        ridePath.Append($" {renderPoint(nextPoint)}");
                        if (skipping) {
                            skippedPath.Append($" {renderPoint(routePoint)}");
                            routePath.Append($" M {renderPoint(routePoint)} L");
                            skipping = false;
                        } else {
                            routePath.Append($" {renderPoint(routePoint)}");
                        }

                        if (matchCount % 10 == 0) {
                            matchPath.Append($"M {renderPoint(routePoint)} L {renderPoint(nextPoint)}");
                        }

                        currentPointIx = nextPointIx;
                        currentSegmentIx = nextSegmentIx;
                        matchCount++;
                    } else {
                        // If distance goes over 200 meters, try skipping this track point.
                        match = false;
                        skippedPointList.Add(routePoint);
                        skippedPoints++;

                        if (skipping) {
                            // continue skip mode
                            skippedPath.Append($" {renderPoint(routePoint)}");
                        } else {
                            // Switch to skip mode
                            routePath.Append($" {renderPoint(routePoint)}");
                            skippedPath.Append($" M {renderPoint(routePoint)} L");
                            skipping = true;
                        }
                    }
                }
            }

            if (debug) {
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
            } else if (match) {
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
                    effort.StartDate = start.Time;
                    effort.ElapsedTime =
                        challenge.UseMovingTime ?
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

                await dbContext.SaveChangesAsync(cancellationToken);

                return new JsonResult(new {
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

        private Boolean IsAdmin(JwtCookiePrincipal identity) {
            return this.siteConfiguration.Value.Administrators != null &&
                this.siteConfiguration.Value.Administrators.Contains(identity.UserId);
        }
    }
}
