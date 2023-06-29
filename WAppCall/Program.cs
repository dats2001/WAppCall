using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Xml.Linq;

namespace WAppCall
{
    public static class WhatsAppUI
    {
        public static class Ru
        {
            public static string NewChatButton = "NewConvoButton";
            public static string VoiceCallButton = "Аудиозвонок";
            public static string QueryTextBox = "QueryTextBox";
            public static string GetStarted = "Начать";
            public static string QrImage = "QrImage";
            public static string Settings = "Открыть Настройки";
            public static string LogOut = "Выйти";
            public static string Yes = "Да";
            public static string Ok = "ОК";
            public static string VoiceCallTitle = "Аудиозвонок - WhatsApp";
            public static string Calling = "Соединение...";
            public static string CallStatusTextId = "CallStatusText";
            public static string PopupBoxName = "Всплывающее окно";
            public static string PopupBoxTextId = "Message";
            public static string CallEndStatusText = "Звонок завершён";
        }
    }

    public static class SIPConstants
    {
        public const string SIP_DEFAULT_USERNAME = "Nikita";
        public const string SIP_DEFAULT_FROMURI = "sip:from_code@goodboy";
        public const string SDP_MIME_CONTENTTYPE = "application/sdp";
        public const ushort DEFAULT_SIP_PORT = 5060;
    }
    internal class Program
    {
        protected static Process _process;
        protected static AutomationElement _root;
        protected static AutomationElement _rootOfCall;
        private static Dictionary<string, AutomationElement> _elements = new Dictionary<string, AutomationElement>();
        private static bool _isInCall = false;
        private static AutomationEventHandler UIAeventHandler;

        static void Main(string[] args)
        {
            if (args == null || args.Length==0 )
            {
                Environment.Exit(1);
            }
            string phoneNumber = args[0];
            // Запускаем процесс с приложением WhatsApp
            _ = Process.Start($"whatsapp://send?phone={phoneNumber}");
            Thread.Sleep(1000);

            // Ищем главное окно приложения
            _root = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, "WhatsApp"));

            if (_root == null)
            {
                Console.WriteLine("Приложение не установлено или не запущено.");
                Environment.Exit(1);
            }

            try
            {

                PushButton(WhatsAppUI.Ru.VoiceCallButton);
                Thread.Sleep(1000);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Возможно необходимо залогиниться и отсканировать QR Code");
            }
            _rootOfCall = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, WhatsAppUI.Ru.VoiceCallTitle));

            if ( _rootOfCall != null )
            {
                Console.WriteLine($"Идет набор телефонного номера: {phoneNumber}");

                var participants = _rootOfCall.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "ParticipantList"));

                // Добавим событие для анализа поднял ли трубку клиент
                if (participants != null )
                {
                    Automation.AddStructureChangedEventHandler(participants, TreeScope.Children, OnStructureChange);                
                }

                // Добавим событие закрытия окна вызова / окончания звонка
                Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, _rootOfCall, TreeScope.Element,
                         UIAeventHandler = new AutomationEventHandler(OnUIAutomationEvent));

            }
            Console.ReadLine();
        }

        private static void OnStructureChange(object sender, StructureChangedEventArgs e)
        {
            var callWindow = (AutomationElement)sender;
            if (!_isInCall)
            {
                Console.WriteLine($"По всей видимости взяли  трубку...{e.EventId.Id}, " +
                    $"{callWindow.Current.AutomationId}, {callWindow.Current.Name}");
                _isInCall = true;
            }
            //Remove hanler
            Automation.RemoveStructureChangedEventHandler((AutomationElement)sender, OnStructureChange);
        }

        private static void OnUIAutomationEvent(object src, AutomationEventArgs e)
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
                Console.WriteLine("Звонок завершен!");
            }
            else
            {
                // TODO Handle any other events that have been subscribed to.
            }
        }





        protected static InvokePattern GetInvokePattern(AutomationElement element)
        {
            return element.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
        }
        protected static void PushButton(string name) => GetInvokePattern(GetButton(name)).Invoke();

        protected static AutomationElement GetButton(string name)
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
}
