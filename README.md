# IoT-Edge-Modules
My repository including MQTT and Dashboard modules

## How to setup the environment:
- add .env file
At the root level of the solution (same level of the deployment.template.json) add a **.env** file including:

  CONTAINER_REGISTRY_USERNAME_edgemodules="The-name-of-your-registry"
  CONTAINER_REGISTRY_PASSWORD_edgemodules="The-password-of-your-registry"

- (optional), edit module settings
For each module you can edit the **module.json** file, and modify:

  "repository": "afoedgemodules.azurecr.io/dashboardmodule"  
  "version": "0.0.6",

For more advanced informations: https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-csharp-module

## Modules included in this repo:

### MQTT Client  
Subscribe to Topics from a MQTT Broker, to receive and forward messages to Azure IoT Hub

To setup this module :
  * you first need to install a MQTT Broker (we installed Mosquitto on the same machine)  
  * you need to configure Mosquitto to use TLS with a custom port (8883 is already in use with Azure Iot Edge)  
  => follow this configuration step by step: http://www.steves-internet-guide.com/mosquitto-tls/  
  * once your Mosquitto instance configured, update the Module Twins accordingly:
    
      "MQTTClientModule": {  
        "properties.desired":{  
          "Temp_Threshold":100,  
          "MQTT_BROKER_ADDRESS":"192.168.0.32",  
          "MQTT_BROKER_PORT":4321,  
          "NBDevices":2,  
          "Device1_ID":"Dev367",  
          "Device1_Schema":"DefaultEngine",  
          "Device1_DataTopic":"Dev367/data",  
          "Device1_FeedbackTopic":"Dev367/feedback",  
          "Device2_ID":"Dev7854",  
          "Device2_Schema":"DefaultEngine",  
          "Device2_DataTopic":"Dev7854/data",  
          "Device2_FeedbackTopic":"Dev7854/feedback"  
        }
      }  
  
  * Finally update the certificate used by the module to authenticate against Mosquitto (CA.crt file in the folder /certs)  
  
- Simple Dashboard  
Visualize data that are received by the MQTT Client module and sent to Azure IoT Hub 

for the moment you need to update the source code to adapt to your device definitions.  
Changes to come to simplify the customization through Module Twins

![Dashboard](/doc/dashboard.png)
