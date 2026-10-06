// Sample — TrippingController.cs (excerpt)
// Handles saving and retrieving relay tripping events.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MySql.Data.MySqlClient;

namespace GridLog.SignalR.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrippingController : ControllerBase
    {
        private readonly string _connStr;
        private readonly IHubContext<GridHub> _hub;

        public TrippingController(IConfiguration config, IHubContext<GridHub> hub)
        {
            _connStr = config.GetConnectionString("MyDbConn")!;
            _hub     = hub;
        }

        // POST api/tripping
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] TrippingSaveRequest req)
        {
            using var conn = new MySqlConnection(_connStr);
            await conn.OpenAsync();

            // Reject exact duplicate (same bay, same OFF time)
            const string dupCheck = @"
                SELECT COUNT(*) FROM tripping_records
                WHERE bay_id = @bayId AND off_datetime = @off";

            using var chkCmd = new MySqlCommand(dupCheck, conn);
            chkCmd.Parameters.AddWithValue("@bayId", req.BayId);
            chkCmd.Parameters.AddWithValue("@off",   req.OffDateTime);
            if (Convert.ToInt32(await chkCmd.ExecuteScalarAsync()) > 0)
                return Conflict(new { message = "Duplicate OFF time for this bay." });

            // Reject if OFF time falls inside an existing open record (no ON time yet)
            var overlap = await OverlapChecker.CheckOffTimeAsync(conn, req.BayId, req.OffDateTime);
            if (overlap != null)
                return Conflict(new { message = overlap });

            const string insert = @"
                INSERT INTO tripping_records
                    (bay_id, relay_type_id, off_datetime, on_datetime,
                     remarks, phase_a, phase_b, phase_c, phase_g, three_phase,
                     I, Ia, Ib, Ic, Ig)
                VALUES
                    (@bayId, @relayTypeId, @off, @on,
                     @remarks, @a, @b, @c, @g, @threePh,
                     @I, @Ia, @Ib, @Ic, @Ig)";

            using var tx  = await conn.BeginTransactionAsync();
            using var cmd = new MySqlCommand(insert, conn, (MySqlTransaction)tx);

            cmd.Parameters.AddWithValue("@bayId",      req.BayId);
            cmd.Parameters.AddWithValue("@relayTypeId",(object?)req.RelayTypeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@off",        req.OffDateTime);
            cmd.Parameters.AddWithValue("@on",         (object?)req.OnDateTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@remarks",    req.Remarks ?? "");
            cmd.Parameters.AddWithValue("@a",          req.PhaseA);
            cmd.Parameters.AddWithValue("@b",          req.PhaseB);
            cmd.Parameters.AddWithValue("@c",          req.PhaseC);
            cmd.Parameters.AddWithValue("@g",          req.PhaseG);
            cmd.Parameters.AddWithValue("@threePh",    req.ThreePhase);
            cmd.Parameters.AddWithValue("@I",          (object?)req.I  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ia",         (object?)req.Ia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ib",         (object?)req.Ib ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ic",         (object?)req.Ic ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ig",         (object?)req.Ig ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            int newId = (int)cmd.LastInsertedId;
            await tx.CommitAsync();

            // Notify all connected clients
            await _hub.Clients.All.SendAsync("PendingChanged");

            return CreatedAtAction(nameof(Save), new { id = newId }, new { id = newId });
        }
    }

    public class TrippingSaveRequest
    {
        public int      BayId       { get; set; }
        public int?     RelayTypeId { get; set; }
        public DateTime OffDateTime { get; set; }   // Nepal local, no offset (Unspecified kind)
        public DateTime? OnDateTime { get; set; }
        public string?  Remarks     { get; set; }
        public bool PhaseA { get; set; }
        public bool PhaseB { get; set; }
        public bool PhaseC { get; set; }
        public bool PhaseG { get; set; }
        public bool ThreePhase { get; set; }
        public double? I  { get; set; }
        public double? Ia { get; set; }
        public double? Ib { get; set; }
        public double? Ic { get; set; }
        public double? Ig { get; set; }
    }
}
