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

- MQTT Client
Subcribe to Topics from a MQTT Broker, to receive and forward messages to Azure IoT Hub

- Simple Dashboard
Visualize data that are received by the MQTT Client module and sent to Azure IoT Hub 

![Dashboard](/doc/dashboard.png)
