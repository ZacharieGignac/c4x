import xapi from 'xapi';


export class C4X {
  onReadyCallbacks = [];
  sentRequests = [];
  activeComPorts = []; // Track active COM port objects

  constructor() {
    let self = this;

    xapi.Event.Message.Send.on(message => {
      try {
        // Check if message is base64 encoded by attempting to decode
        let decodedMessage = atob(message.Text);
        //console.warn(decodedMessage);
        self.parseMessage(decodedMessage);
      } catch (e) {
        // Ignore messages that are not valid base64
        //console.log('Ignoring non-base64 message:', message.Text);
      }
    });

    this.sendInitRequest();
  }

  generateRandomId() {
    let result = '';
    for (let i = 0; i < 8; i++) {
      result += Math.floor(Math.random() * 16).toString(16);
    }
    return result.toUpperCase();
  }

  parseMessage(message) {
    try {
      let json = JSON.parse(message);

      if (json.hasOwnProperty("C4X") && json.hasOwnProperty("t")) {
        switch (json.t) {
          case 'inot':
            //console.log('Parsing: ' + message);
            console.log('C4X: Init notify received');
            this.raiseOnReady(json);
            break;
          case 'cger':
            //console.warn('Parsing: ' + message);
            this.raiseGetComPorts(json);
            break;
          case 'crok':
            this.raiseGetComPort(json);
            break;
          case 'crer':
            this.raiseGetComPortError(json);
            break;
          case 'irok':
            this.raiseGetIrComPort(json);
            break;
          case 'crcv':
            this.raiseComDataReceived(json);
            break;
          default:
            //Unknown message
            break;
        }
      }
    }
    catch { }
  }

  crestronWriteLine(text) {
    let obj = {
      C4X: this.generateRandomId(),
      t: 'cprn',
      d: text
    };
    this.sendJsonMessage(obj);
  }

  getComPorts() {
    return new Promise((resolve, reject) => {
      let obj = {
        C4X: this.generateRandomId(),
        t: 'cget'
      };
      this.sendJsonMessageWaitReply(obj, resolve, reject);
    });
  }

  getRelay(id) {
    let self = this;
    return {
      setState: (state) => {
        if (typeof state != 'boolean') {
          throw new Error('State must be boolean');
          return;
        }
        let obj = {
          C4X: this.generateRandomId(),
          t: 'srly',
          i: id,
          s: state
        }
        this.sendJsonMessage(obj);
      }
    }
  }

  getComPort(id, baudRate, databits, parity, stopbits) {
    // Validate that id is a number
    if (typeof id !== 'number' || !Number.isInteger(id)) {
      return Promise.reject(new Error('ID must be a number'));
    }

    let self = this;

    return new Promise((resolve, reject) => {
      let obj = {
        C4X: this.generateRandomId(),
        t: 'ccom',
        p: id,
        b: baudRate,
        d: databits + parity + stopbits
      }

      let comPortObject = {
        type: 'COM',
        id: id,
        baudRate: baudRate,
        config: databits + parity + stopbits,
        onDataReceivedCallbacks: [], // Store callbacks for data received events
        send: (data) => {
          self.comSendData(id, data);
        },
        onDataReceived: (callback) => {
          if (typeof callback === 'function') {
            comPortObject.onDataReceivedCallbacks.push(callback);
          } else {
            console.error('onDataReceived: Must provide callback function');
          }
        },
        // Internal method to trigger callbacks
        _triggerDataReceived: (data) => {
          for (let callback of comPortObject.onDataReceivedCallbacks) {
            try {
              callback(data);
            } catch (e) {
              console.error('Error in onDataReceived callback:', e);
            }
          }
        }
      }

      // Store the comPortObject with the request so raiseGetComPort can access it
      let req = {
        id: obj.C4X,
        resolve: resolve,
        reject: reject,
        comPortObject: comPortObject  // Add this line
      }
      this.sentRequests.push(req);
      this.sendJsonMessage(obj);
    });
  }

  getIrComPort(id, baudRate, databits, parity, stopbits) {
    // Validate that id is a number
    if (typeof id !== 'number' || !Number.isInteger(id)) {
      return Promise.reject(new Error('ID must be a number'));
    }

    let self = this;
    return new Promise((resolve, reject) => {
      let obj = {
        C4X: this.generateRandomId(),
        t: 'cirp',
        p: id,
        b: baudRate,
        d: databits + parity + stopbits
      }

      let irPortObject = {
        type: 'IR',
        id: id,
        baudRate: baudRate,
        config: databits + parity + stopbits,
        send: (data) => {
          self.irSendData(id, data);
        }
      }

      let req = {
        id: obj.C4X,
        resolve: resolve,
        reject: reject,
        irPortObject: irPortObject
      }
      this.sentRequests.push(req);
      this.sendJsonMessage(obj);

    });
  }
  comSendData(port, data) {
    let obj = {
      C4X: this.generateRandomId(),
      t: 'csnd',
      p: port,
      d: data
    };

    this.sendJsonMessage(obj);
  }

