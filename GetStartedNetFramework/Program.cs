//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: A getting started program to demonstrate how to use the SIPSorcery
// library to place a call targeting the .Net Framework.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 17 Apr 2020 Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using NAudio.Wave;
using Serilog;
using Serilog.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;

namespace demo
{
    class Program
    {
        protected static Process _process;
        protected static AutomationElement _root;
        protected static AutomationElement _rootOfCall;
        private static Dictionary<string, AutomationElement> _elements = new Dictionary<string, AutomationElement>();
        private static bool _isInCall = false;
        private static AutomationEventHandler UIAeventHandler;
        private static string DESTINATION = "7777@178.154.207.15:5060";

        private static SIPTransport sipTransport;
        private static SIPUserAgent userAgent;
        private static WindowsAudioEndPoint winAudio;
        private static VoIPMediaSession voipMediaSession;

        private static readonly WaveFormat _waveFormat = new WaveFormat(8000, 16, 1);
        private static WaveFileWriter _waveFile;


        static async Task Main()
        {
            Console.WriteLine("SIPSorcery Getting Started Demo");

            AddConsoleLogger();

            _waveFile = new WaveFileWriter("output.mp3", _waveFormat);

            string phoneNumber = "+79119115650";
            sipTransport = new SIPTransport();
            userAgent = new SIPUserAgent(sipTransport, null);

            userAgent.ClientCallFailed += (uac, err, resp) =>
            {
                Console.WriteLine($"Call failed {err}");
                _waveFile?.Close();
            };
            userAgent.OnCallHungup += (dialog) => _waveFile?.Close();

            winAudio = new WindowsAudioEndPoint(new AudioEncoder(),-1);

            

            voipMediaSession = new VoIPMediaSession(new MediaEndPoints { AudioSink = winAudio, AudioSource = winAudio });
            voipMediaSession.OnRtpPacketReceived += OnRtpPacketReceived;

            // Запускаем процесс с приложением WhatsApp
            _process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/C start whatsapp://send?phone={phoneNumber}";
            _process.StartInfo = startInfo;
            _process.Start();
            
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

                PushButton("Аудиозвонок");
                Thread.Sleep(1000);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Возможно необходимо залогиниться и отсканировать QR Code");
            }
            _rootOfCall = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, "Аудиозвонок - WhatsApp"));

            if (_rootOfCall != null)
            {
                Console.WriteLine($"Идет набор телефонного номера: {phoneNumber}");

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
            Console.ReadLine();



            
        }

        private static void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            if (mediaType == SDPMediaTypesEnum.audio)
            {
                var sample = rtpPacket.Payload;

                for (int index = 0; index < sample.Length; index++)
                {
                    if (rtpPacket.Header.PayloadType == (int)SDPWellKnownMediaFormatsEnum.PCMA)
                    {
                        short pcm = NAudio.Codecs.ALawDecoder.ALawToLinearSample(sample[index]);
                        byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        _waveFile.Write(pcmSample, 0, 2);
                    }
                    else
                    {
                        short pcm = NAudio.Codecs.MuLawDecoder.MuLawToLinearSample(sample[index]);
                        byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        _waveFile.Write(pcmSample, 0, 2);
                    }
                }
            }
        }


        private static async void OnStructureChange(object sender, StructureChangedEventArgs e)
        {
            var callWindow = (AutomationElement)sender;
            if (!_isInCall)
            {
                Console.WriteLine($"По всей видимости взяли  трубку...{e.EventId.Id}, " +
                    $"{callWindow.Current.AutomationId}, {callWindow.Current.Name}");
                _isInCall = true;


                // Place the call and wait for the result.
                bool callResult = await userAgent.Call(DESTINATION, null, null, voipMediaSession);
                Console.WriteLine($"Call result {((callResult) ? "success" : "failure")}.");

                Console.WriteLine("press any key to exit...");
                Console.Read();

                if (userAgent.IsCallActive)
                {
                    Console.WriteLine("Hanging up.");
                    userAgent.Hangup();
                }

                // Clean up.
                sipTransport.Shutdown();
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

        /// <summary>
        ///  Adds a console logger. Can be omitted if internal SIPSorcery debug and warning messages are not required.
        /// </summary>
        private static void AddConsoleLogger()
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.Console()
                .CreateLogger();
            var factory = new SerilogLoggerFactory(serilogLogger);
            SIPSorcery.LogFactory.Set(factory);
        }
    }
}