// Program.cs — SupplierQuality (LAN-only) single-folder deploy
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;

// PDF (MigraDocCore + PdfSharpCore backend via MigraDocCore.Rendering)
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.DocumentObjectModel.Tables;
using MigraDocCore.Rendering;

var builder = WebApplication.CreateBuilder(args);

// LAN-only: ascolta su tutte le interfacce, porta 8085
builder.WebHost.UseUrls("http://0.0.0.0:8085");

var app = builder.Build();

// ============ VERSIONE APP (usata anche nel PDF)
const string APP_VERSION = "v3.0.0"; // <-- bump quando rilasci

// Paths
var root = AppContext.BaseDirectory;
var dataDir = Path.Combine(root, "data");
var backupDir = Path.Combine(dataDir, "backups");

var suppliersFile = Path.Combine(dataDir, "suppliers.json");
var evalsFile = Path.Combine(dataDir, "evaluations.json");
var methodsFile = Path.Combine(dataDir, "methods.json");

// asset (logo PNG)
var assetDir = Path.Combine(root, "asset");
var logoPath = Path.Combine(assetDir, "logo.png");

Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(backupDir);
Directory.CreateDirectory(assetDir);

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

/* =========================
   SCORE HELPERS (come UI)
   ========================= */
static int CalcOverall1000(EvalScores s, MethodWeights w)
{
    double pPct = (ClampInt(s.Punctuality, 0, 4) / 4.0) * 100.0;
    double qPct = (ClampInt(s.Quality, 0, 4) / 4.0) * 100.0;
    double dPct = (ClampInt(s.Documentation, 0, 4) / 4.0) * 100.0;
    double rPct = (ClampInt(s.Reactivity, 0, 4) / 4.0) * 100.0;

    double overall =
        (pPct * w.Punctuality +
         qPct * w.Quality +
         dPct * w.Documentation +
         rPct * w.Reactivity) / 100.0;

    return (int)Math.Round(overall * 10.0); // 0..1000
}

static string StatusBaseFromScore(int v)
{
    if (v >= 850) return "Eccellente";
    if (v >= 700) return "Buono";
    if (v >= 550) return "Sufficiente";
    if (v > 0) return "Critico";
    return "N/D";
}

static string TrendFromDelta(int d)
{
    if (d >= 80) return " (in forte miglioramento)";
    if (d >= 30) return " (in miglioramento)";
    if (d <= -80) return " (in forte peggioramento)";
    if (d <= -30) return " (in peggioramento)";
    return " (stabile)";
}

static string LevelName(int v) => v switch
{
    0 => "N/D",
    1 => "Critico",
    2 => "Sufficiente",
    3 => "Buono",
    4 => "Eccellente",
    _ => "?" + v
};

/* =========================
   LIMIT helper (OBBLIGATORIO)
   ========================= */
static int RequireLimit(int? limit)
{
    if (limit is null)
        throw new ArgumentException("limit mancante. Specifica ?limit=...");

    // 1..5000
    return Math.Min(5000, Math.Max(1, limit.Value));
}

/* =========================
   PDF BUILDER (logo PNG da asset/logo.png)
   - compatibile con MigraDocCore che richiede IImageSource
   ========================= */
