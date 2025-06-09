import { C4X } from './c4x';

// Initialize the C4X library
const c4x = new C4X();

// ===== 1. READY EVENT =====
c4x.onReady((info) => {
  console.log('System ready!');
  console.log('Version:', info.version);
  console.log('Serial:', info.serialNumber);
});

// ===== 2. CONSOLE OUTPUT =====
c4x.crestronWriteLine('Hello from Macro Framework!');

// ===== 3. REBOOT SYSTEM =====
// c4x.reboot(); // Uncomment to reboot

// ===== 4. GET AVAILABLE COM PORTS =====
async function getAvailablePorts() {
  try {
    const ports = await c4x.getComPorts();
    console.log('Available COM ports:', ports);
  } catch (error) {
    console.error('Error getting ports:', error);
  }
}
getAvailablePorts();

// ===== 5. COM PORT USAGE =====
// Sony projector
async function useComPort() {
  try {
    // Open COM port 2 at 38400 baud, 8E1
    const comPort = await c4x.getComPort(2, 38400, 8, 'E', 1);
    
    // Listen for incoming data
    comPort.onDataReceived((data) => {
      console.log('COM port received:', data);
    });
    
    // Send data
    comPort.send('power "on"\r\n');
    
  } catch (error) {
    console.error('COM port error:', error);
  }
}
useComPort();

// ===== 6. IR COM PORT USAGE =====
// LG display
async function useIrPort() {
  try {
    // Open IR port 1
    const irPort = await c4x.getIrComPort(1, 9600, 8, 'N', 1);
    
    // Send IR command
    irPort.send('ka 00 01\r');
    
  } catch (error) {
    console.error('IR port error:', error);
  }
}
useIrPort();

// ===== 7. RELAY CONTROL =====
const relay = c4x.getRelay(1);

// Turn relay on
relay.setState(true);

// Turn relay off after 2 seconds
setTimeout(() => {
  relay.setState(false);
}, 2000);