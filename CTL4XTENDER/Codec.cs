using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DM;
using System;
using System.Collections.Generic;
using System.Timers;


/*PROTOCOL DOCUMENTATION
 * 
 * === From codec to crestron processor ===
init request:
{
  "C4X": "<random id>",
  "t": "init"
}

configure ComPort:
{
  "C4X": "<random id>",
  "t": "ccom",
  "p": "COM1",
  "b": 115200,
  "d": "8N1""
}

configure IrPort:
{
  "C4X": "<random id>",
  "t": "cirp",
  "p": "IR1",
  "b": 9600,
  "d": "8N1"
}

ComPort send data:
{
  "C4X": "<random id>",
  "t": "csnd",
  "p": "COM1",
  "d": "<data to send>"
}

Get ComPort list:
{
  "C4X": "<random id>",
  "t": "cget"
}

IrPort send data:
{
  "C4X": "<random id>",
  "t": "isnd",
  "p": "IR1",
  "d": "<data to send>"
}

Crestron Console PrintLine command:
{
  "C4X": "<random id>",
  "t": "cprn",
  "d": "<data to print>"
}


=== From crestron processor to codec ===
init notify
{
  "C4X": "<id from init request>",
  "t": "inot",
  "v": "1.0.0"
}

configure ComPort response (OK):
{
  "C4X": "<id from configure ComPort request>",
  "t": "crok",
}

configure ComPort response (Error):
{
  "C4X": "<id from configure ComPort request>",
  "t": "crer",
  "e": "Error message here"
}

get comport list response:
{
  "C4X": "<id from get ComPort list request>",
  "t": "cger",
  "p": ["com1", "com2", "com3", ....]
}

configure IrPort response (OK):
{
  "C4X": "<id from configure IrPort request>",
  "t": "irok",
  "s": "ok"
}

configure IrPort response (Error):
{
  "C4X": "<id from configure IrPort request>",
  "t": "irer",
  "e": "Error message here"
}

ComPort data received:
{
  "C4X": "<random id>",
  "t": "crcv",
  "p": "COM1",
  "d": "<data received>"
}

IrPort data received:
{
  "C4X": "<random id>",
  "t": "ircv",
  "p": "IR1",
  "d": "<data received>"
}

Set Relay State:
{
  "C4X": "<random id>",
  "t": "srly",
  "p": "RLY1", // Relay name
  "s": true // true for on, false for off
}
 * 
 * 
 * 
 * 
 * 
 * 
 */


namespace CTL4XTENDER
{
    public delegate void InitRequestEventHandler(object sender, Newtonsoft.Json.Linq.JObject json);
    public delegate void GetComPortListEventHandler(object sender, Newtonsoft.Json.Linq.JObject json);
    public delegate void ConfigureComPortEventHandler(object sender, Newtonsoft.Json.Linq.JObject json);
    public delegate void ComPortSendDataEventHandler(object sender, Newtonsoft.Json.Linq.JObject json);
    public delegate void ConfigureIrPortEventHandler(object sender, Newtonsoft.Json.Linq.JObject json);
    public delegate void IrPortSendDataEventHandler(object sender, Newtonsoft.Json.Linq.JObject json);
    public delegate void RelayStateEventHandler(object sender, Newtonsoft.Json.Linq.JObject json);
    public delegate void PrintLineEventHandler(object sender, string data);
    public delegate void RebootEventHandler(object sender);


    public class Codec
    {
        private Timer heartBeatTimer;
        private ControlSystem controlSystem;
        private ComPort codec;
        private string serialBuffer = string.Empty;

        //Events
        public event InitRequestEventHandler InitRequestReceived;
        public event GetComPortListEventHandler GetComPortListReceived;
        public event ConfigureComPortEventHandler ConfigureComPortReceived;
        public event ComPortSendDataEventHandler ComPortSendDataReceived;
        public event ConfigureIrPortEventHandler ConfigureIrPortReceived;
        public event IrPortSendDataEventHandler IrPortSendDataReceived;
        public event RelayStateEventHandler RelayStateReceived;
        public event RebootEventHandler RebootReceived;
        public event PrintLineEventHandler PrintLineReceived;

