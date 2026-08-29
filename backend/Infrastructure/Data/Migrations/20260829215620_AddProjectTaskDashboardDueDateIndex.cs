using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTaskDashboardDueDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ProjectId_Status_DueDate",
                table: "ProjectTasks",
                columns: new[] { "ProjectId", "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_ProjectId_Status_DueDate",
                table: "ProjectTasks");
        }
    }
}
