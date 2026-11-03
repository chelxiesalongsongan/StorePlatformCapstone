using Microsoft.AspNetCore.Mvc;
using PaymentService.Simulation;

namespace PaymentService.Controllers;

[ApiController]
[Route("payments/v1/simulation")]
public class SimulationController(PaymentFailureSwitch failureSwitch, ILogger<SimulationController> log) : ControllerBase
{
    [HttpGet]
    public ActionResult<SimulationSettings> Get() => new SimulationSettings(failureSwitch.FailPayments);

    [HttpPut]
    public ActionResult<SimulationSettings> Set(SimulationSettings settings)
    {
        failureSwitch.FailPayments = settings.FailPayments;
        log.LogWarning("Simulate payment failure switched {State}", settings.FailPayments ? "ON" : "OFF");
        return new SimulationSettings(failureSwitch.FailPayments);
    }
}
