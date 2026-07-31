namespace NetworkMonitor.Api.Models;

public static class SnmpOids
{
    public static class System
    {
        public const string Description = "1.3.6.1.2.1.1.1.0";
        public const string ObjectId = "1.3.6.1.2.1.1.2.0";
        public const string UpTime = "1.3.6.1.2.1.1.3.0";
        public const string Contact = "1.3.6.1.2.1.1.4.0";
        public const string Name = "1.3.6.1.2.1.1.5.0";
        public const string Location = "1.3.6.1.2.1.1.6.0";

        public static readonly IReadOnlyList<string> All =
        [
            Description,
            ObjectId,
            UpTime,
            Contact,
            Name,
            Location
        ];
    }

    public static class Interfaces
    {
        public const string Index = "1.3.6.1.2.1.2.2.1.1";
        public const string Description = "1.3.6.1.2.1.2.2.1.2";
        public const string Speed = "1.3.6.1.2.1.2.2.1.5";
        public const string AdminStatus = "1.3.6.1.2.1.2.2.1.7";
        public const string OperStatus = "1.3.6.1.2.1.2.2.1.8";
    }

    public static class Lldp
    {
        // IEEE 802.1AB LLDP-MIB. These roots are fixed by the server; clients cannot provide OIDs.
        public const string LocalPortId = "1.0.8802.1.1.2.1.3.7.1.3";
        public const string RemoteChassisId = "1.0.8802.1.1.2.1.4.1.1.5";
        public const string RemotePortId = "1.0.8802.1.1.2.1.4.1.1.7";
        public const string RemotePortDescription = "1.0.8802.1.1.2.1.4.1.1.8";
        public const string RemoteSystemName = "1.0.8802.1.1.2.1.4.1.1.9";
        public const string RemoteManagementAddress = "1.0.8802.1.1.2.1.4.2.1.2";
    }
}