static byte[] BuildSupplierReportPdf(
    Supplier supplier,
    VerifyMethod? method,
    MethodWeights weights,
    List<Evaluation> evals,
    string appVersion,
    string logoFilePath
)
{
    var doc = new Document();
    doc.Info.Title = "Supplier Quality — Report Fornitore";
    doc.Info.Subject = supplier.Name;
    doc.Info.Author = "Supplier Quality (LAN)";

    var style = doc.Styles["Normal"];
    style.Font.Name = "Verdana";
    style.Font.Size = 10;

    var sec = doc.AddSection();
    sec.PageSetup.TopMargin = Unit.FromCentimeter(1.6);
    sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.6);
    sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
    sec.PageSetup.RightMargin = Unit.FromCentimeter(1.6);

    // HEADER: logo + titolo
    {
        var ht = sec.AddTable();
        ht.Borders.Width = 0;
        ht.AddColumn(Unit.FromCentimeter(3.2));
        ht.AddColumn(Unit.FromCentimeter(13.8));

        var r = ht.AddRow();
        r.TopPadding = 0;
        r.BottomPadding = Unit.FromCentimeter(0.1);

        // Logo (se presente) — robusto
        try
        {
            if (!string.IsNullOrWhiteSpace(logoFilePath) && File.Exists(logoFilePath))
            {
                var imgSrc = MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource
                    .FromStream(
                        "logo.png",
                        () => File.OpenRead(logoFilePath),
                        75
                    );

                var img = r.Cells[0].AddImage(imgSrc);
                img.LockAspectRatio = true;
                img.Width = Unit.FromCentimeter(2.8);
            }
            else
            {
                r.Cells[0].AddParagraph("");
            }
        }
        catch
        {
            r.Cells[0].AddParagraph("");
        }

        var p = r.Cells[1].AddParagraph("Supplier Quality — Report Fornitore");
        p.Format.Font.Size = 18;
        p.Format.Font.Bold = true;
        p.Format.SpaceAfter = Unit.FromCentimeter(0.1);

        var meta = r.Cells[1].AddParagraph();
        meta.AddFormattedText("Generato il: ", TextFormat.Bold);
        meta.AddText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        meta.AddText("   •   ");
        meta.AddFormattedText("Versione: ", TextFormat.Bold);
        meta.AddText(appVersion);
        meta.Format.SpaceAfter = Unit.FromCentimeter(0.4);
    }

    void AddLine(string label, string value)
    {
        var p = sec.AddParagraph();
        p.AddFormattedText(label + ": ", TextFormat.Bold);
        p.AddText(string.IsNullOrWhiteSpace(value) ? "—" : value);
        p.Format.SpaceAfter = 3;
    }

    AddLine("Fornitore", supplier.Name);
    AddLine("ID", supplier.Id);
    AddLine("Riferimento", supplier.Ref ?? "—");
    AddLine("Note", supplier.Notes ?? "—");

    AddLine("Metodo verifica", method?.Name ?? "(Default)");
    AddLine("Pesi", $"P={weights.Punctuality}%  Q={weights.Quality}%  D={weights.Documentation}%  R={weights.Reactivity}%");

    sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.2);

    // Ordina evals
    var ordered = evals
        .OrderBy(e => int.TryParse(e.Period, out var pp) ? pp : int.MaxValue)
        .ThenBy(e => e.CreatedAt)
        .ToList();

    // Sintesi
    var stTitle = sec.AddParagraph("Sintesi");
    stTitle.Format.Font.Bold = true;
    stTitle.Format.Font.Size = 12;
    stTitle.Format.SpaceAfter = Unit.FromCentimeter(0.2);

    if (ordered.Count == 0)
    {
        var no = sec.AddParagraph("Nessuna valutazione registrata.");
        no.Format.SpaceAfter = Unit.FromCentimeter(0.4);
    }
    else
    {
        var latest = ordered[^1];
        var latestScore = CalcOverall1000(latest.Scores, weights);

        int? prevScore = null;
        int delta = 0;
        if (ordered.Count >= 2)
        {
            prevScore = CalcOverall1000(ordered[^2].Scores, weights);
            delta = latestScore - prevScore.Value;
        }

        var status = StatusBaseFromScore(latestScore) +
                     (prevScore is null ? " (primo periodo)" : TrendFromDelta(delta));

        AddLine("Ultimo periodo", latest.Period);
        AddLine("Punteggio", prevScore is null ? $"{latestScore}" : $"{prevScore} → {latestScore} (Δ {delta:+#;-#;0})");
        AddLine("Stato", status);

        sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.2);
    }

    // Tabella
    var tblTitle = sec.AddParagraph("Valutazioni (per periodo)");
    tblTitle.Format.Font.Bold = true;
    tblTitle.Format.Font.Size = 12;
    tblTitle.Format.SpaceAfter = Unit.FromCentimeter(0.2);

    var t = sec.AddTable();
    t.Borders.Width = 0.5;

    // colonne bilanciate (Punteggio più largo)
    t.AddColumn(Unit.FromCentimeter(2.0));  // Periodo
    t.AddColumn(Unit.FromCentimeter(2.4));  // Punteggio
    t.AddColumn(Unit.FromCentimeter(1.2));  // Δ
    t.AddColumn(Unit.FromCentimeter(2.3));  // P
    t.AddColumn(Unit.FromCentimeter(2.3));  // Q
    t.AddColumn(Unit.FromCentimeter(2.3));  // D
    t.AddColumn(Unit.FromCentimeter(2.3));  // R

    var head = t.AddRow();
    head.Shading.Color = Colors.LightGray;
    head.Format.Font.Bold = true;
    head.Format.Font.Size = 9;
    head.VerticalAlignment = VerticalAlignment.Center;

    head.Cells[0].AddParagraph("Periodo");
    head.Cells[1].AddParagraph("Punteggio");
    head.Cells[2].AddParagraph("Δ");
    head.Cells[3].AddParagraph("Puntualità");
    head.Cells[4].AddParagraph("Qualità");
    head.Cells[5].AddParagraph("Doc.");
    head.Cells[6].AddParagraph("Reatt.");

    for (int i = 0; i < 7; i++)
    {
        head.Cells[i].Format.Alignment = ParagraphAlignment.Center;
        head.Cells[i].VerticalAlignment = VerticalAlignment.Center;
        head.Cells[i].Format.SpaceBefore = 1;
        head.Cells[i].Format.SpaceAfter = 1;
    }

    int? lastScore = null;
    foreach (var e in ordered)
    {
        int score = CalcOverall1000(e.Scores, weights);
        int d = lastScore is null ? 0 : (score - lastScore.Value);
        lastScore = score;

        var r = t.AddRow();

        r.Cells[0].AddParagraph(e.Period);
        r.Cells[1].AddParagraph(score.ToString());
        r.Cells[2].AddParagraph(d.ToString("+0;-0;0"));
        r.Cells[3].AddParagraph(LevelName(e.Scores.Punctuality));
        r.Cells[4].AddParagraph(LevelName(e.Scores.Quality));
        r.Cells[5].AddParagraph(LevelName(e.Scores.Documentation));
        r.Cells[6].AddParagraph(LevelName(e.Scores.Reactivity));

        r.Cells[0].Format.Alignment = ParagraphAlignment.Center;
        r.Cells[1].Format.Alignment = ParagraphAlignment.Center;
        r.Cells[2].Format.Alignment = ParagraphAlignment.Center;
    }

    sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.3);

    var foot = sec.AddParagraph("Nota: report generato localmente (LAN). I dati provengono dai file JSON in /data.");
    foot.Format.Font.Size = 8;
    foot.Format.Font.Color = Colors.Gray;

    var renderer = new PdfDocumentRenderer(unicode: true);
    renderer.Document = doc;
    renderer.RenderDocument();

    using var ms = new MemoryStream();
    renderer.PdfDocument.Save(ms, false);
    return ms.ToArray();
}

