namespace PaymentService.Simulation;

/// Runtime switch so graders can force payment failures without editing code or stopping a service.
public class PaymentFailureSwitch
{
    private volatile bool _failPayments;
    public bool FailPayments
    {
        get => _failPayments;
        set => _failPayments = value;
    }
}

public record SimulationSettings(bool FailPayments);