        public Codec(ControlSystem ctlSystem, uint comport)
        {
            controlSystem = ctlSystem;
            //controlSystem.debug("Codec: starting", true);


            //controlSystem.debug("Codec: creating timers", true);
            heartBeatTimer = new Timer();
            heartBeatTimer.Elapsed += HeartBeatTimer_Elapsed;
            heartBeatTimer.AutoReset = true;
            heartBeatTimer.Interval = 30000;



            codec = controlSystem.ComPorts[comport];



            //controlSystem.debug($"Codec: registering comport {comport}", true);
            codec.Register();

            //
            //controlSystem.debug($"Codec: configuring comport {comport}", true);
            codec.SetComPortSpec(
                ComPort.eComBaudRates.ComspecBaudRate115200,
                ComPort.eComDataBits.ComspecDataBits8,
                ComPort.eComParityType.ComspecParityNone,
                ComPort.eComStopBits.ComspecStopBits1,
                ComPort.eComProtocolType.ComspecProtocolRS232,
                ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                false);

            //controlSystem.debug("Codec: registering data received event", true);
            codec.SerialDataReceived += Codec_SerialDataReceived;

            Start();
        }


        public void sendInitNotify(string id)
        {
            // This function sends an init notify message.
            // If the id is null, the value sent for "C4X" is "".

            string c4xValue = id ?? "";
            var json = new Newtonsoft.Json.Linq.JObject
            {
                ["C4X"] = c4xValue,
                ["t"] = "inot",
                ["v"] = "1.0.0",
                ["s"] = CrestronEnvironment.SystemInfo.SerialNumber,
                ["r"] = CrestronEnvironment.MemoryInfo.TotalRamSize,
                ["f"] = CrestronEnvironment.MemoryInfo.RamFree
            };

            string message = json.ToString(Newtonsoft.Json.Formatting.None);
            //controlSystem.debug($"Codec: sending init notify: {message}");
            sendMessage(message);
        }

        public void SendComPortList(Newtonsoft.Json.Linq.JObject json)
        {
            // This function sends a ComPort list message.
            // The json parameter should contain the C4X ID and the list of ComPorts.
            if (json == null) return;
            string message = json.ToString(Newtonsoft.Json.Formatting.None);
            //controlSystem.debug($"Codec: sending ComPort list: {message}");
            sendMessage(message);
        }

        public void SendJsonMessage(Newtonsoft.Json.Linq.JObject json)
        {
            //CrestronConsole.PrintLine("Codec: sending JSON message: {0}", json.ToString(Newtonsoft.Json.Formatting.None));
            // This function sends a JSON message.
            if (json == null) return;
            string message = json.ToString(Newtonsoft.Json.Formatting.None);
            //controlSystem.debug($"Codec: sending JSON message: {message}");
            sendMessage(message);
        }

        private void Codec_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            // Append new data to buffer
            serialBuffer += args.SerialData;

