using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EdgeHub;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Functions.Samples
{
    public static class AlertingModule
    {
        [FunctionName("AlertingModule")]
        public static async Task FilterMessageAndSendMessage(
                    [EdgeHubTrigger("input1")] Message messageReceived,
                    [EdgeHub(OutputName = "output1")] IAsyncCollector<Message> output,
                    ILogger logger)
        {
            int Temp_Threshold = 100;
            
            byte[] messageBytes = messageReceived.GetBytes();
            var messageString = System.Text.Encoding.UTF8.GetString(messageBytes);

            if (!string.IsNullOrEmpty(messageString))
            {
                logger.LogInformation("Info: Received one non-empty message");
                
                var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

                if (messageBody.Machine.Temperature >= Temp_Threshold)
                {
                    Console.WriteLine($"*** Alert *** for {messageBody.Machine} at {messageBody.TimeCreated}");

                    try
                    {
                        var pipeMessage = new Message(messageBytes);
                        pipeMessage.Properties.Add("MessageType", "Alert");  
                        pipeMessage.Properties.Add("Alert", "Temp");
                        pipeMessage.Properties.Add("DeviceId", messageBody.Machine.Id);     
                        
                        await output.AddAsync(pipeMessage);
                        Console.WriteLine("Message sent.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error invoking method {ex}");
                    }
                }
                else
                {
                     Console.WriteLine($"Message ok for {messageBody.Machine} at {messageBody.TimeCreated}");
                }
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