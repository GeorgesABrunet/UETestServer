using Core.Logging;
using Core.Networking;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MQTTnet.Server;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Client.Options;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace UETestServer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private TCPServer m_Server;

        private IMqttServer m_MqqtServer;
        private IManagedMqttClient m_MqqtPublisher;

        private System.Timers.Timer statusTimerSendMessage;
        //private System.Timers.Timer statusReceivedTimer;

        private ServerType m_ServerType;

        public bool IsServerRunning
        {
            get
            {
                return (m_ServerType == ServerType.MQTT && m_MqqtServer != null) || (m_Server != null && m_Server.IsRunning);
            }
        }
        
        #region UE Vars

        private int SequencerIndex = 0;
        private int LocalSequencerIndex = 0;
        public bool bQueueMessages = false;
        public List<FSequenceParams> QueuedMessages = new System.Collections.Generic.List<FSequenceParams>();

        #endregion
        public MainWindow()
        {
            InitializeComponent();

            Logger.LogReceived += Logger_LogReceived;

            Logger.Info("Ready.");

            RefreshUI();

            lSendMessageStatus.Content = String.Empty;

            statusTimerSendMessage = new System.Timers.Timer(5000);
            statusTimerSendMessage.Elapsed += StatusTimerSendMessage_Elapsed;

            cbServerType.SelectionChanged += cbServerType_SelectionChanged;

            Logger.Info("[TUTORIAL] UE Test Server requires appropriate connection parameters defined for the chosen protocol. ");

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != m_Server)
            {
                m_Server.Stop();
                m_Server = null;
            }
        }

        private void RefreshUI()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (IsServerRunning)
            {
                btnListen.Content = "Stop";
                bStatus.Background = Brushes.Green;
                btnSendMessage.IsEnabled = true;
            }
            else
            {
                btnListen.Content = "Start";
                bStatus.Background = Brushes.Red;
                btnSendMessage.IsEnabled = false;
            }

            tbMsgAudioFilePC.IsEnabled = btnMsgAudioFilePC.IsEnabled = cbSendNullAudioPC.IsEnabled = cbSendMsgAudioPC.IsChecked.Value;

            tbSetMessageTxt.IsEnabled = cb_MessageTxt.IsChecked.Value;

            //Browsing ability is only available if option to send a JSON msg is checked
            btnModMsgBrowseFilePC.IsEnabled = tbJSONFilePC.IsEnabled = cbSendMsgJSON.IsChecked.Value;
            
            //Connection parameters will only be available to change when server is not running
            tbAddress.IsEnabled = tbPort.IsEnabled = cbServerType.IsEnabled = tbHostName.IsEnabled = tbUserName.IsEnabled = tbPassName.IsEnabled = tbClientId.IsEnabled = !IsServerRunning;

           
        }

        private void Logger_LogReceived(object sender, Logger.LogEventArgs e)
        {
            if (!Dispatcher.CheckAccess()) 
            {
                Dispatcher.Invoke(new Logger.LogEventHandler(Logger_LogReceived), sender, e);
                return;
            }

            tbLog.AppendText(e.Message);
            tbLog.AppendText(Environment.NewLine);

            tbLog.ScrollToEnd();
        }

        private async void btnListen_Click(object sender, RoutedEventArgs e)
        {
            if (IsServerRunning)
            {
                // stop all servers

                if (m_Server != null)
                {
                    m_Server.Stop();
                }

                if (m_MqqtPublisher != null)
                {
                    await m_MqqtPublisher.StopAsync();
                    m_MqqtPublisher = null;
                }
                if (m_MqqtServer != null)
                {
                    await m_MqqtServer.StopAsync();
                    m_MqqtServer = null;
                    Logger.Info("[MQTT] Server stopped");
                }
            }
            else
            {
                if (m_ServerType == ServerType.TCP)
                {

                    // start tcp message server
                    if (m_Server == null)
                    {
                        m_Server = new TCPServer();
                        m_Server.OnClientConnect += M_Server_OnClientConnect;
                        m_Server.OnDataReceived += M_Server_OnDataReceived;
                    }

                    if (!m_Server.IsRunning)
                    {
                        m_Server.Start(String.Format("{0}:{1}", tbAddress.Text, tbPort.Text), true);
                    }

                }
                else
                {
                    try
                    {
                        // start mqtt server
                        m_MqqtServer = new MqttFactory().CreateMqttServer();
                        m_MqqtServer.UseClientConnectedHandler(M_MqqtServer_ClientConnected);
                        m_MqqtServer.UseClientDisconnectedHandler(M_MqqtServer_ClientDisconnected);
                        m_MqqtServer.UseApplicationMessageReceivedHandler(M_MqqtServer_MessageReceived);

                        MqttServerOptions options = new MqttServerOptions();
                        options.DefaultEndpointOptions.BoundInterNetworkAddress = System.Net.IPAddress.Parse(tbAddress.Text);
                        options.DefaultEndpointOptions.Port = int.Parse(tbPort.Text);
                        
                        await m_MqqtServer.StartAsync(options);

                        Logger.Info("[MQTT] Server started");


                        // start mqtt publisher

                        ManagedMqttClientOptions publisherOptions = new ManagedMqttClientOptionsBuilder()
                            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                            .WithClientOptions(new MqttClientOptionsBuilder()
                                .WithClientId(tbClientId.Text)
                                .WithMaximumPacketSize(0)
                                .WithTcpServer(tbAddress.Text, int.Parse(tbPort.Text))
                                .WithCredentials(tbUserName.Text, tbPassName.Text)
                                .Build())
                            .Build();

                        m_MqqtPublisher = new MqttFactory().CreateManagedMqttClient();
                        //await m_MqqtPublisher.SubscribeAsync(new TopicFilterBuilder().WithTopic(m_MqqtTopic).Build());
                        await m_MqqtPublisher.StartAsync(publisherOptions);

                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[MQTT] Error starting server: {0}", ex.Message);

                        if (null != m_MqqtPublisher)
                        {
                            await m_MqqtPublisher.StopAsync();
                            m_MqqtPublisher = null;
                        }

                        await m_MqqtServer.StopAsync();
                        m_MqqtServer = null;

                    }
                }
            }

            RefreshUI();
        }

        #region MQQT Server

        private void M_MqqtServer_ClientConnected(MqttServerClientConnectedEventArgs eventArgs)
        {
            Logger.Info("[MQTT] Client connected: {0}", eventArgs.ClientId);            
        }

        private void M_MqqtServer_ClientDisconnected(MqttServerClientDisconnectedEventArgs eventArgs)
        {
            Logger.Info("[MQTT] Client disconnected: {0}", eventArgs.ClientId);
        }

        private void M_MqqtServer_MessageReceived(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            //var item = $"Timestamp: {DateTime.Now:O} | Topic: {eventArgs.ApplicationMessage.Topic} | Payload: {eventArgs.ApplicationMessage.ConvertPayloadToString()} | QoS: {eventArgs.ApplicationMessage.QualityOfServiceLevel}";

            if (eventArgs.ClientId.Equals("UETestServer", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ProcessMessage(eventArgs.ApplicationMessage.ConvertPayloadToString());
        }

        #endregion MQQT Server


        #region TCP Server

        private void M_Server_OnClientConnect(object sender, Core.Networking.Sockets.RemoteConnection connection)
        {
            CallRPC("setStatusVerbosity", "verbose");
        }


        private void M_Server_OnDataReceived(object sender, Core.Networking.Sockets.Package packet)
        {
            ProcessMessage(packet.m_Text);
        }

        #endregion TCP Server


        private string SerializeJson(object data)
        {
            if (null == data)
            {
                return String.Empty;
            }

            JsonSerializerSettings Settings = new JsonSerializerSettings();
            Settings.NullValueHandling = NullValueHandling.Ignore;
            Settings.DefaultValueHandling = DefaultValueHandling.Ignore;
            
            return JsonConvert.SerializeObject(data, Settings);
        }

        private void BroadcastMessage(string messagePath, object message)
        {
            if (null == message)
            {
                return;
            }

            BroadcastMessage(messagePath, SerializeJson(message));
        }

        private void BroadcastMessage(string messagePath, string messageJson)
        {
            if (!IsServerRunning)
            {
                return;
            }

            if (m_ServerType == ServerType.TCP)
            {
                m_Server.Broadcast(messageJson);
            }
            else
            {   //Assuming server type is MQTT...
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(messageJson);

                var message = new MqttApplicationMessageBuilder()
                     .WithTopic(messagePath.TrimStart('/')).WithPayload(payload).Build();

                m_MqqtPublisher.PublishAsync(message);
            }
        }

        private void CallRPC(string rpcName, params object[] parameters)
        {
            List<object> rpc = new List<object>();
            rpc.Add(rpcName);
            rpc.AddRange(parameters);

            List<object> toSend = new List<object>();
            toSend.Add("call");
            toSend.Add(rpc);

            BroadcastMessage("call", toSend);
        }

        private void miClear_Click(object sender, RoutedEventArgs e)
        {
            tbLog.Clear();
        }

        private void SetStatus(Label label, string status, bool autocomplete = false)
        {
            label.Content = status.ToUpper();
            label.Foreground = GetStatusColor(status);


            System.Timers.Timer timer = null;
            if (label == lSendMessageStatus)
            {
                timer = statusTimerSendMessage;
            }


            if (null != timer)
            {
                timer.Stop();

                if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    timer.Start();
                }
                else if (autocomplete)
                {
                    timer.Interval = 1000;
                    timer.Start();
                }
            }
        }

        //GetStatusColor is used to color code the status of outbound messages
        private Brush GetStatusColor(string status)
        {
            switch (status.Trim().ToLower())
            {
                case "sending":
                    {
                        return Brushes.DarkGray;
                    }
                case "completed":
                    {
                        return Brushes.Green;
                    }
                case "started":
                    {
                        return Brushes.Orange;
                    }
                default:
                    {
                        return Brushes.Red;
                    }
            }
        }

        private void cbCheckedRefreshUI(object sender, RoutedEventArgs e)
        {
            RefreshUI();
        }

        private void cbServerType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse<ServerType>(((ComboBoxItem)cbServerType.SelectedItem).Content.ToString(), out m_ServerType);

            RefreshUI();
        }

        #region User Specific

        private void ProcessMessage(string messageText)
        {
            if (String.IsNullOrWhiteSpace(messageText))
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(delegate
            {
                FMessageTemplate message;

                try
                {

                    message = JsonConvert.DeserializeObject<FMessageTemplate>(messageText);

                    switch (message.Topic) //For all expected message topics, define case.
                    {
                        default:
                            {
                                if(message != null)
                                {
                                    Logger.Info("[FROM CLIENT] {0} Hit default.", messageText);
                                }
                                else
                                {
                                    Logger.Info("[FROM CLIENT] Hit default.");
                                }
                                

                                SetStatus(lSendMessageStatus, "COMPLETED");
                                break;
                            }
                    }

                    if (bQueueMessages)
                    {
                        if (cbLoopSequence.IsChecked == true)
                        {
                            if (SequencerIndex < QueuedMessages.Count)
                            {
                                SequencerIndex = SequencerIndex + 1;
                            }
                            else
                            {
                                SequencerIndex = 0;
                            }

                            SendNextMessage();
                        }
                        else
                        {
                            if (SequencerIndex < QueuedMessages.Count)
                            {
                                SequencerIndex = SequencerIndex + 1;
                                SendNextMessage();
                            }
                            else
                            {
                                SequencerIndex = 0;
                                bQueueMessages = false;
                            }
                        }

                       
                    }
                }
                catch
                {
                    Logger.Info("[STATUS] Warning: failed to deserialize message: {0}", messageText);

                    if (cbLoopSequence.IsChecked == true)
                    {
                        SequencerIndex = 0;

                        SendNextMessage();
                    }
                }
            }));
        }
        public string GetJSONfromFile(string InputFile)
        {
            string jsonString = null;
            if (File.Exists(InputFile))
            {
                try
                {
                    jsonString = File.ReadAllText(InputFile);
                }
                catch(Exception ex)
                {
                    Logger.Error("Error reading JSON file: {0}", ex.Message);
                }
            }

            return jsonString;
        }


        private void StatusTimerSendMessage_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new System.Timers.ElapsedEventHandler(StatusTimerSendMessage_Elapsed), sender, e);
                return;
            }

            lSendMessageStatus.Content = String.Empty;

            ((System.Timers.Timer)sender).Stop();
        }

        private void tbSetMessageTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            //User Defined
        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (IsServerRunning)//Server must be running to send messages!
            {
                bQueueMessages = false;

                if (cbSendMsgJSON.IsChecked == true) //If using prebuilt messages
                {
                    string JSONmsg = GetJSONfromFile(tbJSONFilePC.Text);
                    
                    if (JSONmsg != null )
                    {
                        string LogData = JSONmsg;
                        if (LogData.Length > 300)
                        {
                            LogData = LogData.Substring(0, 300) + "... (truncated)";
                        }
                        Logger.Info("[SENDING]: raw JSON file --  {0}", LogData);

                    
                        BroadcastMessage(tbMsgTopic.Text, JSONmsg);
                        SetStatus(lSendMessageStatus, "SENDING");

                        if (cbSendMsgAudioPC.IsChecked.Value)
                        {
                            byte[] AudioData = new byte[0];

                            if(!cbSendNullAudioPC.IsChecked.Value)
                            {
                                AudioData = File.ReadAllBytes(tbMsgAudioFilePC.Text);
                            }
                            

                            string audiolength = AudioData.Length.ToString();
                            Logger.Info("[SENDING]: byte array length --  {0}", audiolength);

                            SendMsgAudioBytes(tbMsgTopic.Text, AudioData);
                        }
                    }
                    else
                    {
                        Logger.Info("Cannot send NULL message to client! Please review message components before trying again.");
                    }
                    
                }
                else // Modular messaging
                {
                    FMessageTemplate UEEvent = new FMessageTemplate(tbMsgTopic.Text, tbMsgSubTopic.Text);

                    //For all parameters that need values set from UI, add it here
                    if (cb_MessageTxt.IsChecked == true)
                    {
                        UEEvent.data.Txt = tbSetMessageTxt.Text;
                    }
                    
                    BroadcastMessage(tbMsgTopic.Text, UEEvent);
                    SetStatus(lSendMessageStatus, "SENDING");

                    if (cbSendMsgAudioPC.IsChecked.Value == true)
                    {
                        UEEvent.data.Relevant_WAV = tbMsgAudioFilePC.Text;

                        byte[] AudioData = new byte[0];

                        if (cbSendNullAudioPC.IsChecked.Value == false)
                        {
                            AudioData = File.ReadAllBytes(tbMsgAudioFilePC.Text);
                        }

                        string audiolength = AudioData.Length.ToString();
                        Logger.Info("[SENDING]: byte array length --  {0}", audiolength);

                        SendMsgAudioBytes(tbMsgTopic.Text, AudioData);
                    }
                }
            }
        }

        private void btnAddModMsgToSequencer_Click(object sender, RoutedEventArgs e)
        {

            FSequenceParams SequenceBit = new FSequenceParams();
            string JSONmsg = null;
            string AudioTopicString = null;
            string AudioFilename = null;

            if (cbSendMsgJSON.IsChecked == true) //Prebuilt messages
            {
                JSONmsg = GetJSONfromFile(tbJSONFilePC.Text);
            }
            else // Modular messaging
            {

                FMessageTemplate UEEvent = new FMessageTemplate(tbMsgTopic.Text, tbMsgSubTopic.Text);

                if (cbSendMsgAudioPC.IsChecked.Value)
                {
                    UEEvent.data.Relevant_WAV = tbMsgAudioFilePC.Text;
                }

                if (cb_MessageTxt.IsChecked == true)
                {
                    UEEvent.data.Txt = tbSetMessageTxt.Text;
                }

                JSONmsg = SerializeJson(UEEvent);
            }

            if (cbSendMsgAudioPC.IsChecked.Value)
            {
                AudioTopicString = tbMsgTopic.Text;

                AudioFilename = tbMsgAudioFilePC.Text;
                if(cbSendNullAudioPC.IsChecked.Value)
                {
                    AudioFilename = null;
                }
            }

            SequenceBit.JsonFilename = JSONmsg;
            SequenceBit.ContainsAudio = cbSendMsgAudioPC.IsChecked.Value;
            SequenceBit.SequenceAudioFilename = AudioFilename;
            SequenceBit.SequenceTopicName = tbMsgTopic.Text;
            
            string JsonLogData = SequenceBit.JsonFilename;

            if (JsonLogData != null)
            {
                if (JsonLogData.Length > 70)
                {
                    JsonLogData = JsonLogData.Substring(0, 70) + "... (truncated)";
                }

                QueuedMessages.Add(SequenceBit);

                Logger.Info("Adding msg to queue: {0} ", JsonLogData);

                if (LocalSequencerIndex == 0)
                {
                    tbSequenceLog.Clear();
                }

                LocalSequencerIndex++;

                string SequenceLogMsg = null;

                if (SequenceBit.ContainsAudio)
                {
                    SequenceLogMsg = JsonLogData + " -- " + SequenceBit.SequenceTopicName;
                }
                else
                {
                    SequenceLogMsg = JsonLogData + " -- No Audio";
                }

                tbSequenceLog.Text = tbSequenceLog.Text + SequenceLogMsg + "\n";
            }
            else
            {
                Logger.Info("Cannot add NULL message to queue! Please review message components before trying again.");
            }
        }

        private void SendMsgAudioBytes(string AudioTopic, byte[] AudioData)
        {
            // Custom audio data sending function to support wav files
            if (AudioData != null && AudioData.Length > 0)
            {
                var message = new MqttApplicationMessageBuilder().WithTopic(AudioTopic).WithPayload(AudioData).Build();

                m_MqqtPublisher.PublishAsync(message);
            }
        }

        private void btnMsgAudioFilePC_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Audio files|*.wav|All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                tbMsgAudioFilePC.Text = dialog.FileName;
            }
        }

        private void btnModMsgBrowseFilePC_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "JSON files|*.json|All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                tbJSONFilePC.Text = dialog.FileName;
            }
        }

        #endregion

        private void cbAddMsgToQueue_Click(object sender, RoutedEventArgs e)
        {

            string JSONmsg = null;
            string AudioTopicString = null;
            string AudioFilename = null;
            
            JSONmsg = GetJSONfromFile(tb_SeqJsonFile1.Text); //convert JSON file into string
            
            FSequenceParams SequenceBit = new FSequenceParams();
            SequenceBit.JsonFilename = JSONmsg;
            
            string JsonLogData = SequenceBit.JsonFilename;
            if (JsonLogData.Length > 30)
            {
                JsonLogData = JsonLogData.Substring(0, 30) + "... (truncated)";
            }

            SequenceBit.ContainsAudio = cbSendAudioFileForSequence.IsChecked.Value;

            if (cbSendAudioFileForSequence.IsChecked.Value)
            {
                AudioTopicString = tb_SeqTopic.Text;

                AudioFilename = tb_SequenceAudioFile.Text;

                if(cbSendEmptyAudioForSequence.IsChecked.Value == true)
                {
                    AudioFilename = null;
                }
            } 
            
            SequenceBit.SequenceAudioFilename = AudioFilename;
            SequenceBit.SequenceTopicName = AudioTopicString;

            QueuedMessages.Add(SequenceBit);

            Logger.Info("Adding msg to queue: {0} ", JsonLogData);
            
            if(LocalSequencerIndex == 0)
            {
                tbSequenceLog.Clear();
            }

            LocalSequencerIndex++;

            string SequenceLogMsg = null;

            if(SequenceBit.ContainsAudio)
            {
                SequenceLogMsg = JsonLogData + " -- " + SequenceBit.SequenceTopicName;
            }
            else
            {
                SequenceLogMsg = JsonLogData + " -- No Audio";
            }
            
            tbSequenceLog.Text = tbSequenceLog.Text  + SequenceLogMsg + "\n";


        }

        private void SendNextMessage()
        {
            if(QueuedMessages.Count() > 0)
            {
                Logger.Info("Sending next message...");

                BroadcastMessage(QueuedMessages[SequencerIndex].SequenceTopicName, QueuedMessages[SequencerIndex].JsonFilename);
            
                if (QueuedMessages[SequencerIndex].ContainsAudio)
                {
                    string AudioTopicString = QueuedMessages[SequencerIndex].SequenceTopicName;
                    string AudioFilename = QueuedMessages[SequencerIndex].SequenceAudioFilename; 

                    byte[] AudioData = new byte[0];

                    if (AudioFilename != null)
                    {
                        AudioData = File.ReadAllBytes(AudioFilename);
                    }
                    
                    string audiolength = AudioData.Length.ToString();
                    Logger.Info("[SENDING]: Message audio, {0} , {1} \n byte array length -- {2}", AudioTopicString, AudioFilename, audiolength);

                    SendMsgAudioBytes(AudioTopicString, AudioData);
                }
            }
            else
            {
                Logger.Info("No remaining messages in queue!");
            }
            
        }
        private void btn_FindSeqJsonFile1_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "JSON files|*.json|All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                tb_SeqJsonFile1.Text = dialog.FileName;
            }
        }

        private void btn_FindSequenceAudioFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Audio files|*.wav|All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                tb_SequenceAudioFile.Text = dialog.FileName;
            }
        }

        private void btnSendMessageSequence_Click(object sender, RoutedEventArgs e)
        {

            bQueueMessages = true;
            SequencerIndex = 0;

            SendNextMessage();
        }

        private void btnClearSequenceLog_Click(object sender, RoutedEventArgs e)
        {
            QueuedMessages.Clear();
            tbSequenceLog.Clear();
            LocalSequencerIndex = 0;
            tbSequenceLog.Text = "No messages in queue.  Add JSON messages by selecting a JSON file and clicking the Add Msg to Queue button." +
                "If using MQTT, please specify the Topic to use for the message to queue. ";
        }

        private void btnRemoveLastItem_Click(object sender, RoutedEventArgs e)
        {
            if(!(QueuedMessages.Count() == 0))
            {
                QueuedMessages.RemoveAt((QueuedMessages.Count() - 1));
                tbSequenceLog.Text = null;

                for(int i = 0; i < QueuedMessages.Count(); i++)
                {
                    string tempJsonName = QueuedMessages[i].JsonFilename;

                    if(tempJsonName.Length > 30)
                    {
                        tempJsonName = tempJsonName.Substring(0, 30) + "... (truncated)";
                    }

                    if (QueuedMessages[i].ContainsAudio)
                    {
                        tbSequenceLog.Text += tempJsonName + " -- " + QueuedMessages[i].SequenceTopicName;
                    }
                    else
                    {
                        tbSequenceLog.Text += tempJsonName + " -- No Audio";
                    }

                    tbSequenceLog.Text += "\n";
                }
            }
        }

        private void tbMsgTopic_TextChanged(object sender, TextChangedEventArgs e)
        {
            //Topic
        }
    }
}
