// Sample — TrippingRecord.cs
// Client-side model used by WPFGridLog-1 for display and API communication.

namespace WPFGridLog_1.Models
{
    public class TrippingRecord
    {
        public int      Id          { get; set; }
        public int      BayId       { get; set; }
        public string   BayName     { get; set; } = "";
        public int?     RelayTypeId { get; set; }
        public string   RelayName   { get; set; } = "";

        // Stored as plain Nepal local DateTime (DateTimeKind.Unspecified).
        // Never serialized with timezone offset — avoids server-side conversion.
        public DateTime  OffDateTime { get; set; }
        public DateTime? OnDateTime  { get; set; }

        public int?   TotalTime { get; set; }
        public string Remarks   { get; set; } = "";

        // Phase flags
        public bool PhaseA     { get; set; }
        public bool PhaseB     { get; set; }
        public bool PhaseC     { get; set; }
        public bool PhaseG     { get; set; }
        public bool ThreePhase { get; set; }

        // Fault currents
        public double? I  { get; set; }
        public double? Ia { get; set; }
        public double? Ib { get; set; }
        public double? Ic { get; set; }
        public double? Ig { get; set; }

        public string FaultType { get; set; } = "TEMPORARY";
    }
}
