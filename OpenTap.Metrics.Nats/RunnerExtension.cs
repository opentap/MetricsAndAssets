using NATS.Client;

namespace OpenTap.Metrics.Nats
{
    public class RunnerExtension
    {
        public IConnection Connection { get; }
        public string RunnerId { get; }

        private RunnerExtension(IConnection connection, string runnerId)
        {
            Connection = connection; RunnerId = runnerId; BaseSubject = $"OpenTap.Runner.{runnerId}";
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
    }
}
