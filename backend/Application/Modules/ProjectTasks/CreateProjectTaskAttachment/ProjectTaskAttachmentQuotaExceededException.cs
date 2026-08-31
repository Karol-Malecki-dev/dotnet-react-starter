namespace Application.Modules.ProjectTasks.CreateProjectTaskAttachment;

/// <summary>Indicates that a task attachment quota would be exceeded.</summary>
public sealed class ProjectTaskAttachmentQuotaExceededException : Exception
{
    public ProjectTaskAttachmentQuotaExceededException(string message)
        : base(message)
    {
    }
}