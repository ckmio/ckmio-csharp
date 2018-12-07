using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;

namespace Ckmio
{

    public class Response
    {
        public string clt_ref {get; set;}
        public string status {get; set; }
        public string service {get; set;}
        public string message {get; set; }
        public Dictionary<string, Object> payload { get; set;}
    }
    
    public class ChatMessage{
         Dictionary<string, Object> _data;
        public ChatMessage(Dictionary<string, Object> dict)
        {
            _data = dict;
        }
        public string Content  {get  { return (string)CkmioClient.TryGet(_data, "content") ;}}
        public string From {get  { return (string)CkmioClient.TryGet(_data, "from") ;}}
        public string Type {get  { return (string)CkmioClient.TryGet(_data, "type") ;}}
        public string To  {get  { return (string)CkmioClient.TryGet(_data, "to") ;}}
    }

    public class FunnelCondition{
        public string field;
        public string op;
        public object value;

        public FunnelCondition(string field, string op, object value){
            this.field = field; 
            this.op = op;
            this.value = value;
        }
    }

    public class FunnelUpdate
    {
        Dictionary<string, Object> _data;
        public FunnelUpdate(Dictionary<string, Object> dict) => _data = dict;
        public string Stream {get  { return (string)CkmioClient.TryGet(_data, "stream") ;}}
         public Object Content  {get  { return CkmioClient.TryGet(_data, "content") ;}}
         public Object When {get  { return CkmioClient.TryGet(_data, "when") ;}}
    }

    public class State {
        public int BytesToRead { get; set; }
        public int Read {get; set ;}
        public int Type {get; set;}
    }

    public class TopicUpdate
    {
        Dictionary<string, Object> _data;
        public TopicUpdate(Dictionary<string, Object> dict) => _data = dict;
        public string Name {get  { return (string)CkmioClient.TryGet(_data, "name") ;}}
         public string Content  {get  { return (string)CkmioClient.TryGet(_data, "content") ;}}
    }
    public class CkmioClient
    {
        const string Auth = "10";
        const string Chat = "22";
        const string Topic = "21";
        const string Funnel = "27";
        static string SendChatMessage = "send-chat-message";
        static string Add = "add";
        static string Notify = "notify";
        static string Authenticate = "authenticate";
        static string Subscribe = "subscribe";
        static string Address = "ckmio.com";
        public static int Port = 7023;

        private Byte[] Buffer = new byte[1024];
        private StringBuilder BufferStr = new StringBuilder();
        private Byte[] tokenSize = new byte[4];

        public string PlanKey {get; private set;}
        public string PlanSecret {get; private set;}
        public string UserName {get; private set;}
        public string Password {get; private set;}
        private Socket Connection {get; set; }
        public bool Debug {get; set;}
        public CkmioClient(string planKey, string planSecret)
        {
            PlanKey = planKey;
            PlanSecret = planSecret;
            UserName = ""; 
            Password = "";
        }

        public CkmioClient(string planKey, string planSecret, string userName, string password)
        {
            PlanKey = planKey;
            PlanSecret = planSecret;
            UserName = userName;
            Password = password;
        }
        private ManualResetEvent quit = new ManualResetEvent(false);
        public void Start()
        {
            Connect();
        }
        public void Stop()
        {
        }

