using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEmailOutboxDeadLetterStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_NextAttemptAt_P~",
                table: "NotificationEmailOutboxMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "NotificationEmailOutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "NotificationEmailOutboxMessages"
                SET "DeadLetteredAt" = CURRENT_TIMESTAMP
                WHERE "ProcessedAt" IS NULL
                  AND "DeadLetteredAt" IS NULL
                  AND "AttemptCount" >= 3;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_DeadLetteredAt_~",
                table: "NotificationEmailOutboxMessages",
                columns: new[] { "ProcessedAt", "DeadLetteredAt", "NextAttemptAt", "ProcessingLeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_DeadLetteredAt_~",
                table: "NotificationEmailOutboxMessages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "NotificationEmailOutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEmailOutboxMessages_ProcessedAt_NextAttemptAt_P~",
                table: "NotificationEmailOutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt", "ProcessingLeaseExpiresAt" });
        }
    }
}
