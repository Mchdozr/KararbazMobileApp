using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nereye.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterUserId_IsResolved",
                table: "Reports",
                columns: new[] { "ReporterUserId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCampaigns_Status_TargetIntent_EndAt",
                table: "PromotionCampaigns",
                columns: new[] { "Status", "TargetIntent", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_VenueId_CreatedAt",
                table: "Favorites",
                columns: new[] { "VenueId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentLogs_UserId",
                table: "ConsentLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReporterUserId_IsResolved",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_PromotionCampaigns_Status_TargetIntent_EndAt",
                table: "PromotionCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_VenueId_CreatedAt",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_ConsentLogs_UserId",
                table: "ConsentLogs");
        }
    }
}
