using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetworkMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSnmpBandwidthMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SnmpMonitoringProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    CredentialId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpMonitoringProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SnmpMonitoringProfiles_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SnmpMonitoringProfiles_NetworkCredentials_CredentialId",
                        column: x => x.CredentialId,
                        principalTable: "NetworkCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SnmpMonitoredInterfaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnmpMonitoringProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    InterfaceIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    InterfaceName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpMonitoredInterfaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SnmpMonitoredInterfaces_SnmpMonitoringProfiles_SnmpMonitoringProfileId",
                        column: x => x.SnmpMonitoringProfileId,
                        principalTable: "SnmpMonitoringProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterfaceTrafficSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnmpMonitoredInterfaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    InOctets = table.Column<long>(type: "INTEGER", nullable: false),
                    OutOctets = table.Column<long>(type: "INTEGER", nullable: false),
                    InBitsPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    OutBitsPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    OperStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SysUpTimeTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    CounterDiscontinuityTicks = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterfaceTrafficSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterfaceTrafficSamples_SnmpMonitoredInterfaces_SnmpMonitoredInterfaceId",
                        column: x => x.SnmpMonitoredInterfaceId,
                        principalTable: "SnmpMonitoredInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceTrafficSamples_SnmpMonitoredInterfaceId_Timestamp",
                table: "InterfaceTrafficSamples",
                columns: new[] { "SnmpMonitoredInterfaceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SnmpMonitoredInterfaces_SnmpMonitoringProfileId_InterfaceIndex",
                table: "SnmpMonitoredInterfaces",
                columns: new[] { "SnmpMonitoringProfileId", "InterfaceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SnmpMonitoringProfiles_CredentialId",
                table: "SnmpMonitoringProfiles",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_SnmpMonitoringProfiles_DeviceId",
                table: "SnmpMonitoringProfiles",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterfaceTrafficSamples");

            migrationBuilder.DropTable(
                name: "SnmpMonitoredInterfaces");

            migrationBuilder.DropTable(
                name: "SnmpMonitoringProfiles");
        }
    }
}
