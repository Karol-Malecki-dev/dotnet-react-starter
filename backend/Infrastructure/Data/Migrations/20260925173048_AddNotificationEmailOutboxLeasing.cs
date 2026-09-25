using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEmailOutboxLeasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_NextAttemptAt",
                table: "NotificationEmailOutboxMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingLeaseExpiresAt",
                table: "NotificationEmailOutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessingLeaseId",
                table: "NotificationEmailOutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_NextAttemptAt_P~",
                table: "NotificationEmailOutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt", "ProcessingLeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_NextAttemptAt_P~",
                table: "NotificationEmailOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseExpiresAt",
                table: "NotificationEmailOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseId",
                table: "NotificationEmailOutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_NextAttemptAt",
                table: "NotificationEmailOutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" });
        }
    }
}
