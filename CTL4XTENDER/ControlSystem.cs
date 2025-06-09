using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;         	// For Generic Device Support
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

Set Relay State:
{
  "C4X": "<random id>",
  "t": "srly",
  "p": "RLY1", // Relay name
  "s": true // true for on, false for off
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
 * 
 * 
 * 
 * 
 * 
 * 
 */
namespace CTL4XTENDER
{
    public class ControlSystem : CrestronControlSystem
    {
        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>


        private bool debugMode { get; set; } = true;
        private Codec c;
        private List<SerialPort> serialPorts = new List<SerialPort>();
        private List<IRSerialPort> irPorts = new List<IRSerialPort>();

        public ControlSystem()
            : base()
        {
            try {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(_ControllerEthernetEventHandler);
            }
            catch (Exception e) {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        /// 

        public void debug(string text, bool forceDisplay = false)
        {
            if (forceDisplay || debugMode) {
                CrestronConsole.PrintLine(text);
            }
        }

        public void InitializeRelays()
        {
            foreach (Relay r in RelayPorts) {
                //CrestronConsole.PrintLine("C4X: Initializing Relay {0}", r.ID);
                r.Register();
            }
        }

        public void InitializeSerialPorts()
        {
            foreach (ComPort c in this.ComPorts) {
                if (c.ID > 1) {
                    //CrestronConsole.PrintLine("C4X: Initializing Serial Port {0}", c.ID);
                    SerialPort tempSerialPort = new SerialPort(this, c.ID);
                    serialPorts.Add(tempSerialPort);
                    tempSerialPort.SerialDataReceived += P_SerialDataReceived;  

                }

            }
            
        }

        private void P_SerialDataReceived(ComPort ReceivingComPort, JObject json)
        {
            // Strip double quotes, newlines, and carriage returns from the data string
            string data = json["data"]?.ToString() ?? string.Empty;
            /*
            data = data.Replace("\"", string.Empty)
                       .Replace("\n", string.Empty)
                       .Replace("\r", string.Empty);
            */

            JObject responseJson = new JObject
            {
                { "C4X", "" }, // Echo the C4X ID from the request
                { "t", "crcv" },        // Type of request
                { "p", ReceivingComPort.ID }, // Port name
                { "d", data } // Data received with newlines/carriage returns and double quotes stripped
            };
            c.SendJsonMessage(responseJson); // Send error response
        }

        public void InitializeIrPorts()
        {
            foreach (IROutputPort ir in this.IROutputPorts) {
                if (ir.ID > 0) {
                    //CrestronConsole.PrintLine("C4X: Initializing IR Port {0}", ir.ID);
                    irPorts.Add(new IRSerialPort(this, ir.ID));
                }
            }
        }

        public override void InitializeSystem()
        {
            try {
                string logo = @"
                                                            
                      ,ad8888ba,         ,d8  8b        d8  
                     d8""'    `""8b      ,d888   Y8,    ,8P   
                    d8'              ,d8"" 88    `8b  d8'    
                    88             ,d8""   88      Y88P      
                    88           ,d8""     88      d88b      
                    Y8,          8888888888888  ,8P  Y8,    
                     Y8a.    .a8P         88   d8'    `8b   
                      `""Y8888Y""'          88  8P        Y8

                     Control 4 Port Extender for Cisco Codecs                    
                                                                     
";
                CrestronConsole.PrintLine(logo);
                c = new Codec(this, 1);
                c.InitRequestReceived += C_InitRequestReceived;
                c.GetComPortListReceived += C_GetComPortListReceived;
                c.ConfigureComPortReceived += C_ConfigureComPortReceived;
                c.ComPortSendDataReceived += C_ComPortSendDataReceived;
                c.ConfigureIrPortReceived += C_ConfigureIrPortReceived;
                c.IrPortSendDataReceived += C_IrPortSendDataReceived;
                c.RelayStateReceived += C_RelayStateReceived;
                c.RebootReceived += C_RebootReceived;
                c.PrintLineReceived += C_PrintLineReceived;

                InitializeRelays();
                InitializeSerialPorts();
                InitializeIrPorts();

                //CrestronConsole.PrintLine("C4X: Codec initialized.");
                //CrestronConsole.PrintLine("C4X: Sending init notify.");
                c.sendInitNotify("");
            }
            catch (Exception e) {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        private void C_PrintLineReceived(object sender, string data)
        {
            CrestronConsole.PrintLine(data);
        }

        private void C_RebootReceived(object sender)
        {
            string response = string.Empty;
            CrestronConsole.SendControlSystemCommand("reboot", ref response);
        }

        private void C_RelayStateReceived(object sender, JObject json)
        {
            //CrestronConsole.PrintLine("C4X: RelayStateReceived called with JSON: " + json.ToString());
            try {
                uint relayId = (uint)json["i"];
                RelayPorts[relayId].State = json["s"].ToObject<bool>();
            }
            catch (Exception e) {
                //CrestronConsole.PrintLine("Error processing relay state: {0}", e.Message);
            }
        }

        private void C_IrPortSendDataReceived(object sender, JObject json)
        {
            // Extract port ID and data from JSON and send data to the specified IRSerialPort
            var portValue = json["p"]?.ToString();
            foreach (var irp in irPorts) {
                // Allow matching "IR<number>", "ir<number>", or just "<number>"
                if (
                    string.Equals($"IR{irp.Id}", portValue, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{irp.Id}", portValue, StringComparison.OrdinalIgnoreCase)
                ) {
                    // The data may contain newlines and carriage returns, so preserve as-is
                    string data = json["d"]?.ToString() ?? string.Empty;
                    //CrestronConsole.PrintLine($"Sending data to IRSerialPort {irp.Id}: {data}");
                    irp.SendData(data);
                    return;
                }
            }
        }

        private void C_ComPortSendDataReceived(object sender, JObject json)
        {
            //CrestronConsole.PrintLine("C4X: ComPortSendDataReceived called with JSON: " + json.ToString());
            // Extract port ID and data from JSON and send data to the specified SerialPort
            var portValue = json["p"]?.ToString();
            foreach (var sp in serialPorts) {
                // Allow matching "COM<number>", "com<number>", or just "<number>"
                if (
                    string.Equals($"COM{sp.Id}", portValue, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{sp.Id}", portValue, StringComparison.OrdinalIgnoreCase)
                ) {
                    // The data may contain newlines and carriage returns, so preserve as-is
                    string data = json["d"]?.ToString() ?? string.Empty;
                    //CrestronConsole.PrintLine($"Sending data to SerialPort {sp.Id}: {data}");
                    sp.SendData(data);
                    return;
                }
            }
        }

        private void C_ConfigureComPortReceived(object sender, JObject json)
        {
            //CrestronConsole.PrintLine("Configuring ComPort with JSON: " + json.ToString());
            // Extract port ID, baud rate, and settings from JSON
            var portName = json["p"]?.ToString();
            var baudRate = json["b"] != null ? (int)json["b"] : 9600;
            var settings = json["d"]?.ToString() ?? "8N1";

            // Find the SerialPort with matching Id (allow "COM1", "com1", or just "1")
            SerialPort targetPort = null;
            foreach (var sp in serialPorts) {
                if (
                    string.Equals($"COM{sp.Id}", portName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{sp.Id}", portName, StringComparison.OrdinalIgnoreCase)
                ) {
                    targetPort = sp;
                    break;
                }
            }

            if (targetPort != null) {
                targetPort.Configure(baudRate, settings);
                //CrestronConsole.PrintLine($"SerialPort {portName} configured: {baudRate}, {settings}");
                JObject responseJson = new JObject
                {
                    { "C4X", json["C4X"] }, // Echo the C4X ID from the request
                    { "t", "crok" }         // Type of request
                };
                c.SendJsonMessage(responseJson); // Assuming Codec has a method to send the response
            }
            else {
                //CrestronConsole.PrintLine($"SerialPort {portName} not found.");
                JObject responseJson = new JObject
                {
                    { "C4X", json["C4X"] }, // Echo the C4X ID from the request
                    { "t", "crer" },        // Type of request
                    { "e", $"SerialPort {portName} not found." } // Error message
                };
                c.SendJsonMessage(responseJson); // Send error response
            }
        }

        private void C_ConfigureIrPortReceived(object sender, JObject json)
        {
            //CrestronConsole.PrintLine("Configuring IrPort with JSON: " + json.ToString());
            // Extract port ID, baud rate, and settings from JSON
            var portName = json["p"]?.ToString();
            var baudRate = json["b"] != null ? (int)json["b"] : 9600;
            var settings = json["d"]?.ToString() ?? "8N1";

            IRSerialPort targetPort = null;
            foreach (var irp in irPorts) {
                if (
                    string.Equals($"IR{irp.Id}", portName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{irp.Id}", portName, StringComparison.OrdinalIgnoreCase)
                ) {
                    targetPort = irp;
                    break;
                }
            }

            if (targetPort != null) {
                targetPort.Configure(baudRate, settings);
                //CrestronConsole.PrintLine($"IRSerialPort {portName} configured: {baudRate}, {settings}");
                JObject responseJson = new JObject
                {
                    { "C4X", json["C4X"] }, // Echo the C4X ID from the request
                    { "t", "irok" },        // Type of request
                    { "s", "ok" }
                };
                c.SendJsonMessage(responseJson); // Assuming Codec has a method to send the response
            }
            else {
                //CrestronConsole.PrintLine($"IRSerialPort {portName} not found.");
                JObject responseJson = new JObject
                {
                    { "C4X", json["C4X"] }, // Echo the C4X ID from the request
                    { "t", "irer" },        // Type of request
                    { "e", $"IRSerialPort {portName} not found." } // Error message
                };
                c.SendJsonMessage(responseJson); // Send error response
            }
        }
        private void C_GetComPortListReceived(object sender, JObject json)
        {
            //CrestronConsole.PrintLine("ControlSystem: GetComPortListReceived called with JSON: " + json.ToString());
            /*Here we take the serialPorts array of string and we make a json reply that looks like this:
             * {
                  "C4X": "<id from get ComPort list request>",
                  "t": "cget",
                  "p": ["com1", "com2", "com3", ....]
               }
             * 
             * 
             */
            JArray comPortsArray = new JArray();
            foreach (SerialPort sp in serialPorts) {
                comPortsArray.Add(sp.Id); // Assuming SerialPort has a property Id that returns the port name
            }
            JObject responseJson = new JObject
            {
                { "C4X", json["C4X"] }, // Echo the C4X ID from the request
                { "t", "cger" },        // Type of request
                { "p", comPortsArray }  // List of COM ports
            };
            //CrestronConsole.PrintLine("ControlSystem: Sending ComPort list: " + responseJson.ToString());
            c.SendJsonMessage(responseJson); // Assuming Codec has a method to send the ComPort list
        }

        private void C_InitRequestReceived(object sender, Newtonsoft.Json.Linq.JObject json)
        {
            //debug("ControlSystem: InitRequestReceived called with JSON: " + json.ToString());
        }


        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void _ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType) {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter) {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter) {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void _ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType) {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void _ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType) {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }
    }
}