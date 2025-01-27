using System.Collections.Generic;

namespace ListToTable;

public class StyleSettings
{
    public StyleType StyleType { get; set; }

    public Dictionary<string, string> Properties { get; set; } = new();
}