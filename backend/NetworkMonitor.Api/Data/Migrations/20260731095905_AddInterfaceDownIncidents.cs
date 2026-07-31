using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetworkMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterfaceDownIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveDownSamples",
                table: "SnmpMonitoredInterfaces",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveUpSamples",
                table: "SnmpMonitoredInterfaces",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastOperationalState",
                table: "SnmpMonitoredInterfaces",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminStatus",
                table: "InterfaceTrafficSamples",
                type: "TEXT",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsecutiveDownSamples",
                table: "SnmpMonitoredInterfaces");

            migrationBuilder.DropColumn(
                name: "ConsecutiveUpSamples",
                table: "SnmpMonitoredInterfaces");

            migrationBuilder.DropColumn(
                name: "LastOperationalState",
                table: "SnmpMonitoredInterfaces");

            migrationBuilder.DropColumn(
                name: "AdminStatus",
                table: "InterfaceTrafficSamples");
        }
    }
}
