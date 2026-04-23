using RestaurantPosWpf;

Console.WriteLine("Postgres smoke test (PosDataAccess + App.config)");
Console.WriteLine($"App base: {AppContext.BaseDirectory}");

try
{
    var snap = PosDataAccess.CurrentSnapshot;
    Console.WriteLine($"Driver: {snap.Driver}");
    Console.WriteLine(
        $"Compact connection: {(string.IsNullOrEmpty(snap.CompactConnectionString) ? "(empty)" : "(present, not printed)")}");

    var pda = new PosDataAccess();
    if (!pda.CheckCurrentConnection())
    {
        Console.Error.WriteLine("FAIL: compact connection string from config is missing or invalid (need 5 parts).");
        Environment.Exit(1);
    }

    var dt = pda.GetDataTable("SELECT 1::integer AS ok", 15);
    if (dt.Rows.Count == 0)
    {
        Console.Error.WriteLine("FAIL: query returned no rows.");
        Environment.Exit(1);
    }

    var v = dt.Rows[0]["ok"];
    Console.WriteLine($"OK: SELECT 1 returned {v} (PostgreSQL reachable via ODBC).");
    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.Error.WriteLine("FAIL: " + ex.Message);
    if (ex.InnerException != null)
        Console.Error.WriteLine("      " + ex.InnerException.Message);
    Environment.Exit(1);
}
