# c4x
Crestron Control 4 port extender for Cisco Codecs

## What it is ?
Use your favorite Crestron 4-series processor as a port extender in Cisco Macro Framework.

Load the program on your Crestron processor, and access the serial ports (send, receive), the ir-serial ports (send) and relays!

## Why use that ?
- Crestron programming gives you acid reflux
- If you want to centralize your code on only one system
- You have some JavaScript skills and do not know Crestron programming
- You need to control multiple devices with your Cisco codec

## How is it working ?
- The Crestron processor and the codec are communicating through base64-encoded JSON messages through the serial port COM1 (for now. Looking into SSH).

## What is currently working ?
Com Ports, IR Com Ports, Relays, Console PrintLine and Reboot

## Installation and Usage
- Connect the codec to the Crestron Processor using a serial cable (COM1 on the Crestron Processor)
- Upload the program (cpz file) to your Crestron Processor using Crestron ToolBox
- Upload the 2 provided macros to your codec
- Look at the examples
