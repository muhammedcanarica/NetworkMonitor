using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetworkMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBandwidthThresholdAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incidents_DeviceId_Type",
                table: "Incidents");

            migrationBuilder.AddColumn<double>(
                name: "ObservedBitsPerSecond",
                table: "Incidents",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnmpMonitoredInterfaceId",
                table: "Incidents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ThresholdBitsPerSecond",
                table: "Incidents",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InterfaceBandwidthThresholds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnmpMonitoredInterfaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    InboundThresholdBitsPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    OutboundThresholdBitsPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    BreachSampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RecoverySampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    InboundConsecutiveBreaches = table.Column<int>(type: "INTEGER", nullable: false),
                    OutboundConsecutiveBreaches = table.Column<int>(type: "INTEGER", nullable: false),
                    InboundConsecutiveRecoveries = table.Column<int>(type: "INTEGER", nullable: false),
                    OutboundConsecutiveRecoveries = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterfaceBandwidthThresholds", x => x.Id);
                    table.CheckConstraint("CK_InterfaceBandwidthThresholds_AtLeastOneThreshold", "\"InboundThresholdBitsPerSecond\" IS NOT NULL OR \"OutboundThresholdBitsPerSecond\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_InterfaceBandwidthThresholds_SnmpMonitoredInterfaces_SnmpMonitoredInterfaceId",
                        column: x => x.SnmpMonitoredInterfaceId,
                        principalTable: "SnmpMonitoredInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OpenDeviceType",
                table: "Incidents",
                columns: new[] { "DeviceId", "Type" },
                unique: true,
                filter: "\"Status\" = 'Open' AND \"SnmpMonitoredInterfaceId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OpenInterfaceType",
                table: "Incidents",
                columns: new[] { "DeviceId", "SnmpMonitoredInterfaceId", "Type" },
                unique: true,
                filter: "\"Status\" = 'Open' AND \"SnmpMonitoredInterfaceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_SnmpMonitoredInterfaceId",
                table: "Incidents",
                column: "SnmpMonitoredInterfaceId");

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceBandwidthThresholds_SnmpMonitoredInterfaceId",
                table: "InterfaceBandwidthThresholds",
                column: "SnmpMonitoredInterfaceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_SnmpMonitoredInterfaces_SnmpMonitoredInterfaceId",
                table: "Incidents",
                column: "SnmpMonitoredInterfaceId",
                principalTable: "SnmpMonitoredInterfaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_SnmpMonitoredInterfaces_SnmpMonitoredInterfaceId",
                table: "Incidents");

            migrationBuilder.DropTable(
                name: "InterfaceBandwidthThresholds");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_OpenDeviceType",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_OpenInterfaceType",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_SnmpMonitoredInterfaceId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ObservedBitsPerSecond",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "SnmpMonitoredInterfaceId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ThresholdBitsPerSecond",
                table: "Incidents");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DeviceId_Type",
                table: "Incidents",
                columns: new[] { "DeviceId", "Type" },
                unique: true,
                filter: "\"Status\" = 'Open'");
        }
    }
}
