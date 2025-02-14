using System;
using System.Text;
using NATS.Client;
using Newtonsoft.Json;

namespace OpenTap.Metrics.Nats
{
    /// <summary>
    /// Helpers to facilitate plugins extending the runner API
    /// </summary>
    public class RunnerExtension
    {
        private readonly IConnection connection;
        private readonly string runnerId;
        private readonly Guid? sessionId;
        private readonly string baseSubject;

        private static RunnerExtension instance;
        internal static RunnerExtension Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RunnerExtension();
                }
                return instance;
            }
        }

        public static bool IsRunnerSession() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENTAP_RUNNER_SESSION_ID"));

        private RunnerExtension()
        {
            var sessionIdVar = Environment.GetEnvironmentVariable("OPENTAP_RUNNER_SESSION_ID");
            if (string.IsNullOrEmpty(sessionIdVar))
            {
                // This is not a session, don't attempt to connect to NATS.
                return;
            }
            sessionId = Guid.Parse(sessionIdVar);

            var options = ConnectionFactory.GetDefaultOptions();
            options.Servers = new string[] { DefaultServer };
            connection = new ConnectionFactory().CreateConnection(options);
            runnerId = connection.ServerInfo.ServerName;
            baseSubject = $"OpenTap.Runner.{runnerId}.Session.{sessionId}";
        }

        public static string BaseSubject => Instance.baseSubject;
        public static string DefaultServer = "nats://127.0.0.1:20111";
        public static IConnection Connection => Instance.connection;
        public static Guid? SessionId => Instance.sessionId;

        /// <summary>
        /// Used by plugins to add endpoints to the runner API
        /// </summary>
        public static void MapEndpoint<Tresponse>(string endpoint, Func<Tresponse> handler)
        {
            if (Instance == null)
            {
                return;
            }
            Instance.connection.SubscribeAsync($"{Instance.baseSubject}.Request.{endpoint}", (sender, args) =>
            {
                var response = handler();
                Instance.connection.Publish(args.Message.Reply, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            });
        }

        /// <summary>
        /// Used by plugins to add endpoints to the runner API
        /// </summary>
        public static void MapEndpoint<Trequest, Tresponse>(string endpoint, Func<Trequest, Tresponse> handler)
        {
            if (Instance == null)
            {
                return;
            }
            Instance.connection.SubscribeAsync($"{Instance.baseSubject}.Request.{endpoint}", (sender, args) =>
            {
                var response = handler(JsonConvert.DeserializeObject<Trequest>(Encoding.UTF8.GetString(args.Message.Data)));
                Instance.connection.Publish(args.Message.Reply, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            });
        }
    }
}
