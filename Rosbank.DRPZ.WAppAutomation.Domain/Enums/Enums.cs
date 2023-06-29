using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rosbank.DRPZ.WAppAutomation.Domain.Enums
{
    public enum WhatsAppStatus
    { 
        LoginRequired,
        Busy,
        Ready
    }

    public enum WAppCallStatus
    {
        Calling,
        VoiceInteraction,
        CallFinished
    }
}
