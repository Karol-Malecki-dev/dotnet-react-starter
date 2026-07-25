using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectRolesAndTaskCreators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "ProjectTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ProjectMembers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.Sql("UPDATE \"ProjectMembers\" SET \"Role\" = 'Owner' FROM \"Projects\" WHERE \"ProjectMembers\".\"ProjectId\" = \"Projects\".\"Id\" AND \"ProjectMembers\".\"UserId\" = \"Projects\".\"OwnerId\"");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_CreatedByUserId",
                table: "ProjectTasks",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_Users_CreatedByUserId",
                table: "ProjectTasks",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_Users_CreatedByUserId",
                table: "ProjectTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_CreatedByUserId",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ProjectMembers");
        }
    }
}
