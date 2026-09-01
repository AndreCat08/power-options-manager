using System.Windows;

namespace PowerOptionsManager.Models;

public class PowerPlan
{
    public string Name { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsInactive => !IsActive;
    public DateTime? LastModified { get; set; }
}
