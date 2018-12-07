using System;
using Ckmio;

namespace Ckmio.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            
            var ckmio = new CkmioClient("community-test-key", "community-test-secret", "Khady", "");
            ckmio.Start();
            ckmio.Debug = false;
            String topicName = "A brand new topic";  
            String streamName = "A brand new Stream";
            ckmio.SubscribeToChat();
            ckmio.SubscribeToTopic(topicName);
            ckmio.StartFunnel(streamName, new FunnelCondition[]{new FunnelCondition("age", "greater_than", 40)});
            ckmio.ChatMessageHandler = (ChatMessage message)=> Console.WriteLine("Received Message from "+ message.From);
            ckmio.TopicUpdateHandler = (TopicUpdate topicUpdate)=> Console.WriteLine("Topic Update: " + topicUpdate.Content);
            ckmio.FunnelUpdateHandler = (FunnelUpdate FunnelUpdate)=> Console.WriteLine("Funnel Update: " + FunnelUpdate.Content);

            ckmio.Send("Khady", "Hello");
            for(var i=0; i<10000;  i++){
                ckmio.UpdateTopic(topicName, "Topic Update");
            }
            ckmio.SendToStream(streamName, new { age = 60, name = "Bob", gender = "Male"});
            ckmio.SendToStream(streamName, new { age = 20, name = "Alice", gender = "Female"});
            Console.ReadLine();
            ckmio.Stop();
        }
    }
}
