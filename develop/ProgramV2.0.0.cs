// Program.cs — SupplierQuality (LAN-only) single-folder deploy
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// LAN-only: ascolta su tutte le interfacce, porta 8085
builder.WebHost.UseUrls("http://0.0.0.0:8085");

var app = builder.Build();

// Paths
var root = AppContext.BaseDirectory;
var dataDir = Path.Combine(root, "data");
var backupDir = Path.Combine(dataDir, "backups");

var suppliersFile = Path.Combine(dataDir, "suppliers.json");
var evalsFile = Path.Combine(dataDir, "evaluations.json");
var methodsFile = Path.Combine(dataDir, "methods.json");

Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(backupDir);

// Ensure initial files exist
if (!File.Exists(suppliersFile)) File.WriteAllText(suppliersFile, "[]");
if (!File.Exists(evalsFile)) File.WriteAllText(evalsFile, "[]");
if (!File.Exists(methodsFile)) File.WriteAllText(methodsFile, "[]");

// JSON options
var jsonOpts = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// Basic in-process file locks
var locks = new ConcurrentDictionary<string, SemaphoreSlim>();
SemaphoreSlim LockFor(string path) => locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));

async Task<List<T>> LoadList<T>(string path)
{
    var sem = LockFor(path);
    await sem.WaitAsync();
    try
    {
        var txt = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<T>>(txt, jsonOpts) ?? new List<T>();
    }
    finally { sem.Release(); }
}

async Task SaveList<T>(string path, List<T> list)
{
    var sem = LockFor(path);
    await sem.WaitAsync();
    try
    {
        var tmp = path + ".tmp";
        var txt = JsonSerializer.Serialize(list, jsonOpts);
        await File.WriteAllTextAsync(tmp, txt);
        File.Move(tmp, path, true); // atomic replace
    }
    finally { sem.Release(); }
}

