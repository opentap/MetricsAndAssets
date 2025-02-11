using System.Collections.Generic;

namespace OpenTap.Metrics.Settings;

[Display("Blocked Metrics")]
public class MetricsBlockList : ComponentSettings<MetricsBlockList>
{
    [Display("Blocked Metrics", "These metrics have been manually disabled.")]
    public List<string> BlockList { get; set; } = new();
    
    public bool IsBlocked(MetricSpecifier d) => BlockList.Contains(d.ToString());

    public void Block(MetricSpecifier d)
    {
        if (!IsBlocked(d))
            BlockList.Add(d.ToString());
    }
}