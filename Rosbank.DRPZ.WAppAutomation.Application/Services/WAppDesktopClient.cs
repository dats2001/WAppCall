using Microsoft.Extensions.Logging;
using Rosbank.DRPZ.WAppAutomation.Domain.EventArgs;
using Rosbank.DRPZ.WAppAutomation.Domain.Interfaces;
using System.Diagnostics;
using System.Windows.Automation;

namespace Rosbank.DRPZ.WAppAutomation.Application.Services;


public class WAppDesktopClient : IWAppDesktopClient
{
    protected Process _process;
    protected AutomationElement _root;
    protected static AutomationElement _rootOfCall;
    private Dictionary<string, AutomationElement> _elements = new Dictionary<string, AutomationElement>();
    private Stopwatch _stopwatch = new Stopwatch();
    private static bool _isInCall = false;
    private static AutomationEventHandler UIAeventHandler;

    private DateTime StartInteraction;

    string currentSessionPhone;

    private readonly ILogger _logger;

    public string Phone 
    {
        get
        {
            return currentSessionPhone;
        }
        set
        {
            currentSessionPhone = value;
        }
     }

    public WAppDesktopClient(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<WAppDesktopClient>();
    }

    private async Task StartWhatsAppApplication(string appName, string rootElement, int timeoutInMs = 7000)
    {
        //_process = Process.Start(appName);


        _process = new Process();
         ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = "/C start " + appName;
        _process.StartInfo = startInfo;
        _process.Start();

        do
        {
            _root = AutomationElement.RootElement
                .FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, rootElement));
            await Task.Delay(1000);
        }
        while (_root == null && _stopwatch.ElapsedMilliseconds < timeoutInMs);
    }

    private void CloseWhatsAppApplication()
    {
        if (_process != null)
        {
            _process.CloseMainWindow();
            _process.Dispose();
        }
    }


    public event Func<WAppCallEventArgs, Task> CallStatusChanged;

    public async Task OnCallStatusChanged(WAppCallEventArgs args)
    {
        Func<WAppCallEventArgs, Task> handler = CallStatusChanged;
        if (handler == null)
            return;

        Delegate[] invocationList = handler.GetInvocationList();
        Task[] handlerTasks = new Task[invocationList.Length];

        for (int i = 0; i < invocationList.Length; i++)
        {
            handlerTasks[i] = ((Func<WAppCallEventArgs, Task>)invocationList[i])(args);
        }
        await Task.WhenAll(handlerTasks);
    }

    public async Task Call(string phone)
    {
        try
        {
            this.Phone = phone;
            await StartWhatsAppApplication($"whatsapp://send?phone={phone}", "WhatsApp");

            try
            {
                PushButton(WhatsAppUI.RuName.VoiceCallButton);
                Thread.Sleep(1000);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex.Message);
                _logger.LogInformation("Возможно необходимо залогиниться и отсканировать QR Code");
            }
            _rootOfCall = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, WhatsAppUI.RuName.VoiceCallTitle));

            if (_rootOfCall != null)
            {
                await OnCallStatusChanged(new WAppCallEventArgs
                {
                    PhoneNumber = this.Phone,
                    Status = Domain.Enums.WAppCallStatus.Calling,
                    StartDate = DateTime.MinValue,
                    Duration = TimeSpan.FromSeconds(0)
                });

                _logger.LogInformation($"Идет набор телефонного номера: {phone}");

                var participants = _rootOfCall.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "ParticipantList"));

                // Добавим событие для анализа поднял ли трубку клиент
                if (participants != null)
                {
                    Automation.AddStructureChangedEventHandler(participants, TreeScope.Children, OnStructureChange);
                }

                // Добавим событие закрытия окна вызова / окончания звонка
                Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, _rootOfCall, TreeScope.Element,
                         UIAeventHandler = new AutomationEventHandler(OnUIAutomationEvent));

            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }

    private void OnStructureChange(object sender, StructureChangedEventArgs e)
    {
        var callWindow = (AutomationElement)sender;
        if (!_isInCall)
        {
            _logger.LogInformation($"По всей видимости взяли  трубку...{e.EventId.Id}, " +
                $"{callWindow.Current.AutomationId}, {callWindow.Current.Name}");

            StartInteraction = DateTime.Now;
            
            OnCallStatusChanged(new WAppCallEventArgs
            {
                PhoneNumber = this.Phone,
                Status = Domain.Enums.WAppCallStatus.VoiceInteraction,
                StartDate = StartInteraction,
                Duration = TimeSpan.FromSeconds(0)
            });
            _isInCall = true;
        }
        //Remove hanler
        Automation.RemoveStructureChangedEventHandler((AutomationElement)sender, OnStructureChange);
    }

    private void OnUIAutomationEvent(object src, AutomationEventArgs e)
    {
        // Make sure the element still exists. Elements such as tooltips
        // can disappear before the event is processed.
        AutomationElement sourceElement;
        try
        {
            sourceElement = src as AutomationElement;
        }
        catch (ElementNotAvailableException)
        {
            return;
        }
        if (e.EventId == WindowPattern.WindowClosedEvent)
        {
            OnCallStatusChanged(new WAppCallEventArgs
            {
                PhoneNumber = this.Phone,
                Status = Domain.Enums.WAppCallStatus.CallFinished,
                StartDate = StartInteraction,
                Duration = (StartInteraction==DateTime.MinValue) ? TimeSpan.Zero : TimeSpan.FromSeconds((DateTime.Now - StartInteraction).TotalMilliseconds)    
            });
            _logger.LogInformation("Звонок завершен!");
        }
        else
        {
            // TODO Handle any other events that have been subscribed to.
        }
    }


    protected InvokePattern GetInvokePattern(AutomationElement element)
    {
        return element.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
    }
    protected void PushButton(string name) => GetInvokePattern(GetButton(name)).Invoke();

    protected AutomationElement GetButton(string name)
    {
        if (_elements.TryGetValue(name, out AutomationElement elem))
        {
            return elem;
        }

        var result = _root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "AudioCallButton"));

        if (result == null)
        {
            throw new ArgumentException("No function button found with name: " + name);
        }

        _elements.Add(name, result);

        return result;
    }
}
