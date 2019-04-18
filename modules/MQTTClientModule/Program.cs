namespace MQTTClientModule
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    // related to MQTTnet
    using MQTTnet;
    using MQTTnet.Client;
    using MQTTnet.Protocol;

    class Program
    {
        static int counter;

        public static string MQTT_BROKER_ADDRESS = "192.168.43.134";
        
        public static int MQTT_BROKER_PORT = 4321;

        //public static int temperatureThreshold { get; set; } = 25;

        public static IMqttClient MqttClient { get; set; } = null;

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
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            Console.WriteLine("In Init()");

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Read the TemperatureThreshold value from the module twin's desired properties
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            Console.WriteLine("Reading Messages");
            await Task.Run(ReadMQTTMessages);
        }

        static Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine($"{DateTime.Now.ToString()} - Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

               //TODO
               // récupération device configs
               // réinitialisation des liens avec MQTT Broker
           
                // if (desiredProperties["TemperatureThreshold"]!=null)
                //     temperatureThreshold = desiredProperties["TemperatureThreshold"];
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

        public static async Task ReadMQTTMessages()
        {
            // var client = new MqttFactory().CreateMqttClient();

            // X509Certificate ca_crt = new X509Certificate("certs/ca.crt");

            // var tlsOptions = new MqttClientOptionsBuilderTlsParameters();
            // tlsOptions.SslProtocol = System.Security.Authentication.SslProtocols.Tls12;
            // tlsOptions.Certificates = new List<IEnumerable<byte>>() { ca_crt.Export(X509ContentType.Cert).Cast<byte>() };
            // tlsOptions.UseTls = true;
            // tlsOptions.AllowUntrustedCertificates = true;
            // tlsOptions.IgnoreCertificateChainErrors = false;
            // tlsOptions.IgnoreCertificateRevocationErrors = false;

            // var options = new MqttClientOptionsBuilder()
            // .WithClientId("IoTEdgeModule")
            // .WithTcpServer(MQTT_BROKER_ADDRESS, MQTT_BROKER_PORT)
            // .WithTls(tlsOptions)
            // .Build();

            // client.ApplicationMessageReceived += async (sender, eventArgs) => { await Client1_ApplicationMessageReceived(sender, eventArgs); };
            // await client.ConnectAsync(options);

            // if (MqttClient == null | !MqttClient.IsConnected)
            // {
                MqttClient = await ConnectAsync("IoTEdgeModule");
                MqttClient.ApplicationMessageReceived += async (sender, eventArgs) => { await Client1_ApplicationMessageReceived(sender, eventArgs); };
            // }
            // client.ApplicationMessageReceived += Client1_ApplicationMessageReceived;
            await MqttClient.SubscribeAsync("/Test1/temperature",MqttQualityOfServiceLevel.ExactlyOnce);
        }


        public static async Task<IMqttClient> ConnectAsync(string clientId)
        {
            var client = new MqttFactory().CreateMqttClient();
            X509Certificate ca_crt = new X509Certificate("certs/ca.crt");

            var tlsOptions = new MqttClientOptionsBuilderTlsParameters();
            tlsOptions.SslProtocol = System.Security.Authentication.SslProtocols.Tls12;
            tlsOptions.Certificates = new List<IEnumerable<byte>>() { ca_crt.Export(X509ContentType.Cert).Cast<byte>() };
            tlsOptions.UseTls = true;
            tlsOptions.AllowUntrustedCertificates = true;
            tlsOptions.IgnoreCertificateChainErrors = false;
            tlsOptions.IgnoreCertificateRevocationErrors = false;

            var options = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(MQTT_BROKER_ADDRESS, MQTT_BROKER_PORT)
            .WithTls(tlsOptions)
            .Build();

            await client.ConnectAsync(options);

            return client;
        }

        public static async Task PublishMQTTMessage(string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("/Test1/feedback")
                .WithPayload(payload)
                .WithExactlyOnceQoS()
                .WithRetainFlag()
                .Build();

            if (MqttClient == null | !MqttClient.IsConnected)
            {
                MqttClient = await ConnectAsync("IoTEdgeModule");
            }

            await MqttClient.PublishAsync(message);

            Console.WriteLine("Message sent");
        }


        private static async Task Client1_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            var info = $"Timestamp: {DateTime.Now:O} | Topic: {eventArgs.ApplicationMessage.Topic} | Payload: {Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload)} | QoS: {eventArgs.ApplicationMessage.QualityOfServiceLevel}";
            Console.WriteLine($"Message: {info}");

            var payload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
            
            try
            {
                var messageBody = JsonConvert.DeserializeObject<MessageBody>(payload);

                // int temperature = int.Parse(Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload)); 

                // var messageBody = new MessageBody
                // {
                //     Machine = new Machine
                //     {
                //         Temperature = temperature,
                //         Pressure = 25,
                //     },
                //     Ambient = new Ambient
                //     {
                //         Temperature = temperature - 5,
                //         Humidity = 80
                //     },
                //     TimeCreated = DateTime.UtcNow
                // };

                if (messageBody.Machine.Temperature == 100.0)
                {
                    await PublishMQTTMessage(messageBody.TimeCreated.ToLongTimeString());
                }

                string dataBuffer = JsonConvert.SerializeObject(messageBody);
                var message = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                message.Properties.Add("DeviceId", "Device1");

                //TODO: package sous forme de propriété
                MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                ITransportSettings[] settings = { mqttSetting };
                ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                await ioTHubModuleClient.OpenAsync();

                await ioTHubModuleClient.SendEventAsync("output1", message);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
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
}
