using System;
using Microsoft.AspNetCore.Mvc;

namespace SegmentChallengeWeb.Controllers {
    [Route("api/status")]
    [ApiController]
    public class StatusController : ControllerBase {
        [HttpGet("health")]
        public IActionResult HealthCheck() {
            return new JsonResult(new {
                typeof(StatusController).Assembly.GetName().Version,
                CurrentTime = DateTime.UtcNow
            });
        }
    }
}
