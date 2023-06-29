using Rosbank.DRPZ.WAppAutomation.Domain.Enums;

namespace Rosbank.DRPZ.WAppAutomation.Domain.EventArgs;

public class WAppCallEventArgs
{
    public string PhoneNumber { get; set; }

    public DateTime StartDate { get; set; }

    public TimeSpan Duration { get; set; }

    public DateTime EndDate 
    {
        get
        {
            return this.StartDate.Add(Duration);
        }
    }

    public WAppCallStatus Status { get; set; }
}
