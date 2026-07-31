using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetworkMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigBackups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigBackups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigBackups_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigBackups_DeviceId_CreatedAt",
                table: "ConfigBackups",
                columns: new[] { "DeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigBackups_IpAddress_CreatedAt",
                table: "ConfigBackups",
                columns: new[] { "IpAddress", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigBackups");
        }
    }
}
