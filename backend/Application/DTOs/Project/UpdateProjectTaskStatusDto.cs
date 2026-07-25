using Domain.Enums;

namespace Application.DTOs.Project;

public class UpdateProjectTaskStatusDto
{
    public ProjectTaskStatus Status { get; set; }
}