            int newLineIndex;
            // Process all complete lines
            while ((newLineIndex = serialBuffer.IndexOf('\n')) != -1) {
                // Extract the line (remove \r if present)
                string line = serialBuffer.Substring(0, newLineIndex).TrimEnd('\r');
                //CrestronConsole.PrintLine("Codec: received line: {0}", line);
                if (line.StartsWith("*e Message Send Text:")) {
                    int quoteStart = line.IndexOf('\"');
                    int quoteEnd = line.LastIndexOf('\"');

                    if (quoteStart != -1 && quoteEnd > quoteStart) {
                        string messageText = line.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);

                        // Try to parse as JSON and check for C4X and t fields
                        // messageText is base64 encoded, decode it
                        string decodedMessageText;
                        try {
                            byte[] data = Convert.FromBase64String(messageText);
                            decodedMessageText = System.Text.Encoding.UTF8.GetString(data);
                        }
                        catch (FormatException ex) {
                            //controlSystem.debug($"Codec: failed to decode base64 message: {ex.Message}");
                            // Skip processing this line if decoding fails
                            serialBuffer = serialBuffer.Substring(newLineIndex + 1);
                            continue;
                        }

                        //controlSystem.debug($"Codec: received message: {decodedMessageText}");

                        // Try to parse as JSON and check for C4X and t fields
                        try {
                            // Fix: Use JsonConvert to properly unescape JSON string, so escaped quotes in "d" are handled
                            var json = Newtonsoft.Json.Linq.JObject.Parse(
                                Newtonsoft.Json.JsonConvert.DeserializeObject<string>($"\"{decodedMessageText.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"")
                            );
                            var c4xToken = json["C4X"];
                            var typeToken = json["t"];
                            //CrestronConsole.PrintLine("Codec: received message: {0}", json.ToString(Newtonsoft.Json.Formatting.None));
                            if (c4xToken != null && typeToken != null && typeToken.Type == Newtonsoft.Json.Linq.JTokenType.String) {
                                string type = typeToken.ToString();
                                //controlSystem.debug($"Codec: message is for this system, type: {type}");

                                switch (type) {
                                    case "init":
                                        sendInitNotify(c4xToken.ToString());
                                        InitRequestReceived?.Invoke(this, json);
                                        break;
                                    case "cprn":
                                        string dataToPrint = json["d"]?.ToString() ?? "No data to print";
                                        PrintLineReceived?.Invoke(this, dataToPrint);
                                        break;
                                    case "cget":
                                        GetComPortListReceived?.Invoke(this, json);
                                        break;
                                    case "ccom":
                                        ConfigureComPortReceived?.Invoke(this, json);
                                        break;
                                    case "csnd":    
                                        //controlSystem.debug("Codec: ComPort send data request received, raising event");
                                        ComPortSendDataReceived?.Invoke(this, json);
                                        break;
                                    case "cirp":
                                        ConfigureIrPortReceived?.Invoke(this, json);
                                        break;
                                    case "isnd":
                                        IrPortSendDataReceived?.Invoke(this, json);
                                        break;
                                    case "srly":
                                        RelayStateReceived?.Invoke(this, json);
                                        break;
                                    case "boot":
                                        RebootReceived?.Invoke(this);
                                        break;
                                    default:
                                        //CrestronConsole.PrintLine("Codec: Unknown message type: {0}", type);
                                        break;
                                }
                            }
                        }
                        catch {
                            // Fallback: try to parse as-is (legacy behavior)
                            try {
                                var json = Newtonsoft.Json.Linq.JObject.Parse(decodedMessageText);
                                var c4xToken = json["C4X"];
                                var typeToken = json["t"];
                                if (c4xToken != null && typeToken != null && typeToken.Type == Newtonsoft.Json.Linq.JTokenType.String) {
                                    string type = typeToken.ToString();
                                    switch (type) {
                                        case "init":
                                            sendInitNotify(c4xToken.ToString());
                                            InitRequestReceived?.Invoke(this, json);
                                            break;
                                        case "cprn":
                                            break;
                                        case "cget":
                                            GetComPortListReceived?.Invoke(this, json);
                                            break;
                                        case "ccom":
                                            ConfigureComPortReceived?.Invoke(this, json);
                                            break;
                                        case "csnd":
                                            //controlSystem.debug("Codec: ComPort send data request received, raising event");
                                            ComPortSendDataReceived?.Invoke(this, json);
                                            break;
                                        case "cirp":
                                            ConfigureIrPortReceived?.Invoke(this, json);
                                            break;
                                        case "isnd":
                                            IrPortSendDataReceived?.Invoke(this, json);
                                            break;
                                        case "srly":
                                            RelayStateReceived?.Invoke(this, json);
                                            break;
                                        case "boot":
                                            RebootReceived?.Invoke(this);
                                            break;
                                        default:
                                            //CrestronConsole.PrintLine("Codec: Unknown message type: {0}", type);
                                            break;
                                    }
                                }
                            }
                            catch {
                                // Ignore invalid JSON messages silently
                            }
                        }
                    }
                }

                // Remove processed line from buffer
                serialBuffer = serialBuffer.Substring(newLineIndex + 1);
            }
        }

        private void serialSend(string data)
        {
            //CrestronConsole.PrintLine("Codec: sending data: {0}", data);
            codec.Send(data + "\r\n");
        }

        public void sendMessage(string message)
        {
            //CrestronConsole.PrintLine("Codec: sending message: {0}", message);

            // Base64 encode the message
            string base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message));
            serialSend("xcommand message send text:\"" + base64Message + "\"\r\n");
        }


        private void sendHeartBeat()
        {
            // Use the correct type for controlSystem to access debug()
            //controlSystem.debug("Codec: sending heartbeat");
            string connectString = "xCommand Peripherals Connect ID:C4XTENDER Name:C4XTENDER SoftwareInfo:C4XTENDER-dev SerialNumber:C4XTENDER Type:ControlSystem";
            string heartBeatString = "xCommand Peripherals HeartBeat ID:C4XTENDER Timeout:120";
            string registerString = "xFeedback register /event/message";
            serialSend(connectString);
            serialSend(heartBeatString);
            serialSend(registerString);
        }

        public void Start()
        {
            sendHeartBeat();

            heartBeatTimer.Start();


        }

        private void HeartBeatTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            sendHeartBeat();
        }
    }
}