// Backup helper: copia evaluations.json in data/backups prima di ogni scrittura
async Task<(bool ok, string? fileName, string? error)> BackupEvaluationsAsync(string reason)
{
    var sem = LockFor(evalsFile);
    await sem.WaitAsync();
    try
    {
        if (!File.Exists(evalsFile))
            return (false, null, "evaluations.json non trovato");

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var safeReason = string.Concat((reason ?? "backup").Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'));
        if (string.IsNullOrWhiteSpace(safeReason)) safeReason = "backup";

        var fileName = $"evaluations_{stamp}_{now}_{safeReason}.json";
        var dst = Path.Combine(backupDir, fileName);

        File.Copy(evalsFile, dst, true);
        return (true, fileName, null);
    }
    catch (Exception ex)
    {
        return (false, null, ex.Message);
    }
    finally { sem.Release(); }
}

static int ClampInt(int v, int min, int max) => Math.Min(max, Math.Max(min, v));

static bool IsValidPeriod(string? p) =>
    !string.IsNullOrWhiteSpace(p) && p.All(char.IsDigit);

static bool IsValidScores(EvalScores s)
{
    int[] v = { s.Punctuality, s.Quality, s.Documentation, s.Reactivity };
    return v.All(x => x >= 0 && x <= 4);
}

/* =========================
   PESI METODO: RIGIDO = 100
   ========================= */
static bool IsValidWeightsStrict(MethodWeights w)
{
    int[] v = { w.Punctuality, w.Quality, w.Documentation, w.Reactivity };
    if (v.Any(x => x < 0 || x > 100)) return false;
    return v.Sum() == 100; // <<< OBBLIGATORIO
}

// Serve static UI from ./wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// API
app.MapGet("/api/status", () => Results.Json(new { ok = true }, jsonOpts));

/* =========================
   METHODS (tipologie)
   ========================= */
app.MapGet("/api/methods", async () =>
{
    var methods = await LoadList<VerifyMethod>(methodsFile);
    methods = methods
        .OrderBy(m => string.IsNullOrWhiteSpace(m.Name) ? "ZZZ" : m.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(m => m.CreatedAt)
        .ToList();
    return Results.Json(new { ok = true, methods }, jsonOpts);
});

app.MapPost("/api/methods", async (MethodUpsert body) =>
{
    if (string.IsNullOrWhiteSpace(body.Name))
        return Results.BadRequest(new { ok = false, error = "name mancante" });

    if (body.Weights is null)
        return Results.BadRequest(new { ok = false, error = "weights mancanti" });

    // VALIDAZIONE RIGIDA: somma deve essere 100
    if (!IsValidWeightsStrict(body.Weights))
        return Results.BadRequest(new { ok = false, error = "weights non validi (0..100, somma = 100 obbligatoria)" });

    var methods = await LoadList<VerifyMethod>(methodsFile);
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    if (string.IsNullOrWhiteSpace(body.Id))
    {
        var id = Guid.NewGuid().ToString();
        methods.Add(new VerifyMethod(
            id,
            body.Name.Trim(),
            body.Weights,
            string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
            now,
            now
        ));
        await SaveList(methodsFile, methods);
        return Results.Json(new { ok = true, id }, jsonOpts);
    }

    var existingId = body.Id.Trim();
    var idx = methods.FindIndex(m => m.Id == existingId);
    if (idx < 0) return Results.NotFound(new { ok = false, error = "method id non trovato" });

    var m0 = methods[idx];
    methods[idx] = m0 with
    {
        Name = body.Name.Trim(),
        Weights = body.Weights,
        Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
        UpdatedAt = now
    };

    await SaveList(methodsFile, methods);
    return Results.Json(new { ok = true, id = existingId }, jsonOpts);
});

/* =========================
   SUPPLIERS
   ========================= */
app.MapGet("/api/suppliers", async () =>
{
    var suppliers = await LoadList<Supplier>(suppliersFile);
    return Results.Json(new { ok = true, suppliers }, jsonOpts);
});

app.MapPost("/api/suppliers", async (SupplierUpsert body) =>
{
    if (string.IsNullOrWhiteSpace(body.Name))
        return Results.BadRequest(new { ok = false, error = "name mancante" });

    // methodId può essere null o stringa; se valorizzato, deve esistere
    string? methodId = string.IsNullOrWhiteSpace(body.MethodId) ? null : body.MethodId.Trim();
    if (!string.IsNullOrWhiteSpace(methodId))
    {
        var methods = await LoadList<VerifyMethod>(methodsFile);
        if (!methods.Any(m => m.Id == methodId))
            return Results.BadRequest(new { ok = false, error = "methodId non valido (metodo non trovato)" });
    }

    var suppliers = await LoadList<Supplier>(suppliersFile);
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    if (string.IsNullOrWhiteSpace(body.Id))
    {
        var id = Guid.NewGuid().ToString();
        suppliers.Add(new Supplier(
            id,
            body.Name.Trim(),
            string.IsNullOrWhiteSpace(body.Ref) ? null : body.Ref.Trim(),
            string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
            methodId,
            now,
            now
        ));
        await SaveList(suppliersFile, suppliers);
        return Results.Json(new { ok = true, id }, jsonOpts);
    }

    var existingId = body.Id.Trim();
    var idx = suppliers.FindIndex(s => s.Id == existingId);
    if (idx < 0) return Results.NotFound(new { ok = false, error = "supplier id non trovato" });

    var s0 = suppliers[idx];
    suppliers[idx] = s0 with
    {
        Name = body.Name.Trim(),
        Ref = string.IsNullOrWhiteSpace(body.Ref) ? null : body.Ref.Trim(),
        Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
        MethodId = methodId,
        UpdatedAt = now
    };

    await SaveList(suppliersFile, suppliers);
    return Results.Json(new { ok = true, id = existingId }, jsonOpts);
});

/* =========================
   EVALUATIONS
   ========================= */
app.MapGet("/api/evaluations", async (string supplierId) =>
{
    if (string.IsNullOrWhiteSpace(supplierId))
        return Results.BadRequest(new { ok = false, error = "supplierId mancante" });

    var evals = await LoadList<Evaluation>(evalsFile);
    var rows = evals
        .Where(e => e.SupplierId == supplierId)
        .OrderBy(e => int.TryParse(e.Period, out var p) ? p : int.MaxValue)
        .ThenBy(e => e.CreatedAt)
        .ToList();

    return Results.Json(new { ok = true, evaluations = rows }, jsonOpts);
});

// BACKUP manuale (opzionale)
app.MapPost("/api/evaluations/backup", async () =>
{
    var (ok, fileName, error) = await BackupEvaluationsAsync("manual");
    if (!ok) return Results.Problem(title: "backup failed", detail: error);
    return Results.Json(new { ok = true, file = fileName }, jsonOpts);
});

// INSERT: blocca doppioni (supplierId + period) e fa backup automatico
app.MapPost("/api/evaluations", async (EvaluationAdd body) =>
{
    if (string.IsNullOrWhiteSpace(body.SupplierId))
        return Results.BadRequest(new { ok = false, error = "supplierId mancante" });

    if (!IsValidPeriod(body.Period))
        return Results.BadRequest(new { ok = false, error = "periodo non valido" });

    if (!IsValidScores(body.Scores))
        return Results.BadRequest(new { ok = false, error = "scores fuori range 0..4" });

    var supplierId = body.SupplierId.Trim();
    var period = body.Period.Trim();

    var evals = await LoadList<Evaluation>(evalsFile);

    var exists = evals.Any(e => e.SupplierId == supplierId && e.Period == period);
    if (exists)
        return Results.Conflict(new { ok = false, error = $"Esiste già una valutazione per il periodo {period}. Usa Correggi." });

    var b = await BackupEvaluationsAsync("pre_post");
    if (!b.ok)
        return Results.Problem(title: "backup failed", detail: b.error);

    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    evals.Add(new Evaluation(
        supplierId,
        period,
        now,
        body.Scores,
        string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim()
    ));

    await SaveList(evalsFile, evals);
    return Results.Json(new { ok = true, backup = b.fileName }, jsonOpts);
});

// CORREZIONE: sovrascrive record esistente (supplierId + period) e fa backup automatico
app.MapPut("/api/evaluations", async (EvaluationAdd body) =>
{
    if (string.IsNullOrWhiteSpace(body.SupplierId))
        return Results.BadRequest(new { ok = false, error = "supplierId mancante" });

    if (!IsValidPeriod(body.Period))
        return Results.BadRequest(new { ok = false, error = "periodo non valido" });

    if (!IsValidScores(body.Scores))
        return Results.BadRequest(new { ok = false, error = "scores fuori range 0..4" });

    var supplierId = body.SupplierId.Trim();
    var period = body.Period.Trim();

    var evals = await LoadList<Evaluation>(evalsFile);
    var idx = evals.FindIndex(e => e.SupplierId == supplierId && e.Period == period);
    if (idx < 0)
        return Results.NotFound(new { ok = false, error = $"Valutazione non trovata per periodo {period}" });

    var b = await BackupEvaluationsAsync("pre_put");
    if (!b.ok)
        return Results.Problem(title: "backup failed", detail: b.error);

    var createdAtOriginal = evals[idx].CreatedAt;

    evals[idx] = new Evaluation(
        supplierId,
        period,
        createdAtOriginal,
        body.Scores,
        string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim()
    );

    await SaveList(evalsFile, evals);
    return Results.Json(new { ok = true, backup = b.fileName }, jsonOpts);
});

// =========================
// SHUTDOWN (solo localhost)
// =========================
app.MapPost("/api/shutdown", (HttpContext ctx) =>
{
    // consenti solo richieste locali (127.0.0.1 / ::1)
    var ip = ctx.Connection.RemoteIpAddress;
    var isLocal =
        ip is not null &&
        (System.Net.IPAddress.IsLoopback(ip) ||
         string.Equals(ip.ToString(), "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(ip.ToString(), "::1", StringComparison.OrdinalIgnoreCase));

    if (!isLocal)
        return Results.Unauthorized();

    // rispondi subito, poi ferma il server
    _ = Task.Run(async () =>
    {
        await Task.Delay(250);
        try { app.Lifetime.StopApplication(); } catch { }
    });

    return Results.Json(new { ok = true }, jsonOpts);
});


app.Run();

// --- Types (records) ---
record Supplier(
    string Id,
    string Name,
    string? Ref,
    string? Notes,
    string? MethodId,   // << associazione metodo di verifica
    long CreatedAt,
    long UpdatedAt
);

record EvalScores(int Punctuality, int Quality, int Documentation, int Reactivity);
record Evaluation(string SupplierId, string Period, long CreatedAt, EvalScores Scores, string? Note);

record SupplierUpsert(string? Id, string Name, string? Ref, string? Notes, string? MethodId);
record EvaluationAdd(string SupplierId, string Period, EvalScores Scores, string? Note);

// metodi
record MethodWeights(int Punctuality, int Quality, int Documentation, int Reactivity);

record VerifyMethod(
    string Id,
    string Name,
    MethodWeights Weights,
    string? Notes,
    long CreatedAt,
    long UpdatedAt
);

record MethodUpsert(string? Id, string Name, MethodWeights? Weights, string? Notes);
