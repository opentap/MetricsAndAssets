using System;
using System.Text;
using NATS.Client;
using Newtonsoft.Json;

namespace OpenTap.Metrics.Nats
{
    public class RunnerExtension
    {
        public IConnection Connection { get; }
        public string RunnerId { get; }

        private static RunnerExtension instance;
        internal static RunnerExtension Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = GetConnection();
                }
                return instance;
            }
        }

        private RunnerExtension(IConnection connection, string runnerId)
        {
            Connection = connection;
            RunnerId = runnerId;
            BaseSubject = $"OpenTap.Runner.{runnerId}.Session.{GetSessionId()}";
        }

        public string BaseSubject { get; }
        public static string DefaultServer = "nats://127.0.0.1:20111";

        public static RunnerExtension GetConnection()
        {
            var options = ConnectionFactory.GetDefaultOptions();
            options.Servers = new string[] { DefaultServer };
            IConnection connection = new ConnectionFactory().CreateConnection(options);
            return new RunnerExtension(connection, connection.ServerInfo.ServerName);
        }

        public static Guid GetSessionId()
        {
            var sessionIdVar = Environment.GetEnvironmentVariable("OPENTAP_RUNNER_SESSION_ID");
            if (sessionIdVar == null)
            {
                throw new Exception("OPENTAP_RUNNER_SESSION_ID environment variable not set");
            }
            return Guid.Parse(sessionIdVar);
        }

        /// <summary>
        /// Used by plugins to add endpoints to the runner API
        /// </summary>
        public static void MapEndpoint<Tresponse>(string endpoint, Func<Tresponse> handler)
        {
            Instance.Connection.SubscribeAsync($"{Instance.BaseSubject}.Request.{endpoint}", (sender, args) =>
            {
                var response = handler();
                Instance.Connection.Publish(args.Message.Reply, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            });
        }

        /// <summary>
        /// Used by plugins to add endpoints to the runner API
        /// </summary>
        public static void MapEndpoint<Trequest, Tresponse>(string endpoint, Func<Trequest, Tresponse> handler)
        {
            Instance.Connection.SubscribeAsync($"{Instance.BaseSubject}.Request.{endpoint}", (sender, args) =>
            {
                var response = handler(JsonConvert.DeserializeObject<Trequest>(Encoding.UTF8.GetString(args.Message.Data)));
                Instance.Connection.Publish(args.Message.Reply, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            });
        }
    }
}
