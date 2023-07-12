using Rosbank.DRPZ.WAppAutomation.Domain.EventArgs;

namespace Rosbank.DRPZ.WAppAutomation.Domain.Interfaces;

public interface IWAppDesktopClient
{
    public string Phone { get; set; }

    public event Func<WAppCallEventArgs, Task> CallStatusChanged;

    public Task Call(string phone);


    public Task OnCallStatusChanged(WAppCallEventArgs args);
}
