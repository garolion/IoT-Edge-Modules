namespace ChildDeviceManagerModule
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    class Program
    {
        private static string connectionString = "HostName=AFOCloudGateway.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=1qE/bf15ahuljH8mP1tSNh7miw8tWxei7W3ULRKvj3k=";
        private static string iotHubName = "AFOCloudGateway.azure-devices.net";
        private static string deviceScope = "ms-azure-iot-edge://AzureUbuntu-636934679240085723";
                
        static int counter;

        static int timeOut_mns { get; set; } = 1;
        private static Dictionary<string,Device> devices = new Dictionary<string, Device>();
        private static Dictionary<string,DeviceClient> connections = new Dictionary<string, DeviceClient>();
        private static Dictionary<string,Timer> timers = new Dictionary<string, Timer>();

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(Microsoft.Azure.Devices.Client.TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", MessageReceivedAsync, ioTHubModuleClient);

            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);

            string queryString = "SELECT * FROM devices" +
                            $" WHERE deviceScope = '{deviceScope}'" +
                            " AND capabilities.iotEdge = false";

            var query = registryManager.CreateQuery(queryString);
            while (query.HasMoreResults)
            {
                    IEnumerable<Twin> page = await query.GetNextAsTwinAsync();
                    foreach (Twin twin in page)
                    {
                        Console.WriteLine("DeviceId:" + twin.DeviceId + " Status:" + twin.Status.ToString());
                        var device = await registryManager.GetDeviceAsync(twin.DeviceId);
                        devices.Add(device.Id, device);

                        Console.WriteLine($"Device {twin.DeviceId} registered.");
                    }
            }
        }

        static Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine($"{DateTime.Now.ToString()} - Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));
           
                if (desiredProperties.Contains("TimeOut_mns") && desiredProperties["TimeOut_mns"]!=null)
                {
                    if(int.TryParse(desiredProperties["TimeOut_mns"].ToString(), out int timeOut))
                    {
                        timeOut_mns = timeOut;
                        Console.WriteLine($"value updated for Property 'TimeOut_mns': {timeOut_mns}");

                        CallDirectMethodAsync().Wait();
                    }
                    else
                    {
                        Console.WriteLine($"Check the property TimeOut_mns ({desiredProperties["TimeOut_mns"]}) in the Module Twin. It must be an int");
                    }
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            return Task.CompletedTask;
        }

        static async Task CallDirectMethodAsync()
        {
            try
            {
                var varEnvs = System.Environment.GetEnvironmentVariables();
                foreach(System.Collections.DictionaryEntry varEnv in varEnvs)
                {
                    Console.WriteLine($"Variable: key '{varEnv.Key}', value '{varEnv.Value}'");
                }
                
                var deviceId = System.Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
                Console.WriteLine($"deviceId: {deviceId}");

                // MethodRequest request = new MethodRequest("MethodA", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));
                string message = "{ \"TimeOut_mns\": " + timeOut_mns.ToString() + " }";
                MethodRequest request = new MethodRequest("NotifyTimeOut", Encoding.UTF8.GetBytes(message));

                MqttTransportSettings mqttSetting = new MqttTransportSettings(Microsoft.Azure.Devices.Client.TransportType.Mqtt_Tcp_Only);
                ITransportSettings[] settings = { mqttSetting };
                ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                await ioTHubModuleClient.OpenAsync();

                var response = await ioTHubModuleClient.InvokeMethodAsync(deviceId, "MQTTClientModule", request).ConfigureAwait(false);
                Console.WriteLine($"Received response with status {response.Status}");
            }
            catch(Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error : {ex.Message}");
            }
        }


        static async Task TimeOut(object stateInfo, string deviceID)
        {
            var deviceClient = connections[deviceID];
            await deviceClient.CloseAsync();

            timers.Remove(deviceID);
            connections.Remove(deviceID);

            Console.WriteLine($"Device {deviceID} disconnected.");
        }


        static async Task ConnectDeviceAsync(Device device)
        {
            var auth = new DeviceAuthenticationWithRegistrySymmetricKey(device.Id, device.Authentication.SymmetricKey.PrimaryKey);
            var deviceClient = DeviceClient.Create(iotHubName, auth);
            await deviceClient.OpenAsync();
            connections.Add(device.Id, deviceClient);

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDeviceDesiredPropertiesUpdate, device.Id);

            // Because IoT Hub don't support offline updates we synchronize properties
            var twin = await deviceClient.GetTwinAsync();
            await OnDeviceDesiredPropertiesUpdate(twin.Properties.Desired, device.Id);
        }


        static Task OnDeviceDesiredPropertiesUpdate(TwinCollection desiredProperties, object deviceContext)
        {
            string deviceId = deviceContext.ToString();
            
            Console.WriteLine($"Desired properties have been updated for device '{deviceId}'");
            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just p();ipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> MessageReceivedAsync(Microsoft.Azure.Devices.Client.Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

            if (!timers.ContainsKey(messageBody.Machine.Id))
            {
                Console.WriteLine($"Connecting Device '{messageBody.Machine.Id}'...");
                await ConnectDeviceAsync(devices[messageBody.Machine.Id]);
                await ReportPropertyAsync(messageBody.Machine.Id, "LastConnection", DateTime.Now.ToString());
            }
            
            if(timers.ContainsKey(messageBody.Machine.Id))
            {
                timers[messageBody.Machine.Id].Change(timeOut_mns * 60000, 0);
            }
            else
            {
                timers.Add(messageBody.Machine.Id, new Timer(x => TimeOut (x, messageBody.Machine.Id).Wait(), null, timeOut_mns * 60000, 0));
            }                
            
            return MessageResponse.Completed;
        }

        static async Task ReportPropertyAsync(string deviceId, string propertyName, string propertyValue)
        {
            try
            {
                var reportedProperties = new TwinCollection();
                var customProps = new TwinCollection();
                customProps[propertyName] = propertyValue;
                reportedProperties["CustomProps"] = customProps;
                await connections[deviceId].UpdateReportedPropertiesAsync(reportedProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
        }
    }

    class MessageBody
    {
        public Machine Machine {get;set;}
        public Ambient Ambient {get; set;}
        public DateTime TimeCreated {get; set;}
    }
    class Machine
    {
        public string Id {get; set;}
        public double Temperature {get; set;}
        public double Pressure {get; set;}         
    }
    class Ambient
    {
        public double Temperature {get; set;}
        public int Humidity {get; set;}         
    }

    class DeviceConfig
    {
        public string ID {get; set;}
        public String Schema {get; set;}     
        public String DataTopic {get; set;}     
        public String FeedbackTopic {get; set;}         
    }
}



