namespace Shared.Settings;

public class UiFeatureSettings
{
    public bool ProjectsEnabled { get; set; } = true;

    public bool ProjectArchiveEnabled { get; set; } = true;

    public bool ProjectTaskAssignmentEnabled { get; set; } = true;

    public bool GlobalSearchEnabled { get; set; } = true;

    public bool DashboardOverviewEnabled { get; set; } = true;

    public bool AdminNavigationEnabled { get; set; } = true;

    public bool UserManagementNavigationEnabled { get; set; } = true;

    public bool EmailFeatureSectionsEnabled { get; set; } = true;
}