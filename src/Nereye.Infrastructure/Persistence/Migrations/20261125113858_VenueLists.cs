using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nereye.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VenueLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VenueLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ShareCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VenueListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueListItems_VenueLists_ListId",
                        column: x => x.ListId,
                        principalTable: "VenueLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VenueListItems_ListId_PlaceId",
                table: "VenueListItems",
                columns: new[] { "ListId", "PlaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueLists_ShareCode",
                table: "VenueLists",
                column: "ShareCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueLists_UserId",
                table: "VenueLists",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VenueListItems");

            migrationBuilder.DropTable(
                name: "VenueLists");
        }
    }
}
