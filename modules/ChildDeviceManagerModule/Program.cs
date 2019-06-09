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
        private static Dictionary<string,DeviceClient> connections = new Dictionary<string, DeviceClient>();

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

                        var auth = new DeviceAuthenticationWithRegistrySymmetricKey(device.Id, device.Authentication.SymmetricKey.PrimaryKey);
                        var deviceClient = DeviceClient.Create(iotHubName, auth);

                        connections.Add(device.Id, deviceClient);
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

        static async Task TimeOut(object stateInfo, string deviceID)
        {
            var deviceClient = connections[deviceID];
            await deviceClient.CloseAsync();

            Console.WriteLine($"Device {deviceID} disconnected.");
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> MessageReceivedAsync(Microsoft.Azure.Devices.Client.Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            var device = await registryManager.GetDeviceAsync(messageBody.Machine.Id);

            if (device?.ConnectionState == DeviceConnectionState.Disconnected)
            {
                await ReportPropertyAsync(messageBody.Machine.Id, "LastConnection", DateTime.Now.ToString());
                
                var deviceClient = connections[messageBody.Machine.Id];
                await deviceClient.OpenAsync();
                var timer = new Timer(x => TimeOut (x, messageBody.Machine.Id).Wait(), null, timeOut_mns * 60000, 0);
                Console.WriteLine($"Device {messageBody.Machine.Id} connected.");
            }
            
            return MessageResponse.Completed;
        }

        static async Task ReportPropertyAsync(string deviceId, string propertyName, string propertyValue)
        {
            try
            {
                var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
                var device = await registryManager.GetDeviceAsync(deviceId);
                var deviceConnectionString = $"HostName={iotHubName};DeviceId={deviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
                
                var client = DeviceClient.CreateFromConnectionString(deviceConnectionString, Microsoft.Azure.Devices.Client.TransportType.Mqtt);

                // TwinCollection reportedProperties, michelin;
                var reportedProperties = new TwinCollection();
                var michelin = new TwinCollection();
                michelin[propertyName] = propertyValue;
                reportedProperties["Michelin"] = michelin;
                await client.UpdateReportedPropertiesAsync(reportedProperties);

                client.Dispose();
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



