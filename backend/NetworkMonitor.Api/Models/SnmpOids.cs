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
}
