# c4x
Crestron Control 4 port extender for Cisco Codecs

## What it is ?
Use your favorite Crestron 4-series processor as a port extender in Cisco Macro Framework.

Load the program on your Crestron processor, and access the serial ports (send, receive), the ir-serial ports (send) and relays!

## Why use that ?
- If you have enough of Crestron programming
- If you want to centralize your code on only one system
- You have some JavaScript skills and do not know Crestron programming
- You need to control multiple devices with your Cisco codec

## How is it working ?
- The Crestron processor and the codec are communicating through base64-encoded JSON messages through the serial port COM1 (for now. Looking into SSH).

## What is currently working ?
This is only a draft for. There's debug everywhere. Serial ports and ir-serial ports appears to be fully working. 

But, I would not use this in production for now.

Code will be published in 1-2 weeks.

## Example
### Initialize the C4X communication
```JS
import C4X from './lib_c4x';

let crestron = new C4X();
```

### Register the `onReady` event in which you configure your serial ports
That way, even if the Crestron processor is rebooted, your serial ports will still be how you want them.
```JS
crestron.onReady(async procInfo => {
  // Display informations about the Crestron Processor
  console.log(`C4X Version: ${procInfo.v}`);
  console.log(`C4X Serial Number: ${procInfo.v}`);

  // Setup serial port for displays

  // COM2, baudrate of 38400, 8 databits, Even parity, 1 stopbit

  let projector = await crestron.getComPort(2, 38400, 8, 'E', 1);
  projector.onDataReceived(data => {
    console.log(`Data from projector: ${data}`);
  });

  // COM3, baudrate of 9600, 8 databits, No parity, 1 stopbit

  let monitor = await crestron.getComPort(3, 9600, 8, 'N', 1);
  monitor.onDataReceived(data => {
    console.log(`Data from monitor: ${data}`);
  });

  // Power on the displays
  projector.send('power "on"\r\n');
  monitor.send('ka 00 01\r\n');

  // Get relay 1
  let shades = await c4x.getRelay(1);

  // Close the relay
  shades.setState(true);
});
```
