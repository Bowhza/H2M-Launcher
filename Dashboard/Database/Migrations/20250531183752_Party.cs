using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dashboard.Database.Migrations
{
    /// <inheritdoc />
    public partial class Party : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartyId = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartySnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartySnapshots_PartyId",
                table: "PartySnapshots",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_PartySnapshots_PartyId_Timestamp",
                table: "PartySnapshots",
                columns: new[] { "PartyId", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartySnapshots_Timestamp",
                table: "PartySnapshots",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartySnapshots");
        }
    }
}