        public void Connect()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Address);  
            IPAddress ipAddress = ipHostInfo.AddressList[0];  
            IPEndPoint endpoint =  new IPEndPoint(ipAddress, Port);
            Connection = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);  
            // Connect to the remote endpoint.  
            Connection.Connect(endpoint); 
            AuthenticateClient();
            BeginReceive(new State{Type = 0, BytesToRead =4, Read =0}); 
                
        }

        public Action<ChatMessage> ChatMessageHandler {get; set;}
        public Action<FunnelUpdate> FunnelUpdateHandler {get; set;}
        public Action<TopicUpdate> TopicUpdateHandler {get; set;}

        public void BeginReceive(State state)
        {
            Connection.BeginReceive(Buffer, 0, state.BytesToRead-state.Read, 0,  
                new AsyncCallback(EndReceive), state);  
        }

        private void EndReceive(IAsyncResult iar)
        {
            int bytesRead = Connection.EndReceive(iar);
            var state  =  (State)iar.AsyncState;
            
            if(Debug) Console.WriteLine($"bytes read : {bytesRead}");

            switch (state.Type)
            {
                case 0: 
                    for(var i=0; i< bytesRead; i++)this.tokenSize[i+state.Read] = Buffer[i];
                    state.Read += bytesRead;
                    if(state.BytesToRead == state.Read)
                        BeginReceive(new State{ Type =1, BytesToRead =  tokenSize[2]*256 + tokenSize[3], Read = 0});
                    else 
                        BeginReceive(state);
                    break;
                default: 
                    BufferStr.Append(System.Text.Encoding.UTF8.GetString(Buffer, 0, bytesRead));
                    if(Debug) Console.WriteLine($"Actual data : {BufferStr}");
                    state.Read += bytesRead;
                    if(state.BytesToRead == state.Read){
                        var response = (JsonConvert.DeserializeObject<Response>(BufferStr.ToString()));
                        HandleResponse(response);
                        BufferStr.Clear();
                        BeginReceive(new State{ Type =0, BytesToRead =  4, Read = 0});
                    }
                    else 
                        BeginReceive(state);
                    break;
            } 
        }

        private void HandleResponse(Response response)
        {
            if(response == null)
                return;
            switch(response.service)
            {
                case Chat:
                    ChatMessageHandler?.Invoke(new ChatMessage(response.payload));break;
                case Topic: 
                    TopicUpdateHandler?.Invoke(new TopicUpdate(response.payload));break;
                case Funnel:
                    FunnelUpdateHandler?.Invoke(new FunnelUpdate(response.payload));break;
            }
            if(Debug) Console.WriteLine($"Valid response message from {response.service}");
        }
        public void Send(string to, string message)
        {
            FormatAndSendMessage(Connection, Chat, 
            new {
                action = SendChatMessage,
                clt_ref = Guid.NewGuid().ToString(),
                payload = new { from = UserName, to = to, content = message  },
            });
        }

        public void SendToStream(string streamName, Object message)
        {
            FormatAndSendMessage(Connection, Funnel, 
            new {
                action = Add,
                clt_ref = Guid.NewGuid().ToString(),
                payload = new { stream = streamName, content = message }
            });
        }

        public void SubscribeToChat()
        {
           FormatAndSendMessage(Connection, Chat, 
            new {
                action = Subscribe,
                clt_ref = Guid.NewGuid().ToString(),
                payload = new { }
            }); 
        }

        public void UpdateTopic(string name, string content)
        {
           FormatAndSendMessage(Connection, Topic, 
            new {
                action = Notify,
                clt_ref = Guid.NewGuid().ToString(),
                payload = new { name = name, content = content }
            }); 
        }

        public void StartFunnel(string stream, FunnelCondition[] conditions)
        {
           FormatAndSendMessage(Connection, Funnel, 
            new {
                action = Subscribe,
                clt_ref = Guid.NewGuid().ToString(),
                payload = new { stream = stream, when = conditions }
            }); 
        }

        public void SubscribeToTopic(string name)
        {
           FormatAndSendMessage(Connection, Topic, 
            new {
                action = Subscribe,
                clt_ref = Guid.NewGuid().ToString(),
                payload = new { name = name }
            }); 
        }
        private void AuthenticateClient()
        {
            FormatAndSendMessage(Connection, Auth, 
            new {
                action = Authenticate,
                clt_ref = Guid.NewGuid().ToString(),
                payload = new { user = UserName, password = Password, plan_key = PlanKey, plan_secret = PlanSecret  },

            });
        }
        public void FormatAndSendMessage(Socket client, string Service, Object payload) 
        {
            var message = Service + ":" + JsonConvert.SerializeObject(payload);
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            client.Send(LengthOnForBytes(bytes.Length));
            client.Send(bytes);
        }
        public static Byte[] LengthOnForBytes(int length){
            return new Byte[]{
                (Byte)((length & 0xff000000) >> 24),
                (Byte)((length & 0x00ff0000) >> 16),
                (Byte)((length & 0x0000ff00) >> 8),
                (Byte)((length & 0x000000ff))
            };
        }

        public static Object TryGet(Dictionary<string, Object> from, string index, Object defaultValue = null)
        {
            Object retValue = defaultValue;
            from.TryGetValue(index, out retValue);
            return retValue;
        }
    }
}