  irSendData(port, data) {
    let obj = {
      C4X: this.generateRandomId(),
      t: 'isnd',
      p: port,
      d: data
    }
    this.sendJsonMessageWaitReply(obj);
  }

  sendInitRequest() {
    let obj = {
      C4X: this.generateRandomId(),
      t: 'init'
    };
    this.sendJsonMessage(obj);
  }

  reboot() {
    let obj = {
      C4X: this.generateRandomId(),
      t: 'boot'
    };
    this.sendJsonMessage(obj);
  }

  sendJsonMessage(obj) {
    try {
      let json = JSON.stringify(obj);
      //console.log(`Sending: ${json}`);

      // Encode the JSON message in base64 before sending
      let encodedMessage = btoa(json);

      xapi.Command.Message.Send({
        Text: encodedMessage
      });
    }
    catch (e) {
      console.error(`Can't stringify object: ${obj}`);
    }
  }

  sendJsonMessageWaitReply(obj, resolve, reject) {
    let req = {
      id: obj.C4X,
      resolve: resolve,
      reject: reject
    }
    this.sentRequests.push(req);
    this.sendJsonMessage(obj);
  }

  raiseOnReady(json) {
    console.log('C4X: Ready!');
    
    // Reset active COM ports list since the system has been rebooted
    this.activeComPorts = [];
    console.log('C4X: Initialized');
    
    for (let c of this.onReadyCallbacks) {
      let response = {
        version: json.v,
        serialNumber: json.s,
        totalRamSize: json.r,
        ramFree: json.f
      }
      c(response);
    }
  }

  raiseGetIrComPort(json) {
    //console.log('Raising getIrComPorts');
    // Find and resolve the matching request
    let foundRequest = this.sentRequests.find(sr => json.C4X === sr.id);
    if (foundRequest) {
      // Resolve with the irPortObject that was stored with the request
      foundRequest.resolve(foundRequest.irPortObject);
    }
    // Remove the resolved request from the array
    this.sentRequests = this.sentRequests.filter(sr => json.C4X !== sr.id);
  }

  raiseGetComPorts(json) {
    //console.log('Raising getComPorts');
    // Find and resolve the matching request
    let foundRequest = this.sentRequests.find(sr => json.C4X === sr.id);
    if (foundRequest) {
      foundRequest.resolve(json.p);
    }
    // Remove the resolved request from the array
    this.sentRequests = this.sentRequests.filter(sr => json.C4X !== sr.id);
  }

  raiseGetComPort(json) {
    // Find and resolve the matching request
    let foundRequest = this.sentRequests.find(sr => json.C4X === sr.id);
    if (foundRequest) {
      // Add the COM port object to our tracking array
      this.activeComPorts.push(foundRequest.comPortObject);
      // Resolve with the comPortObject that was stored with the request
      foundRequest.resolve(foundRequest.comPortObject);
    }
    // Remove the resolved request from the array
    this.sentRequests = this.sentRequests.filter(sr => json.C4X !== sr.id);
  }

  raiseGetComPortError(json) {
    // Find and resolve the matching request
    let foundRequest = this.sentRequests.find(sr => json.C4X === sr.id);
    if (foundRequest) {

      // Resolve with the comPortObject that was stored with the request
      foundRequest.reject(json.e);
    }
    // Remove the resolved request from the array
    this.sentRequests = this.sentRequests.filter(sr => json.C4X !== sr.id);
  }

  raiseComDataReceived(json) {
    //console.log('COM Data Received:', json);
    
    // Find the COM port object that matches the port ID
    let comPort = this.activeComPorts.find(port => port.id === json.p);
    
    if (comPort) {
      // Trigger the onDataReceived callbacks with the received data
      comPort._triggerDataReceived(json.d);
    } else {
      console.warn(`Received data for unknown COM port: ${json.p}`);
    }
  }

  onReady(callback) {
    if (callback) {
      this.onReadyCallbacks.push(callback);
    }
    else {
      console.error('onReady: Must provide callback function in first argument');
    }

  }
}