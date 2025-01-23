using System.Collections.Generic;

namespace table.lib;

public class StyleSettings
{
    public StyleType StyleType { get; set; }

    public Dictionary<string, string> Properties { get; set; } = new();
}