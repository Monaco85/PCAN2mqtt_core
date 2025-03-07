﻿using can2mqtt.Translator.StiebelEltron;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Server;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Peak.Can.Basic;
using Microsoft.Extensions.Logging;
using Peak.Can.Basic.BackwardCompatibility;
using System.Globalization;


namespace can2mqtt
{
    //PEAK handle data type
    using TPCANHandle = UInt16;

    public class Can2Mqtt : BackgroundService
    {

        private readonly ILogger Logger;
        private IMqttClient MqttClient;
        private IMqttClientOptions MqttClientOptions;
        private string CanTranslator;
        private string CanServer;
        private int CanServerPort = 29536;
        private bool CanForwardWrite = true;
        private bool CanForwardRead = false;
        private bool CanForwardResponse = true;
//        private int CanReceiveBufferSize = 48;
        private string CanSenderId;
//        private string CanInterfaceName = "slcan0";
        private bool NoUnit = false;
        private string MqttTopic = "";
        private string MqttUser;
        private string MqttPassword;
        private string MqttServer;
        private string MqttClientId;
        private bool MqttAcceptSet = false;
        //private NetworkStream TcpCanStream;
        //private TcpClient ScdClient = null;
        private StiebelEltron Translator = null;
        private string Language = "EN";
        private bool ConvertUnknown = false;
        //PEAK handle data type
        TPCANHandle canHandle = PCANBasic.PCAN_USBBUS1;
        private bool AutoPolling = false;
        private int AutoPollingInterval = 120; // in seconds
        private int AutoPollingThrottle = 150; // in milliseconds
        private Task AutoPollingTask = null;
        byte[] CanSenddata = new byte[9];


        private readonly ILoggerFactory LoggerFactory;

        public Can2Mqtt(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            Logger = loggerFactory.CreateLogger("can2mqtt");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(10, stoppingToken);
            }
        }

