'use strict';

var Transport = require('azure-iot-device-mqtt').Mqtt;
var Client = require('azure-iot-device').ModuleClient;
var Message = require('azure-iot-device').Message;

var express = require('express');
var app = express();
var http = require('http').Server(app);
var io = require('socket.io')(http);

var _client;

// We do not expose the internal module paths to our website.
app.use('/scripts', express.static(__dirname + '/node_modules/highcharts/'));
app.use('/themes', express.static(__dirname + '/node_modules/highcharts/themes/'));
app.use('/css', express.static(__dirname + '/css/'));
app.use('/js', express.static(__dirname + '/js/')); 

// Before we redirect to dashboard page, we query module twins to apply a potential config update 
app.get('/', function(req, res){
  console.log('Page refresh');

  _client.getTwin(function (err, twin) {
    if (err) {
        console.error('Error getting module twin: ' + err.message);
    } else {
        twin.on('properties.desired', function(props) {

          let config = {  
            NBDevices: props.NBDevices,
            Graph2_DeviceID : props.Graph2_DeviceID
          };
          let data = JSON.stringify(config); 
          io.emit('config', data);
        });
    }
  });

  res.sendFile(__dirname + '/index.html');
});

// Log initialization 
http.listen(1234, function(){
  console.log('listening on *:1234');
});

// Default code from Module template
Client.fromEnvironment(Transport, function (err, client) {
  if (err) {
    throw err;
  } 
  else {
    client.on('error', function (err) {
      throw err;
    });

    // connect to the Edge instance
    client.open(function (err) {
      if (err) {
        throw err;
      } 
      else {
        console.log('IoT Hub module client initialized');

        _client = client;

        // Act on input messages to the module.
        client.on('inputMessage', function (inputName, msg) {
          pipeMessage(client, inputName, msg);
        });
      }
    });
  }
});

// This function just pipes the messages without any change.
function pipeMessage(client, inputName, msg) {
  client.complete(msg, printResultFor('Receiving message'));

  if (inputName === 'input1') {
    var message = msg.getBytes().toString('utf8');
    if (message) {
      var outputMsg = new Message(message);

      console.log(message);
      io.emit('iot message', message);

      client.sendOutputEvent('output1', outputMsg, printResultFor('Sending received message'));
    }
  }
}

// Helper function to print results in the console
function printResultFor(op) {
  return function printResult(err, res) {
    if (err) {
      console.log(op + ' error: ' + err.toString());
    }
    if (res) {
      console.log(op + ' status: ' + res.constructor.name);
    }
  };
}
