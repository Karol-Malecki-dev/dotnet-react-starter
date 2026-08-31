using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreventConcurrentPendingProjectInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked_pending AS
                (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY "ProjectId", "InvitedUserId"
                            ORDER BY "CreatedAt" DESC, "Id" DESC
                        ) AS invitation_rank
                    FROM "ProjectInvitations"
                    WHERE "Status" = 'Pending'
                )
                UPDATE "ProjectInvitations" AS invitation
                SET
                    "Status" = 'Expired',
                    "ConcurrencyStamp" = md5(
                        random()::text
                        || clock_timestamp()::text
                        || invitation."Id"::text)
                FROM ranked_pending
                WHERE invitation."Id" = ranked_pending."Id"
                    AND ranked_pending.invitation_rank > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInvitations_ProjectId_InvitedUserId_Status",
                table: "ProjectInvitations",
                columns: new[] { "ProjectId", "InvitedUserId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectInvitations_ProjectId_InvitedUserId_Status",
                table: "ProjectInvitations");
        }
    }
}