        /// <summary>
        /// Loads the config.json to the local variables.
        /// </summary>
        private bool LoadConfig()
        {
            try
            {
                if (!File.Exists("./config.json"))
                {
                    Logger.LogError("Cannot find config.json. Copy and rename the config-sample.json and adjust your settings in that config file.");
                    return false;
                }

                var jsonString = File.ReadAllText("config.json");
                var config = JsonNode.Parse(jsonString);
                if (config == null)
                {
                    Logger.LogError("Unable to read config file.");
                    return false;
                }

                // Read the config file
                canHandle = PCANBasic.PCAN_USBBUS1;
                MqttTopic = Convert.ToString(config["MqttTopic"]);
                CanTranslator = Convert.ToString(config["MqttTranslator"]);
                CanForwardWrite = bool.Parse(config["CanForwardWrite"].ToString());
                CanForwardRead = bool.Parse(config["CanForwardRead"].ToString());
                CanForwardResponse = Convert.ToBoolean(config["CanForwardResponse"].ToString());
                NoUnit = Convert.ToBoolean(config["NoUnits"].ToString());
//                CanReceiveBufferSize = Convert.ToInt32(config["CanReceiveBufferSize"].ToString());
                MqttUser = Convert.ToString(config["MqttUser"]);
                MqttPassword = Convert.ToString(config["MqttPassword"]);
                MqttClientId = Convert.ToString(config["MqttClientId"]);
                MqttServer = Convert.ToString(config["MqttServer"]);
                CanServer = Convert.ToString(config["CanServer"]);
                CanServerPort = Convert.ToInt32(config["CanServerPort"].ToString());
                MqttAcceptSet = Convert.ToBoolean(config["MqttAcceptSet"].ToString());
                CanSenderId = Convert.ToString(config["CanSenderId"]);
//                CanInterfaceName = Convert.ToString(config["CanInterfaceName"]);
                Language = Convert.ToString(config["Language"]).ToUpper();
                ConvertUnknown = bool.Parse(config["ConvertUnknown"].ToString());
                AutoPolling = bool.Parse(config["AutoPolling"].ToString());
                AutoPollingInterval = Convert.ToInt32(config["AutoPollingInterval"].ToString());
                AutoPollingThrottle = Convert.ToInt32(config["AutoPollingThrottle"].ToString());

                //choose the translator to use and translate the message if translator exists
                switch (CanTranslator)
                {
                    case "StiebelEltron":
                        Translator = new StiebelEltron(LoggerFactory);
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Unable to read config file.");
                return false;
            }
        }

        /// <summary>
        /// Start the Can2Mqtt service.
        /// </summary>
        /// <param name="config">Commandline parameters</param>
        /// <returns></returns>
        /// 
        /*
        public override async Task StartAsync(CancellationToken stoppingToken)
        {
            // Load the config from the config file
            if (!LoadConfig())
            {
                Logger.LogCritical("Unable to load config successfully.");
                return;
            }

            await SetupMqtt();
            SetupAutoPolling(stoppingToken);

            //Start listening on socketcand port
            await TcpCanBusListener(CanServer, CanServerPort);
            AutoPollingTask.Wait();
        }
        */
        public override async Task StartAsync(CancellationToken stoppingToken)
        { // Load the config from the config file
            if (!LoadConfig())
            {
                Logger.LogCritical("Unable to load config successfully.");
                return;
            }
            await SetupMqtt();
            SetupAutoPolling(stoppingToken);

            // Start listening on PCAN
            TPCANHandle canHandle = PCANBasic.PCAN_USBBUS1;
            await PcCanBusListener(canHandle);
            //AutoPollingTask.Wait();
        }


        /// <summary>
        /// Sends a payload to the CAN bus
        /// </summary>
        /// <param name="topic">The MQTT Topic</param>
        /// <param name="payload">The Payload for the CAN bus</param>
        /// <param name="canServer">The CAN Server (where socketcand runs)</param>
        /// <param name="canPort">The CAN Server Port</param>
        /// <returns></returns>
        /// 
        /*
        private async Task SendCan(string topic, byte[] payload, string canServer, int canPort)
        {
            Logger.LogDebug("Sending write request for topic {0}", topic);

            try
            {
                //Get the data
                var data = Encoding.UTF8.GetString(payload);

                //Convert the data to the required format
                var canFrames = Translator.TranslateBack(topic, data, CanSenderId, NoUnit, "0");
                await SendCanFrame(canServer, canPort, canFrames);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to set value via CAN bus.");
            }
        }
        */
        private async Task SendCan(string topic, byte[] payload, TPCANHandle canHandle)
        {
            Logger.LogDebug("Sending write request for topic {0}", topic);

            try
            {
                // Get the data
                var data = Encoding.UTF8.GetString(payload);

                // Convert the data to the required format
                var canFrames = Translator.TranslateBack(topic, data, CanSenderId, NoUnit, "0");
                await SendCanFrame(canHandle, canFrames);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to set value via CAN bus.");
            }
        }


        /// <summary>
        /// Sends a read command to the CAN bus to request the send of the requested value from the bus
        /// </summary>
        /// <param name="topic">The MQTT Topic</param>
        /// <param name="canServer">The CAN Server (where socketcand runs)</param>
        /// <param name="canPort">The CAN Server Port</param>
        /// <returns></returns>
        /// 
        /*
        private async Task ReadCan(string topic, string canServer, int canPort)
        {
            Logger.LogDebug("Sending read request for topic {0}", topic);

            try
            {
                //Convert the data to the required format
                var canFrames = Translator.TranslateBack(topic, null, CanSenderId, NoUnit, "1");
                await SendCanFrame(canServer, canPort, canFrames);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send a read via CAN bus.");
            }
        }
        */
        private async Task ReadCan(string topic, TPCANHandle canHandle)
        {
            Logger.LogDebug("Sending read request for topic {0}", topic);

            try
            {
                // Convert the data to the required format
                var canFrames = Translator.TranslateBack(topic, null, CanSenderId, NoUnit, "1");
                await SendCanFrame(canHandle, canFrames);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send a read via CAN bus.");
            }
        }

        /// <summary>
        /// Sends a CAN frame to the CAN bus
        /// </summary>
        /// <param name="canServer">The CAN Server (where socketcand runs)</param>
        /// <param name="canPort">The CAN Server Port</param>
        /// <param name="canFrame">The actual frame to send</param>
        /// <returns></returns>
        /// 
            /*
            private async Task  SendCanFrame(string canServer, int canPort, IEnumerable<string> canFrames)
            {
                await ConnectTcpCanBus(canServer, canPort);

                foreach (var canFrame in canFrames)
                {
                    //Convert data part of the can Frame to socketcand required format
                    var canFrameDataPart = canFrame.Split("#")[1];
                    var canFrameSdData = "";

                    for (int i = 0; i < canFrameDataPart.Length; i += 2)
                    {
                        canFrameSdData += Convert.ToInt32(canFrameDataPart.Substring(i, 2), 16).ToString("X1") + " ";
                    }
                    canFrameSdData = canFrameSdData.Trim();

                    // < send can_id can_datalength [data]* >
                    var canFrameSdCommand = string.Format("< send {0} {1} {2} >", CanSenderId, canFrameDataPart.Length / 2, canFrameSdData);
                    Logger.LogInformation("Sending CAN Frame: {0}", canFrameSdCommand);
                    TcpCanStream.Write(Encoding.Default.GetBytes(canFrameSdCommand));
                }
            }*/
        private async Task SendCanFrame(TPCANHandle canHandle, IEnumerable<string> canFrames)
        {
            await ConnectPcanBus(canHandle);
            try
            {

                foreach (var canFrame in canFrames)
                {
                    // Convert data part of the can Frame to the required format
                    var canFrameDataPart = canFrame.Split("#")[1];
                    //byte[] data = new byte[canFrameDataPart.Length / 2];

                    for (int i = 0; i < canFrameDataPart.Length; i += 2)
                    {
                        CanSenddata[i / 2] = Convert.ToByte(canFrameDataPart.Substring(i, 2), 16);
                    }

                    TPCANMsg canMsg = new TPCANMsg
                    {
                        ID = Convert.ToUInt32(CanSenderId, 16),
                        LEN = 7,
                        MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
                        DATA = CanSenddata
                    };

                    TPCANStatus status = PCANBasic.Write(canHandle, ref canMsg);

                    if (status != TPCANStatus.PCAN_ERROR_OK)
                    {
                        Logger.LogError("Error sending CAN frame: {0}", status);
                    }
                    else
                    {
                        Logger.LogInformation("Sent CAN Frame: {0}", BitConverter.ToString(CanSenddata));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Exception PCAN sending CAN frame");
            }
        }


        /// <summary>
        /// Connect or verify connection to socketcand
        /// </summary>
        /// <param name="canServer">socketcand server address</param>
        /// <param name="canPort">socketcand server port</param>
        /// <returns></returns>
        /*
        public async Task ConnectTcpCanBus(string canServer, int canPort)
        {
            if (ScdClient != null && ScdClient.Connected)
            {
                Logger.LogTrace("Already connected to SocketCanD.");
                return;
            }

            //Create TCP Client for connection to socketcand (=scd)
            while (ScdClient == null || !ScdClient.Connected)
            {
                try
                {
                    ScdClient = new TcpClient(canServer, canPort);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "FAILED TO CONNECT TO SOCKETCAND {1}. Retry...", canServer);
                }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            Logger.LogInformation("CONNECTED TO SOCKETCAND {0} ON PORT {1}", canServer, canPort);

            //Create TCP Stream to read the CAN Bus Data
            byte[] data = new byte[CanReceiveBufferSize];
            TcpCanStream = ScdClient.GetStream();
            int bytes = TcpCanStream.Read(data, 0, data.Length);


            if (Encoding.Default.GetString(data, 0, bytes) == "< hi >")
            {
                Logger.LogInformation("Handshake successful. Opening CAN interface...");
                TcpCanStream.Write(Encoding.Default.GetBytes("< open " + CanInterfaceName + " >"));

                bytes = TcpCanStream.Read(data, 0, data.Length);
                if (Encoding.Default.GetString(data, 0, bytes) == "< ok >")
                {
                    Logger.LogInformation("Opening connection to slcan0 successful. Changing socketcand mode to raw...");
                    TcpCanStream.Write(Encoding.Default.GetBytes("< rawmode >"));

                    bytes = TcpCanStream.Read(data, 0, data.Length);
                    if (Encoding.Default.GetString(data, 0, bytes) == "< ok >")
                    {
                        Logger.LogInformation("Change to rawmode successful");
                    }
                }
            }
        }
        */

        /// <summary>
        /// Listen to the CAN Bus (via TCP) and generate MQTT Message if there is an update
        /// </summary>
        /// <param name="canServer">socketcand server address</param>
        /// <param name="canPort">socketcand server port</param>
        /// <returns></returns>
        /*
                    public async Task TcpCanBusListener(string canServer, int canPort)
                    {
                        try
                        {
                            byte[] data = new byte[CanReceiveBufferSize];
                            string responseData = string.Empty;
                            var previousData = "";

                            await ConnectTcpCanBus(canServer, canPort);
                            int bytes = TcpCanStream.Read(data, 0, data.Length);

                            //Infinite Loop
                            while (bytes > 0)
                            {
                                //Get the string from the received bytes.
                                responseData = previousData + Encoding.ASCII.GetString(data, 0, bytes);

                                //Each received frame starts with "< frame " and ends with " >".
                                //Check if the current responseData starts with "< frame". If not, drop everything before
                                if (!responseData.StartsWith("< frame "))
                                {
                                    if (responseData.Contains("< frame "))
                                    {
                                        //just take everything starting at "< frame "
                                        Logger.LogWarning("Dropping \"{0}\" because it is not expected at the beginning of a frame.", responseData.Substring(0, responseData.IndexOf("< frame ")));
                                        responseData = responseData.Substring(responseData.IndexOf("< frame "));
                                    }
                                    else
                                    {
                                        //Drop everything
                                        responseData = "";
                                    }
                                }

                                //Check if the responData has a closing " >". If not, save data and go on reading.
                                if (responseData != "" && !responseData.Contains(" >"))
                                {
                                    Logger.LogWarning("No closing tag found. Save data and get next bytes.");
                                    previousData = responseData;
                                    continue;
                                }

                                //As long as full frames exist in responseData
                                while (responseData.Contains(" >"))
                                {
                                    var frame = responseData.Substring(0, responseData.IndexOf(" >") + 2);

                                    //Create the CAN frame
                                    var canFrame = new CanFrame
                                    {
                                        RawFrame = frame
                                    };

                                    Logger.LogInformation("Received CAN Frame: {0}", canFrame.RawFrame);
                                    responseData = responseData.Substring(responseData.IndexOf(" >") + 2);

                                    //If forwarding is disabled for this type of frame, ignore it. Otherwise send the Frame
                                    if (canFrame.CanFrameType == "0" && CanForwardWrite ||
                                        canFrame.CanFrameType == "1" && CanForwardRead ||
                                        canFrame.CanFrameType == "2" && CanForwardResponse)
                                    {
                                        //Sent the CAN frame via MQTT
                                        await SendMQTT(canFrame);
                                    }
                                }

                                //Save data handled at next read
                                previousData = responseData;

                                //Reset byte counter
                                bytes = 0;

                                //Get next packages from canlogserver
                                bytes = TcpCanStream.Read(data, 0, data.Length);
                            }
                            //Close the TCP Stream
                            ScdClient.Close();

                            Logger.LogInformation("Disconnected from canServer {0} Port {1}", canServer, canPort);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error while reading CanBus Server.");
                        }
                        finally
                        {
                            //Reconnect to the canlogserver but do not wait for this here to avoid infinite loops
                            _ = TcpCanBusListener(canServer, canPort); //Reconnect
                        }
                    }
                    */
        public async Task ConnectPcanBus(TPCANHandle canHandle)
        {
            // Check if PCAN is already initialized
            TPCANStatus status = PCANBasic.GetStatus(canHandle);
            if (status == TPCANStatus.PCAN_ERROR_OK)
            {
                Logger.LogTrace("Already connected to PCAN.");
                return;
            }

            // Initialize PCAN-Basic
            //uint canBaudRate = TPCANBaudrate.PCAN_BAUD_500K;
            status = PCANBasic.Initialize(canHandle, TPCANBaudrate.PCAN_BAUD_20K);
            if (status != TPCANStatus.PCAN_ERROR_OK)
            {
                Logger.LogError("FAILED TO CONNECT TO PCAN {0}. Retry...", status);
                await Task.Delay(TimeSpan.FromSeconds(2));
                await ConnectPcanBus(canHandle);
                return;
            }

            Logger.LogInformation("CONNECTED TO PCAN {0}", canHandle);

            // Infinite loop to read CAN bus data
            byte[] data = new byte[8];
            while (true)
            {
                // Wait for CAN frame
                TPCANMsg canMsg = new TPCANMsg();
                TPCANTimestamp canTimestamp = new TPCANTimestamp();
                status = PCANBasic.Read(canHandle, out canMsg, out canTimestamp);

                if (status != TPCANStatus.PCAN_ERROR_OK)
                {
                    Logger.LogError("Error reading CAN message: {0}", status);
                    break;
                }

                string responseData = Encoding.ASCII.GetString(canMsg.DATA, 0, canMsg.LEN);


                Logger.LogInformation("Received CAN Frame: {0}", responseData);
            }

            PCANBasic.Uninitialize(canHandle);
            Logger.LogInformation("Disconnected from PCAN {0}", canHandle);
        }
        /*
                public async Task PcCanBusListener(string canServer, int canPort) //PEAK
            {
                TPCANHandle canHandle = PCANBasic.PCAN_USBBUS1;
                //uint canBaudRate = TPCANBaudrate.PCAN_BAUD_500K;
                byte[] data = new byte[8];
                string responseData = string.Empty;
                var previousData = "";

                try
                {
                    // Initialize PCAN-Basic
                    TPCANStatus status = PCANBasic.Initialize(canHandle, TPCANBaudrate.PCAN_BAUD_20K);
                    if (status != TPCANStatus.PCAN_ERROR_OK)
                    {
                        Logger.LogError("Error initializing PCAN: {0}", status);
                        return;
                    }

                    // Infinite Loop
                    while (true)
                    {
                        // Wait for CAN frame
                        TPCANMsg canMsg = new TPCANMsg();
                        TPCANTimestamp canTimestamp = new TPCANTimestamp();
                        status = PCANBasic.Read(canHandle, out canMsg, out canTimestamp);

                        if (status != TPCANStatus.PCAN_ERROR_OK)
                        {
                            Logger.LogError("Error reading CAN message: {0}", status);
                            break;
                        }

                        responseData = previousData + Encoding.ASCII.GetString(canMsg.DATA, 0, canMsg.LEN);

                        // Each received frame starts with "< frame " and ends with " >".
                        if (!responseData.StartsWith("< frame "))
                        {
                            if (responseData.Contains("< frame "))
                            {
                                Logger.LogWarning("Dropping \"{0}\" because it is not expected at the beginning of a frame.", responseData.Substring(0, responseData.IndexOf("< frame ")));
                                responseData = responseData.Substring(responseData.IndexOf("< frame "));
                            }
                            else
                            {
                                responseData = "";
                            }
                        }

                        if (responseData != "" && !responseData.Contains(" >"))
                        {
                            Logger.LogWarning("No closing tag found. Save data and get next bytes.");
                            previousData = responseData;
                            continue;
                        }

                        while (responseData.Contains(" >"))
                        {
                            var frame = responseData.Substring(0, responseData.IndexOf(" >") + 2);

                            var canFrame = new CanFrame
                            {
                                RawFrame = frame
                            };

                            Logger.LogInformation("Received CAN Frame: {0}", canFrame.RawFrame);
                            responseData = responseData.Substring(responseData.IndexOf(" >") + 2);

                            if ((canFrame.CanFrameType == "0" && CanForwardWrite) ||
                                (canFrame.CanFrameType == "1" && CanForwardRead) ||
                                (canFrame.CanFrameType == "2" && CanForwardResponse))
                            {
                                await SendMQTT(canFrame);
                            }
                        }

                        previousData = responseData;
                    }

                    PCANBasic.Uninitialize(canHandle);
                    Logger.LogInformation("Disconnected from canServer {0} Port {1}", canServer, canPort);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error while reading CanBus Server.");
                }
                finally
                {
                    _ = PcCanBusListener(canServer, canPort); //Reconnect
                }
            }
        */
        /*private async Task PcCanBusListener(TPCANHandle canHandle)
        {
            // Check if PCAN is already initialized
            TPCANStatus status = PCANBasic.GetStatus(canHandle);
            if (status == TPCANStatus.PCAN_ERROR_OK)
            {
                Logger.LogTrace("Already connected to PCAN.");
                return;
            }

            // Initialize PCAN-Basic
            //uint canBaudRate = TPCANBaudrate.PCAN_BAUD_500K;
            status = PCANBasic.Initialize(canHandle, TPCANBaudrate.PCAN_BAUD_20K);
            if (status != TPCANStatus.PCAN_ERROR_OK)
            {
                Logger.LogError("FAILED TO CONNECT TO PCAN {0}. Retry...", status);
                await Task.Delay(TimeSpan.FromSeconds(5));
                await PcCanBusListener(canHandle);
                return;
            }

            Logger.LogInformation("CONNECTED TO PCAN {0}", canHandle);

            // Infinite loop to read CAN bus data
            byte[] data = new byte[8];
            while (true)
            {
                // Wait for CAN frame
                TPCANMsg canMsg = new TPCANMsg();
                TPCANTimestamp canTimestamp = new TPCANTimestamp();
                status = PCANBasic.Read(canHandle, out canMsg, out canTimestamp);

                if (status != TPCANStatus.PCAN_ERROR_OK)
                {
                    Logger.LogError("Error reading CAN message: {0}", status);
                    break;
                }

                string responseData = Encoding.ASCII.GetString(canMsg.DATA, 0, canMsg.LEN);
                Logger.LogInformation("Received CAN Frame: {0}", responseData);
            }

            //PCANBasic.Uninitialize(canHandle);
            //Logger.LogInformation("Disconnected from PCAN {0}", canHandle);
        }
        */

        public async Task PcCanBusListener(TPCANHandle canHandle)
        {
            try
            {
                // Initialize PCAN-Basic
                TPCANStatus status = PCANBasic.Initialize(canHandle, TPCANBaudrate.PCAN_BAUD_20K);
                //TPCANStatus status = PCANBasic.SetValue(canHandle, TPCANParameter. PCAN_PARAMETER_LOOPBACK, TPCANParameter.PCAN_PARAMETER_ON, sizeof(uint));
                if (status != TPCANStatus.PCAN_ERROR_OK)
                {
                    //03032025 die zwei teilen mal raus und schauen ob dann alle Nachrichten ankommen
                    //Logger.LogError("Error initializing PCAN: {0}", status);
                    //return;
                }

                //Logger.LogInformation("CONNECTED TO PCAN {0}", canHandle);

                // Infinite loop to read CAN bus data
                byte[] data = new byte[8];
                string responseData = string.Empty;
                var previousData = "";
                string FrameConverter = "";

                while (true)
                {
                    // Wait for CAN frame
                    TPCANMsg canMsg = new TPCANMsg();
                    TPCANTimestamp canTimestamp = new TPCANTimestamp();
                    status = PCANBasic.Read(canHandle, out canMsg, out canTimestamp);

                    if (status != TPCANStatus.PCAN_ERROR_OK)
                    {
                        //Logger.LogError("Error reading CAN message: {0}", status);
                        //03032025 auch das break hier raus und schauen ob dann alles ankommt
                        continue;
                        //break;
                    }
                    //hier wird aus der Can Message so ein Frame gemacht
                    FrameConverter = BuildCanFrameString(canMsg, canTimestamp);

                    responseData = previousData + FrameConverter;

                    //< frame 6A0 1630437901.513376 3100FA000E0000 >
                    //Aus den der canMsg den Frame bauen

                    //Each received frame starts with "< frame " and ends with " >".
                    if (!responseData.StartsWith("< frame "))
                    {
                        if (responseData.Contains("< frame "))
                        {
                            Logger.LogWarning("Dropping \"{0}\" because it is not expected at the beginning of a frame.", responseData.Substring(0, responseData.IndexOf("< frame ")));
                            responseData = responseData.Substring(responseData.IndexOf("< frame "));
                        }
                        else
                        {
                            responseData = "";
                        }
                    }

                    if (responseData != "" && !responseData.Contains(" >"))
                    {
                        Logger.LogWarning("No closing tag found. Save data and get next bytes.");
                        previousData = responseData;
                        continue;
                    }

                    while (responseData.Contains(" >"))
                    {
                        var frame = responseData.Substring(0, responseData.IndexOf(" >") + 2);

                        var canFrame = new CanFrame
                        {
                            RawFrame = frame
                        };
                        //03032025 dieses if eingebaut da ich im Log ganz viele Frames mit 000 erhalte
                        if (canFrame.PayloadSenderCanId != "000")
                        {
                            Logger.LogInformation("Received CAN Frame: {0}", canFrame.RawFrame);
                        }
                        else
                        {
                            continue;
                        }
                            responseData = responseData.Substring(responseData.IndexOf(" >") + 2);

                        if ((canFrame.CanFrameType == "0" && CanForwardWrite) ||
                            (canFrame.CanFrameType == "1" && CanForwardRead) ||
                            (canFrame.CanFrameType == "2" && CanForwardResponse))
                        {
                            await SendMQTT(canFrame);
                        }
                    }

                    previousData = responseData;
                }

                PCANBasic.Uninitialize(canHandle);
                //Logger.LogInformation("Disconnected from PCAN {0}", canHandle);
            }
            catch (Exception ex)
            {
                //03032025 auch das hier raus
                //Logger.LogError(ex, "Error while reading CAN bus.");
            }
            finally
            {
                // Reconnect to the CAN bus but do not wait for this here to avoid infinite loops
                _ = PcCanBusListener(canHandle); // Reconnect
            }
        }

        string BuildCanFrameString(TPCANMsg canMsg, TPCANTimestamp canTimestamp)
        {
            string idHex = canMsg.ID.ToString("X3");
            //string timestamp = (canTimestamp.micros / 1000000.0).ToString("F6", CultureInfo.InvariantCulture);
            string timestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + "." + DateTimeOffset.Now.ToString("ffffff", CultureInfo.InvariantCulture);
            string data = BitConverter.ToString(canMsg.DATA, 0, canMsg.LEN).Replace("-", "");

            return $"< frame {idHex} {timestamp} {data} >";
        }

        /// <summary>
        /// Sends a CAN-Message as a MQTT message
        /// </summary>
        /// <param name="canMsg"></param>
        /// <returns></returns>
        public async Task SendMQTT(CanFrame canMsg)
        {
            try
            {
                //Check if there is payload
                if (canMsg.RawFrame.Trim().Length == 0)
                    return;

                //Use Translator (if selected)
                if (Translator != null)
                {
                    canMsg = Translator.Translate(canMsg, NoUnit, Language, ConvertUnknown);
                    if (!canMsg.IsComplete)
                    {
                        Logger.LogDebug("Waiting for additional data for MQTT topic {0}", canMsg.MqttTopicExtention);
                        return;
                    }
                }

                //Verify connection to MQTT Broker is established
                while (!MqttClient.IsConnected)
                {
                    Logger.LogInformation("UNHANDLED DISCONNECT FROM MQTT BROKER");
                    while (!MqttClient.IsConnected)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));

                        try
                        {
                            await MqttClient.ConnectAsync(MqttClientOptions);
                            Logger.LogInformation("CONNECTED TO MQTT BROKER");
                        }
                        catch
                        {
                            Logger.LogInformation("RECONNECTING TO MQTT BROKER FAILED. Retrying...");
                        }
                    }
                }

                if (string.IsNullOrEmpty(canMsg.MqttTopicExtention))
                {
                    return;
                }

                //Logoutput with or without translated MQTT message
                if (string.IsNullOrEmpty(canMsg.MqttValue))
                    Logger.LogInformation("Sending MQTT Message: {0} and Topic {1}", canMsg.PayloadFull.Trim(), MqttTopic);
                else
                    Logger.LogInformation("Sending MQTT Message: {0} and Topic {1}{2}", canMsg.MqttValue, MqttTopic, canMsg.MqttTopicExtention);

                //Create MQTT Message
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(MqttTopic + canMsg.MqttTopicExtention)
                    .WithPayload(string.IsNullOrEmpty(canMsg.MqttValue) ? canMsg.PayloadFull : canMsg.MqttValue)
                    .WithExactlyOnceQoS()
                    .WithRetainFlag()
                    .Build();

                //Publish MQTT Message
                await MqttClient.PublishAsync(message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ERROR while sending MQTT message.");
            }
        }

        private async Task SetupMqtt()
        {
            // Create a new MQTT client.
            var mqttFactory = new MqttFactory();
            MqttClient = mqttFactory.CreateMqttClient();

            // Create TCP based options using the builder.
            MqttClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(MqttClientId)
                .WithTcpServer(MqttServer)
                .WithCleanSession()
                .Build();

            // If authentication at the MQTT broker is enabled, create the options with credentials
            if (!string.IsNullOrEmpty(MqttUser) && MqttPassword != null)
            {
                Logger.LogInformation("Connecting to MQTT broker using Credentials...");
                MqttClientOptions = new MqttClientOptionsBuilder()
                   .WithClientId(MqttClientId)
                   .WithTcpServer(MqttServer)
                   .WithCredentials(MqttUser, MqttPassword)
                   .WithCleanSession()
                   .Build();
            }

            //Handle reconnect on lost connection to MQTT Server
            MqttClient.UseDisconnectedHandler(async e =>
            {
                Logger.LogWarning("DISCONNECTED FROM MQTT BROKER {0} because of {1}", MqttServer, e.Reason);
                while (!MqttClient.IsConnected)
                {
                    try
                    {
                        // Connect the MQTT Client
                        await MqttClient.ConnectAsync(MqttClientOptions);
                        if (MqttClient.IsConnected)
                            Logger.LogInformation("CONNECTED TO MQTT BROKER {0} using ClientId {1}", MqttServer, MqttClientId);
                        else
                            Logger.LogInformation("CONNECTION TO MQTT BROKER {0} using ClientId {1} FAILED", MqttServer, MqttClientId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInformation("RECONNECTING TO MQTT BROKER {0} FAILED. Exception: {1}", MqttServer, ex.ToString());
                        Thread.Sleep(10000); //Wait 10 seconds
                    }
                }
            });

            // Connect the MQTT Client to the MQTT Broker
            await MqttClient.ConnectAsync(MqttClientOptions);
            if (MqttClient.IsConnected)
                Logger.LogInformation("CONNECTION TO MQTT BROKER {0} established using ClientId {1}", MqttServer, MqttClientId);

            // Only accept set commands, if they are enabled.
            if (MqttAcceptSet)
            {
                //Create listener on MQTT Broker to accept all messages with the MqttTopic from the config.
                await MqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptionsBuilder().WithTopicFilter(MqttTopic + "/#").Build());
                MqttClient.UseApplicationMessageReceivedHandler(async e =>
                {
                    // Check if it is a set topic and handle only if so.
                    if (e.ApplicationMessage.Topic.EndsWith("/set"))
                    {
                        Console.Write("Received MQTT SET Message; Topic = {0}", e.ApplicationMessage.Topic);
                        if (e.ApplicationMessage.Payload != null)
                        {
                            Logger.LogInformation($" and Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                            await SendCan(e.ApplicationMessage.Topic, e.ApplicationMessage.Payload, canHandle);
                        }
                        else
                        {
                            Logger.LogInformation(" WITH NO PAYLOAD");
                        }
                    }
                    // Check if it is a read topic. If yes, send a READ via CAN bus for the corresponding value to trigger a send of the value via CAN bus
                    else if (e.ApplicationMessage.Topic.EndsWith("/read"))
                    {
                        Console.Write("Received MQTT READ Message; Topic = {0}", e.ApplicationMessage.Topic);
                        if (e.ApplicationMessage.Topic != null)
                        {
                            Logger.LogInformation("");
                            await ReadCan(e.ApplicationMessage.Topic, canHandle);
                        }
                        else
                        {
                            Logger.LogInformation(" WITH NO TOPIC");
                        }
                    }
                });
            }
        }

        void SetupAutoPolling(CancellationToken stoppingToken)
        {
            if (!AutoPolling)
            {
                return;
            }

            AutoPollingTask = Task.Run(async () =>
            {
                if (Translator == null)
                {
                    Logger.LogWarning("Nothing to poll - no translator selected.");
                    return;
                }

                if (!Translator.MqttTopicsToPoll.Any())
                {
                    Logger.LogWarning("Nothing to poll - no MQTT topics to poll specified (or all are ignored).");
                    return;
                }

                var delay = TimeSpan.FromSeconds(AutoPollingInterval);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(delay, stoppingToken);

                    foreach (var mqttTopic in Translator.MqttTopicsToPoll)
                    {
                        await ReadCan(mqttTopic + "/read", canHandle);
                        // delay next read to avoid overloading socketcand
                        await Task.Delay(AutoPollingThrottle, stoppingToken);
                    }
                }
            });
        }
    }
}
