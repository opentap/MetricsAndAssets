using System.Collections.Generic;

namespace OpenTap.Metrics.Nats
{
    internal class MetricDto
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }
}

