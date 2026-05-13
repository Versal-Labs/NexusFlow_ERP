using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.HR.Commands;
using NexusFlow.AppCore.Features.HR.Queries;
using NexusFlow.AppCore.Jobs;
using NexusFlow.Infrastructure.Jobs.Runners;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class AttendanceController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IBackgroundJobClient _jobClient;
        public AttendanceController(IMediator mediator, IBackgroundJobClient jobClient)
        {
            _mediator = mediator;
            _jobClient = jobClient;
        }

        // The biometric machine hits this endpoint. 
        // You might want to secure this with an API Key rather than a JWT, 
        // since hardware devices usually don't support JWT login flows.
        [HttpPost("sync-biometrics")]
        public async Task<IActionResult> SyncBiometrics([FromBody] SyncBiometricDataCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // <summary>Reprocess attendance for any past date.</summary>
        [HttpPost("reprocess")]
        public IActionResult Reprocess([FromQuery] DateTime? date)
        {
            var jobId = _jobClient.Enqueue<ProcessDailyAttendanceJobRunner>(
                runner => runner.RunAsync(null!, date, CancellationToken.None));

            return Accepted(new { jobId, date = date?.ToString("yyyy-MM-dd") ?? "yesterday" });
        }

        [HttpGet("daily-records")]
        public async Task<IActionResult> GetDailyRecords([FromQuery] DateTime date)
        {
            var result = await _mediator.Send(new GetDailyAttendanceQuery { Date = date });
            return Ok(result);
        }

        [HttpPost("override-record")]
        public async Task<IActionResult> OverrideRecord([FromBody] OverrideAttendanceCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // Endpoint to let HR manually trigger the nightly job for a specific date
        [HttpPost("process-day")]
        public async Task<IActionResult> ProcessDay([FromQuery] DateTime date, [FromServices] ProcessDailyAttendanceJob job)
        {
            try
            {
                await job.ExecuteAsync(date);
                return Ok(new { message = "Attendance processing completed for " + date.ToString("yyyy-MM-dd") });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
