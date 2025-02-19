using System;
using System.Linq;
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
        private IConnection connection;
        private readonly string runnerId;
        private readonly Guid? sessionId;
        private readonly string baseSubject;

        private static readonly TraceSource _log = Log.CreateSource("Runner Extension");
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

            // The runner secretly allows the user to change the server URL by passing the --server-url command line argument. 
            // We should check for that to make sure we connect to the correct server.
            // This is mostly for internal use, as that command line argument is not documented.
            var clargs = Environment.GetCommandLineArgs();
            if (clargs.Contains("--nats-server") && clargs.Length > Array.IndexOf(clargs, "--nats-server") + 1)
            {
                DefaultServer = clargs[Array.IndexOf(clargs, "--nats-server") + 1];
            }

            var options = ConnectionFactory.GetDefaultOptions();
            options.Servers = new string[] { DefaultServer };
            options.Name = "Runner Extensions";
            options.MaxReconnect = Options.ReconnectForever;
            options.ReconnectWait = 1000;
            options.AllowReconnect = true;
            options.PingInterval = 45000;
            options.Timeout = 5000;
            options.DisconnectedEventHandler += (sender, args) => { _log.Debug($"NATS connection disconnected"); };
            options.AsyncErrorEventHandler += (sender, args) => { _log.Info("NATS connection async error: {error}", args.Error); };
            options.ReconnectedEventHandler = (sender, args) => { _log.Info($"NATS connection reconnected"); };

            connection = new ConnectionFactory().CreateConnection(options);
            _log.Debug($"NATS connected to {DefaultServer}");
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
