using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTL4XTENDER
{

    public delegate void ComPortSerialDataReceivedEventHandler(ComPort ReceivingComPort, Newtonsoft.Json.Linq.JObject json);
    public class SerialPort
    {
        public event ComPortSerialDataReceivedEventHandler SerialDataReceived;
        private ControlSystem _controlSystem { get; set; }
        public uint Id { get; set; } = 0;
        private ComPort _comPort { get; set; } = null;

        public SerialPort(ControlSystem controlSystem, uint id)
        {
            _controlSystem = controlSystem;
            _comPort = _controlSystem.ComPorts[id];
            Id = id;
            //CrestronConsole.PrintLine("SerialPort created with ID: {0}", Id);
            _comPort.SerialDataReceived += _comPort_SerialDataReceived;
        }

        private void _comPort_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            //Raise event with the received data
            //CrestronConsole.PrintLine("Serial data received on SerialPort with ID: {0}, Data: |{1}|", Id, args.SerialData);
            try {
                // Create a JObject to hold the data
                var json = new Newtonsoft.Json.Linq.JObject();
                json["id"] = Id;
                json["data"] = args.SerialData;
                // Raise the event
                SerialDataReceived?.Invoke(ReceivingComPort, json);
            }
            catch (Exception ex) {
                CrestronConsole.PrintLine("Error processing received serial data: {0}", ex.Message);
            }
        }

        public void Configure(int baudRate, string settings)
        {
            //CrestronConsole.PrintLine("Configuring SerialPort with ID: {0}, BaudRate: {1}, Settings: {2}", Id, baudRate, settings);
            try {
                _comPort.Register();

                /*
                 * Example of setting the ComPortSpec directly with hardcoded values:
                _comPort.SetComPortSpec(
                    ComPort.eComBaudRates.ComspecBaudRate38400,
                    ComPort.eComDataBits.ComspecDataBits8,
                    ComPort.eComParityType.ComspecParityEven,
                    ComPort.eComStopBits.ComspecStopBits1,
                    ComPort.eComProtocolType.ComspecProtocolRS232,
                    ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                    ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                    false);

                */

                /*Now we implement the actual configuration, but we need to convert the settings to the appropriate types.
                 * for example, if settings are: 9066 for the first argument and 8N1 for the second:
                 * 9600 is the baud rate
                 * 8 is the data bits
                 * N is no parity
                 * 1 is the stop bits
                 * 
                 * */

                _comPort.SetComPortSpec(
                    (ComPort.eComBaudRates)baudRate, // Assuming baudRate is an enum value
                    ParseDataBits(settings), // Use parsed data bits
                    ParseParity(settings), // Use parsed parity
                    ParseStopBits(settings), // Assuming 1 stop bit
                    ComPort.eComProtocolType.ComspecProtocolRS232, // Assuming RS232 protocol
                    ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone, // No hardware handshake
                    ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone, // No software handshake
                    false); // Not using flow control



            }
            catch (Exception ex) {

            }
        }

        private ComPort.eComDataBits ParseDataBits(string settings)
        {
            // settings is expected to be in the form "8N1" (DataBits, Parity, StopBits)
            if (string.IsNullOrEmpty(settings) || settings.Length < 1)
                return ComPort.eComDataBits.ComspecDataBits8;

            switch (settings[0]) {
                case '7':
                    return ComPort.eComDataBits.ComspecDataBits7;
                case '8':
                    return ComPort.eComDataBits.ComspecDataBits8;
                default:
                    return ComPort.eComDataBits.ComspecDataBits8;
            }
        }

        private ComPort.eComStopBits ParseStopBits(string settings)
        {
            // settings is expected to be in the form "8N1" (DataBits, Parity, StopBits)
            if (string.IsNullOrEmpty(settings) || settings.Length < 3)
                return ComPort.eComStopBits.ComspecStopBits1;
            switch (settings[2]) {
                case '1':
                    return ComPort.eComStopBits.ComspecStopBits1;
                case '2':
                    return ComPort.eComStopBits.ComspecStopBits2;
                default:
                    return ComPort.eComStopBits.ComspecStopBits1;
            }
        }
        private ComPort.eComParityType ParseParity(string settings)
        {
            // settings is expected to be in the form "8N1" (DataBits, Parity, StopBits)
            if (string.IsNullOrEmpty(settings) || settings.Length < 2)
                return ComPort.eComParityType.ComspecParityNone;

            char parityChar = char.ToUpper(settings[1]);
            switch (parityChar) {
                case 'N':
                    return ComPort.eComParityType.ComspecParityNone;
                case 'E':
                    return ComPort.eComParityType.ComspecParityEven;
                case 'O':
                    return ComPort.eComParityType.ComspecParityOdd;
                default:
                    return ComPort.eComParityType.ComspecParityNone;
            }
        }

        public void SendData(string data)
        {
            //CrestronConsole.PrintLine("Sending serial data");
            //CrestronConsole.PrintLine("Sending data on SerialPort with ID: {0}, Data: |{1}|", Id, data);
            try {
                // Unescape any escaped double quotes in the data
                string unescapedData = data.Replace("\\\"", "\"");
                //CrestronConsole.PrintLine("unescaped with ID: {0}, Data: |{1}|", Id, unescapedData);
                _comPort.Send(unescapedData);
            }
            catch (Exception ex) {
                CrestronConsole.PrintLine("Error sending data: {0}", ex.Message);
            }
        }

    }
}
