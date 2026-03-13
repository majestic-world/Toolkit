using System.Collections.Generic;

namespace UnrealTools.Models;

/// <summary>
/// Represents a single row in the Recent Activities DataGrid.
/// </summary>
public class ActivityItem
{
    public string Name     { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Status   { get; set; } = string.Empty;
    public string Date     { get; set; } = string.Empty;

    // Avalonia brush strings for status badge
    public string StatusBackground { get; set; } = "#F0F2F5";
    public string StatusForeground { get; set; } = "#3D4452";

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    private static ActivityItem Create(
        string name, string email, string status, string date)
    {
        var initials = BuildInitials(name);
        var (bg, fg) = StatusColors(status);
        return new ActivityItem
        {
            Name             = name,
            Email            = email,
            Initials         = initials,
            Status           = status,
            Date             = date,
            StatusBackground = bg,
            StatusForeground = fg,
        };
    }

    private static string BuildInitials(string fullName)
    {
        var parts = fullName.Split(' ');
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}"
            : fullName[..1].ToUpper();
    }

    private static (string bg, string fg) StatusColors(string status) =>
        status switch
        {
            "Completed"  => ("#F0FDF4", "#16A34A"),
            "Pending"    => ("#FFF7ED", "#C2410C"),
            "Processing" => ("#EEF2FF", "#4338CA"),
            "Cancelled"  => ("#FFF1F2", "#BE123C"),
            "On Hold"    => ("#F5F3FF", "#7C3AED"),
            _            => ("#F0F2F5", "#3D4452"),
        };

    // -----------------------------------------------------------------------
    // Sample data
    // -----------------------------------------------------------------------

    public static List<ActivityItem> GetSampleData() =>
    [
        Create("Alice Johnson",   "alice@example.com",   "Completed",  "07 Mar 2026, 09:14"),
        Create("Bruno Silva",     "bruno@example.com",   "Processing", "07 Mar 2026, 08:52"),
        Create("Carla Mendes",    "carla@example.com",   "Pending",    "06 Mar 2026, 17:30"),
        Create("David Park",      "david@example.com",   "Completed",  "06 Mar 2026, 15:05"),
        Create("Elena Rossi",     "elena@example.com",   "Cancelled",  "06 Mar 2026, 12:48"),
        Create("Felipe Torres",   "felipe@example.com",  "On Hold",    "05 Mar 2026, 22:10"),
        Create("Grace Kim",       "grace@example.com",   "Completed",  "05 Mar 2026, 18:33"),
        Create("Hugo Martins",    "hugo@example.com",    "Processing", "05 Mar 2026, 14:20"),
        Create("Isabela Costa",   "isabela@example.com", "Pending",    "04 Mar 2026, 11:55"),
        Create("James Nguyen",    "james@example.com",   "Completed",  "04 Mar 2026, 09:02"),
    ];
}
