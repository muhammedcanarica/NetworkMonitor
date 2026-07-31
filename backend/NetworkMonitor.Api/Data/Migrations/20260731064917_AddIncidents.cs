using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetworkMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ResolvedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DeviceId_Status_StartedAt",
                table: "Incidents",
                columns: new[] { "DeviceId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DeviceId_Type",
                table: "Incidents",
                columns: new[] { "DeviceId", "Type" },
                unique: true,
                filter: "\"Status\" = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Incidents");
        }
    }
}