static MethodWeights GetSupplierWeightsOrDefault(Supplier s, List<VerifyMethod> methods)
{
    var mid = (s.MethodId ?? "").Trim();
    if (string.IsNullOrWhiteSpace(mid)) return new MethodWeights(25, 25, 25, 25);

    var m = methods.FirstOrDefault(x => x.Id == mid);
    if (m is null) return new MethodWeights(25, 25, 25, 25);

    var w = m.Weights;
    if (!IsValidWeightsStrict(w)) return new MethodWeights(25, 25, 25, 25);
    return w;
}

// Serve static UI from ./wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// API
app.MapGet("/api/status", () => Results.Json(new { ok = true, version = APP_VERSION }, jsonOpts));

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
// GET valutazioni con limit (OBBLIGATORIO)
app.MapGet("/api/evaluations", async (string supplierId, int? limit) =>
{
    if (string.IsNullOrWhiteSpace(supplierId))
        return Results.BadRequest(new { ok = false, error = "supplierId mancante" });

    supplierId = supplierId.Trim();

    int lim;
    try { lim = RequireLimit(limit); }
    catch (Exception ex) { return Results.BadRequest(new { ok = false, error = ex.Message }); }

    var evals = await LoadList<Evaluation>(evalsFile);

    // Ordina crescente per periodo e poi createdAt, ma applica limit come "ultime N"
    var ordered = evals
        .Where(e => e.SupplierId == supplierId)
        .OrderBy(e => int.TryParse(e.Period, out var p) ? p : int.MaxValue)
        .ThenBy(e => e.CreatedAt)
        .ToList();

    // prendi le ultime N mantenendo l’ordine cronologico
    if (ordered.Count > lim)
        ordered = ordered.Skip(ordered.Count - lim).ToList();

    return Results.Json(new { ok = true, limit = lim, total = ordered.Count, evaluations = ordered }, jsonOpts);
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

/* =========================
   REPORT PDF (limit OBBLIGATORIO)
   ========================= */
app.MapGet("/api/report", async (string supplierId, int? limit) =>
{
    if (string.IsNullOrWhiteSpace(supplierId))
        return Results.BadRequest(new { ok = false, error = "supplierId mancante" });

    supplierId = supplierId.Trim();

    int lim;
    try { lim = RequireLimit(limit); }
    catch (Exception ex) { return Results.BadRequest(new { ok = false, error = ex.Message }); }

    var suppliers = await LoadList<Supplier>(suppliersFile);
    var s = suppliers.FirstOrDefault(x => x.Id == supplierId);
    if (s is null)
        return Results.NotFound(new { ok = false, error = "supplier non trovato" });

    var methods = await LoadList<VerifyMethod>(methodsFile);
    var weights = GetSupplierWeightsOrDefault(s, methods);

    VerifyMethod? method = null;
    if (!string.IsNullOrWhiteSpace(s.MethodId))
        method = methods.FirstOrDefault(m => m.Id == s.MethodId);

    var evals = await LoadList<Evaluation>(evalsFile);
    var rows = evals
        .Where(e => e.SupplierId == supplierId)
        .OrderBy(e => int.TryParse(e.Period, out var p) ? p : int.MaxValue)
        .ThenBy(e => e.CreatedAt)
        .ToList();

    // limit come “ultime N”
    if (rows.Count > lim)
        rows = rows.Skip(rows.Count - lim).ToList();

    var pdf = BuildSupplierReportPdf(
        supplier: s,
        method: method,
        weights: weights,
        evals: rows,
        appVersion: APP_VERSION,
        logoFilePath: logoPath
    );

    var safeName = string.Concat((s.Name ?? "fornitore").Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'));
    if (string.IsNullOrWhiteSpace(safeName)) safeName = "fornitore";

    var fileName = $"SupplierQuality_Report_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
    return Results.File(pdf, "application/pdf", fileName);
});

// =========================
// SHUTDOWN (solo localhost)
// =========================
app.MapPost("/api/shutdown", (HttpContext ctx) =>
{
    var ip = ctx.Connection.RemoteIpAddress;
    var isLocal =
        ip is not null &&
        (System.Net.IPAddress.IsLoopback(ip) ||
         string.Equals(ip.ToString(), "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(ip.ToString(), "::1", StringComparison.OrdinalIgnoreCase));

    if (!isLocal)
        return Results.Unauthorized();

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
    string? MethodId,
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