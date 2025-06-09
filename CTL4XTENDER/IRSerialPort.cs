using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.EthernetCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTL4XTENDER
{
    public class IRSerialPort
    {
        private ControlSystem _controlSystem { get; set; }
        public uint Id { get; set; } = 0;
        private IROutputPort _irPort { get; set; } = null;

        public IRSerialPort(ControlSystem controlSystem, uint id)
        {
            _controlSystem = controlSystem;
            _irPort = _controlSystem.IROutputPorts[id];
            Id = id;
            //CrestronConsole.PrintLine("SerialPort created with ID: {0}", Id);
        }

        public void Configure(int baudRate, string settings)
        {
            //CrestronConsole.PrintLine("Configuring SerialPort with ID: {0}, BaudRate: {1}, Settings: {2}", Id, baudRate, settings);


            /*
                We need to adapt this code to support the IRSerialPort class.
            Like I did with another project:
                        IrPort.SetIRSerialSpec(
            eIRSerialBaudRates.ComspecBaudRate38400,
            eIRSerialDataBits.ComspecDataBits8,
            eIRSerialParityType.ComspecParityEven,
            eIRSerialStopBits.ComspecStopBits1,
            Encoding.ASCII
        );

            */

            /*Now we implement the actual configuration, but we need to convert the settings to the appropriate types.
             * for example, if settings are: 9066 for the first argument and 8N1 for the second:
             * 9600 is the baud rate
             * 8 is the data bits
             * N is no parity
             * 1 is the stop bits
             * 
             * */

            /*
             * Example of setting the values for a comport, but this is not applicable for IRSerialPort:
            _comPort.SetComPortSpec(
                (ComPort.eComBaudRates)baudRate, // Assuming baudRate is an enum value
                ParseDataBits(settings), // Use parsed data bits
                ParseParity(settings), // Use parsed parity
                ParseStopBits(settings), // Assuming 1 stop bit
                ComPort.eComProtocolType.ComspecProtocolRS232, // Assuming RS232 protocol
                ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone, // No hardware handshake
                ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone, // No software handshake
                false); // Not using flow control

            */

            //Init the port with the parsed values
            _irPort.SetIRSerialSpec(
                ParseIRBaudRate(baudRate), // Convert baud rate to eIRSerialBaudRates
                ParseIRDataBits(settings), // Convert data bits from settings
                ParseIRParity(settings), // Convert parity from settings
                ParseIRStopBits(settings), // Convert stop bits from settings
                Encoding.ASCII // Assuming ASCII encoding for IR serial communication
            );
        }

            // Helper to map baud rate int to eIRSerialBaudRates
        private eIRSerialBaudRates ParseIRBaudRate(int baudRate)
        {
            switch (baudRate) {
                case 9600:
                    return eIRSerialBaudRates.ComspecBaudRate9600;
                case 19200:
                    return eIRSerialBaudRates.ComspecBaudRate19200;
                case 38400:
                    return eIRSerialBaudRates.ComspecBaudRate38400;
                case 57600:
                    return eIRSerialBaudRates.ComspecBaudRate57600;
                case 115200:
                    return eIRSerialBaudRates.ComspecBaudRate115200;
                default:
                    return eIRSerialBaudRates.ComspecBaudRate9600;
            }
        }

        // Helper to map settings string to eIRSerialDataBits
        private eIRSerialDataBits ParseIRDataBits(string settings)
        {
            if (string.IsNullOrEmpty(settings) || settings.Length < 1)
                return eIRSerialDataBits.ComspecDataBits8;

            switch (settings[0]) {
                case '7':
                    return eIRSerialDataBits.ComspecDataBits7;
                case '8':
                    return eIRSerialDataBits.ComspecDataBits8;
                default:
                    return eIRSerialDataBits.ComspecDataBits8;
            }
        }

        // Helper to map settings string to eIRSerialParityType
        private eIRSerialParityType ParseIRParity(string settings)
        {
            if (string.IsNullOrEmpty(settings) || settings.Length < 2)
                return eIRSerialParityType.ComspecParityNone;

            char parityChar = char.ToUpper(settings[1]);
            switch (parityChar) {
                case 'N':
                    return eIRSerialParityType.ComspecParityNone;
                case 'E':
                    return eIRSerialParityType.ComspecParityEven;
                case 'O':
                    return eIRSerialParityType.ComspecParityOdd;
                default:
                    return eIRSerialParityType.ComspecParityNone;
            }
        }

        // Helper to map settings string to eIRSerialStopBits
        private eIRSerialStopBits ParseIRStopBits(string settings)
        {
            if (string.IsNullOrEmpty(settings) || settings.Length < 3)
                return eIRSerialStopBits.ComspecStopBits1;

            switch (settings[2]) {
                case '1':
                    return eIRSerialStopBits.ComspecStopBits1;
                case '2':
                    return eIRSerialStopBits.ComspecStopBits2;
                default:
                    return eIRSerialStopBits.ComspecStopBits1;
            }
        }

      

        public void SendData(string data)
        {
            //CrestronConsole.PrintLine("Sending data on SerialPort with ID: {0}, Data: |{1}|", Id, data);
            try {
                // Unescape any escaped double quotes in the data
                string unescapedData = data.Replace("\\\"", "\"");
                //CrestronConsole.PrintLine("unescaped with ID: {0}, Data: |{1}|", Id, unescapedData);
                _irPort.SendSerialData(unescapedData);
            }
            catch (Exception ex) {
                CrestronConsole.PrintLine("Error sending data: {0}", ex.Message);
            }
        }

    }
}
