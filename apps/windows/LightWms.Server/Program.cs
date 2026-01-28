using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using LightWms.Core.Models;
using LightWms.Core.Services;
using LightWms.Data;
using LightWms.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

var dbPath = ResolveDbPath(builder.Configuration);

builder.Services.AddSingleton<SqliteDataStore>(sp =>
{
    var directory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
    var store = new SqliteDataStore(dbPath);
    store.Initialize();
    return store;
});
builder.Services.AddSingleton<LightWms.Core.Abstractions.IDataStore>(sp => sp.GetRequiredService<SqliteDataStore>());
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton(new ApiDocStore(dbPath));

var app = builder.Build();

app.UseHttpsRedirection();

LogDbInfo(app.Logger, dbPath);

app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();

    var path = context.Request.Path.Value ?? "/";
    if (!string.IsNullOrWhiteSpace(context.Request.QueryString.Value))
    {
        path += context.Request.QueryString.Value;
    }

    app.Logger.LogInformation(
        "{Method} {Path} => {StatusCode} in {Elapsed}ms",
        context.Request.Method,
        path,
        context.Response.StatusCode,
        sw.ElapsedMilliseconds);
});

var tsdRoot = ServerPaths.TsdRoot;
var tsdIndexPath = Path.Combine(tsdRoot, "index.html");
if (Directory.Exists(tsdRoot))
{
    var fileProvider = new PhysicalFileProvider(tsdRoot);
    var contentTypeProvider = new FileExtensionContentTypeProvider();
    contentTypeProvider.Mappings[".jsonl"] = "application/x-ndjson";
    contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        ContentTypeProvider = contentTypeProvider
    });
}

app.MapGet("/api/ping", () =>
{
    return Results.Ok(new
    {
        ok = true,
        server_time = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
    });
});

app.MapGet("/api/diag/db", () =>
{
    var info = BuildDbInfo(dbPath);
    return Results.Ok(info);
});

app.MapGet("/api/diag/counts", () =>
{
    using var connection = OpenConnection(dbPath);
    return Results.Ok(new
    {
        items = CountTable(connection, "items"),
        locations = CountTable(connection, "locations"),
        partners = CountTable(connection, "partners"),
        orders = CountTable(connection, "orders"),
        docs = CountTable(connection, "docs"),
        ledger = CountTable(connection, "ledger")
    });
});

app.MapGet("/api/locations", (SqliteDataStore store) =>
{
    var locations = store.GetLocations()
        .Select(location => new { id = location.Id, code = location.Code, name = location.Name })
        .ToList();
    return Results.Ok(locations);
});

app.MapGet("/api/items/by-barcode/{barcode}", (string barcode, SqliteDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(barcode))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_BARCODE"));
    }

    var trimmed = barcode.Trim();
    var item = store.FindItemByBarcode(trimmed) ?? FindItemByBarcodeVariant(store, trimmed);
    if (item == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new
    {
        id = item.Id,
        name = item.Name,
        barcode = item.Barcode,
        gtin = item.Gtin,
        base_uom_code = item.BaseUom
    });
});

app.MapGet("/api/stock", () =>
{
    using var connection = OpenConnection(dbPath);
    using var command = connection.CreateCommand();
    command.CommandText = @"
SELECT item_id, location_id, COALESCE(SUM(qty_delta), 0) AS qty
FROM ledger
GROUP BY item_id, location_id
HAVING qty != 0
ORDER BY item_id, location_id;";
    using var reader = command.ExecuteReader();
    var rows = new List<object>();
    while (reader.Read())
    {
        rows.Add(new
        {
            item_id = reader.GetInt64(0),
            location_id = reader.GetInt64(1),
            qty = reader.GetDouble(2)
        });
    }

    return Results.Ok(rows);
});

app.MapGet("/api/hu-stock", (SqliteDataStore store) =>
{
    var rows = store.GetHuStockRows()
        .Select(row => new
        {
            hu = row.HuCode,
            item_id = row.ItemId,
            location_id = row.LocationId,
            qty = row.Qty
        })
        .ToList();

    return Results.Ok(rows);
});

app.MapGet("/api/hus", (HttpRequest request) =>
{
    var takeText = request.Query["take"].ToString();
    var take = 200;
    if (!string.IsNullOrWhiteSpace(takeText) && int.TryParse(takeText, out var parsed))
    {
        take = parsed;
    }

    if (take < 1)
    {
        take = 1;
    }
    if (take > 1000)
    {
        take = 1000;
    }

    using var connection = OpenConnection(dbPath);
    using var command = connection.CreateCommand();
    command.CommandText = @"
SELECT id, hu_code, status, created_at, created_by, closed_at, note
FROM hus
ORDER BY id DESC
LIMIT @take;";
    command.Parameters.AddWithValue("@take", take);
    using var reader = command.ExecuteReader();
    var list = new List<object>();
    while (reader.Read())
    {
        list.Add(new
        {
            id = reader.GetInt64(0),
            hu_code = reader.GetString(1),
            status = reader.GetString(2),
            created_at = reader.GetString(3),
            created_by = reader.IsDBNull(4) ? null : reader.GetString(4),
            closed_at = reader.IsDBNull(5) ? null : reader.GetString(5),
            note = reader.IsDBNull(6) ? null : reader.GetString(6)
        });
    }

    return Results.Ok(list);
});

app.MapGet("/api/hus/{huCode}", (string huCode, SqliteDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(huCode))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_HU"));
    }

    var normalized = NormalizeHu(huCode);
    if (string.IsNullOrWhiteSpace(normalized))
    {
        return Results.BadRequest(new ApiResult(false, "INVALID_HU"));
    }

    var record = store.GetHuByCode(normalized);
    if (record == null)
    {
        return Results.Ok(new ApiResult(false, "UNKNOWN_HU"));
    }

    return Results.Ok(new
    {
        ok = true,
        hu = new
        {
            id = record.Id,
            hu_code = record.Code,
            status = record.Status,
            created_at = record.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            created_by = record.CreatedBy,
            closed_at = record.ClosedAt?.ToString("O", CultureInfo.InvariantCulture),
            note = record.Note
        }
    });
});

app.MapPost("/api/hus/generate", (HuGenerateRequest request) =>
{
    var count = request.Count;
    if (count < 1 || count > 1000)
    {
        return Results.BadRequest(new ApiResult(false, "INVALID_COUNT"));
    }

    var createdBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? null : request.CreatedBy.Trim();
    var createdAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
    var codes = new List<string>(count);

    using var connection = OpenConnection(dbPath);
    using var transaction = connection.BeginTransaction();
    try
    {
        for (var i = 0; i < count; i++)
        {
            var tmpCode = "TMP-" + Guid.NewGuid().ToString("N");
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
INSERT INTO hus(hu_code, status, created_at, created_by)
VALUES(@hu_code, 'OPEN', @created_at, @created_by);
SELECT last_insert_rowid();
";
            insert.Parameters.AddWithValue("@hu_code", tmpCode);
            insert.Parameters.AddWithValue("@created_at", createdAt);
            insert.Parameters.AddWithValue("@created_by", createdBy ?? (object)DBNull.Value);
            var id = (long)(insert.ExecuteScalar() ?? 0L);
            var huCode = $"HU-{id:000000}";

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE hus SET hu_code = @hu_code WHERE id = @id;";
            update.Parameters.AddWithValue("@hu_code", huCode);
            update.Parameters.AddWithValue("@id", id);
            update.ExecuteNonQuery();

            codes.Add(huCode);
        }

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }

    return Results.Ok(new { ok = true, hus = codes });
});

app.MapPost("/api/ops", async (HttpRequest request, SqliteDataStore store, DocumentService docs, ApiDocStore apiStore) =>
{
    string rawJson;
    using (var reader = new StreamReader(request.Body))
    {
        rawJson = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrWhiteSpace(rawJson))
    {
        return Results.BadRequest(new ApiResult(false, "EMPTY_BODY"));
    }

    OperationEventRequest? opEvent;
    try
    {
        opEvent = JsonSerializer.Deserialize<OperationEventRequest>(
            rawJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (JsonException)
    {
        return Results.BadRequest(new ApiResult(false, "INVALID_JSON"));
    }

    if (opEvent == null)
    {
        return Results.BadRequest(new ApiResult(false, "INVALID_JSON"));
    }

    if (string.IsNullOrWhiteSpace(opEvent.EventId))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_EVENT_ID"));
    }

    if (apiStore.IsEventProcessed(opEvent.EventId))
    {
        return Results.Ok(new ApiResult(true));
    }

    if (!string.Equals(opEvent.Op, "MOVE", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new ApiResult(false, "UNSUPPORTED_OP"));
    }

    if (string.IsNullOrWhiteSpace(opEvent.DocRef))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_DOC_REF"));
    }

    if (string.IsNullOrWhiteSpace(opEvent.Barcode))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_BARCODE"));
    }

    if (opEvent.Qty <= 0)
    {
        return Results.BadRequest(new ApiResult(false, "INVALID_QTY"));
    }

    var barcode = opEvent.Barcode.Trim();
    var item = store.FindItemByBarcode(barcode) ?? FindItemByBarcodeVariant(store, barcode);
    if (item == null)
    {
        return Results.BadRequest(new ApiResult(false, "UNKNOWN_BARCODE"));
    }

    var fromResult = ResolveLocationForEvent(store, opEvent.FromLoc, opEvent.FromLocationId);
    if (fromResult.Error != null)
    {
        return Results.BadRequest(BuildLocationErrorResult(fromResult, opEvent, store));
    }

    var toResult = ResolveLocationForEvent(store, opEvent.ToLoc, opEvent.ToLocationId);
    if (toResult.Error != null)
    {
        return Results.BadRequest(BuildLocationErrorResult(toResult, opEvent, store));
    }

    var fromLocation = fromResult.Location!;
    var toLocation = toResult.Location!;

    var docRef = opEvent.DocRef.Trim();
    var existingDoc = store.FindDocByRef(docRef, DocType.Move);
    long docId;

    if (existingDoc != null)
    {
        if (existingDoc.Status == DocStatus.Closed)
        {
            return Results.BadRequest(new ApiResult(false, "DOC_ALREADY_CLOSED"));
        }

        docId = existingDoc.Id;
    }
    else
    {
        docId = docs.CreateDoc(DocType.Move, docRef, null, null, null, null);
    }

    var fromHu = NormalizeHu(opEvent.FromHu);
    var toHu = NormalizeHu(opEvent.ToHu);
    if (string.IsNullOrWhiteSpace(toHu) && !string.IsNullOrWhiteSpace(opEvent.HuCode))
    {
        toHu = NormalizeHu(opEvent.HuCode);
    }

    var missingHu = new List<string>();
    if (!string.IsNullOrWhiteSpace(fromHu) && store.GetHuByCode(fromHu) == null)
    {
        missingHu.Add("from_hu");
    }
    if (!string.IsNullOrWhiteSpace(toHu) && store.GetHuByCode(toHu) == null)
    {
        missingHu.Add("to_hu");
    }

    if (missingHu.Count > 0)
    {
        return Results.BadRequest(new
        {
            ok = false,
            error = "UNKNOWN_HU",
            missing = missingHu
        });
    }

    try
    {
        docs.AddDocLine(
            docId,
            item.Id,
            opEvent.Qty,
            fromLocation.Id,
            toLocation.Id,
            null,
            null,
            fromHu,
            toHu);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ApiResult(false, ex.Message));
    }

    // Online ops close immediately to keep server state authoritative.
    var closeResult = docs.TryCloseDoc(docId, allowNegative: false);
    if (!closeResult.Success)
    {
        var error = closeResult.Errors.Count > 0
            ? string.Join("; ", closeResult.Errors)
            : "CLOSE_FAILED";
        return Results.Ok(new ApiResult(false, error));
    }

    apiStore.RecordOpEvent(opEvent.EventId, "OP", null, opEvent.DeviceId, rawJson);
    return Results.Ok(new ApiResult(true));
});

app.MapPost("/api/docs", (CreateDocRequest request, SqliteDataStore store, DocumentService docs, ApiDocStore apiStore) =>
{
    if (!string.Equals(request.Op, "MOVE", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new ApiResult(false, "UNSUPPORTED_OP"));
    }

    var docRef = docs.GenerateDocRef(DocType.Move, DateTime.Now);
    var docId = docs.CreateDoc(DocType.Move, docRef, null, null, null, null);
    var docUid = Guid.NewGuid().ToString("N");
    apiStore.AddApiDoc(docUid, docId, "DRAFT");

    return Results.Ok(new CreateDocResponse
    {
        DocUid = docUid,
        DocRef = docRef,
        Status = "DRAFT"
    });
});

app.MapPost("/api/docs/{docUid}/lines", (string docUid, AddMoveLineRequest request, SqliteDataStore store, DocumentService docs, ApiDocStore apiStore) =>
{
    if (string.IsNullOrWhiteSpace(request.EventId))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_EVENT_ID"));
    }

    if (apiStore.IsEventProcessed(request.EventId))
    {
        return Results.Ok(new ApiResult(true));
    }

    var docInfo = apiStore.GetApiDoc(docUid);
    if (docInfo == null)
    {
        return Results.NotFound(new ApiResult(false, "DOC_NOT_FOUND"));
    }

    if (!string.Equals(docInfo.Value.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new ApiResult(false, "DOC_CLOSED"));
    }

    if (string.IsNullOrWhiteSpace(request.Barcode))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_BARCODE"));
    }

    if (request.Qty <= 0)
    {
        return Results.BadRequest(new ApiResult(false, "INVALID_QTY"));
    }

    var item = store.FindItemByBarcode(request.Barcode.Trim())
               ?? FindItemByBarcodeVariant(store, request.Barcode.Trim());
    if (item == null)
    {
        return Results.BadRequest(new ApiResult(false, "UNKNOWN_BARCODE"));
    }

    var fromLocation = ResolveLocation(store, request.FromLocCode);
    var toLocation = ResolveLocation(store, request.ToLocCode);
    if (fromLocation == null || toLocation == null)
    {
        return Results.BadRequest(new ApiResult(false, "UNKNOWN_LOCATION"));
    }

    var fromHu = NormalizeHu(request.FromHu);
    var toHu = NormalizeHu(request.ToHu);
    var available = store.GetLedgerBalance(item.Id, fromLocation.Id, fromHu);
    var reserved = apiStore.GetReservedQty(item.Id, fromLocation.Id, docUid);
    var remaining = available - reserved;

    if (remaining < request.Qty)
    {
        return Results.Ok(new ApiResult(false, "INSUFFICIENT_STOCK"));
    }

    docs.AddDocLine(docInfo.Value.DocId, item.Id, request.Qty, fromLocation.Id, toLocation.Id, null, null, fromHu, toHu);
    apiStore.AddReservationLine(docUid, item.Id, fromLocation.Id, request.Qty);
    apiStore.RecordEvent(request.EventId, "LINE", docUid);

    return Results.Ok(new ApiResult(true));
});

app.MapPost("/api/docs/{docUid}/close", (string docUid, CloseDocRequest request, SqliteDataStore store, DocumentService docs, ApiDocStore apiStore) =>
{
    if (string.IsNullOrWhiteSpace(request.EventId))
    {
        return Results.BadRequest(new ApiResult(false, "MISSING_EVENT_ID"));
    }

    if (apiStore.IsEventProcessed(request.EventId))
    {
        return Results.Ok(new ApiResult(true));
    }

    var docInfo = apiStore.GetApiDoc(docUid);
    if (docInfo == null)
    {
        return Results.NotFound(new ApiResult(false, "DOC_NOT_FOUND"));
    }

    var result = docs.TryCloseDoc(docInfo.Value.DocId, allowNegative: false);
    if (!result.Success)
    {
        var error = result.Errors.Count > 0
            ? string.Join("; ", result.Errors)
            : "CLOSE_FAILED";
        return Results.Ok(new ApiResult(false, error));
    }

    apiStore.ClearReservations(docUid);
    apiStore.UpdateApiDocStatus(docUid, "CLOSED");
    apiStore.RecordEvent(request.EventId, "CLOSE", docUid);

    return Results.Ok(new ApiResult(true));
});

if (Directory.Exists(tsdRoot) && File.Exists(tsdIndexPath))
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(tsdIndexPath);
    });
}

app.Run();

static string ResolveDbPath(IConfiguration configuration)
{
    var configured = configuration["DbPath"];
    var path = string.IsNullOrWhiteSpace(configured) ? ServerPaths.DatabasePath : configured;
    path = Environment.ExpandEnvironmentVariables(path);
    return Path.GetFullPath(path);
}

static void LogDbInfo(ILogger logger, string dbPath)
{
    var fileInfo = new FileInfo(dbPath);
    var exists = fileInfo.Exists;
    var sizeBytes = exists ? fileInfo.Length : 0;
    var lastWriteUtc = exists
        ? fileInfo.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture)
        : null;

    logger.LogInformation(
        "DB: {Path} exists={Exists} size={Size} lastWriteUtc={LastWriteUtc}",
        dbPath,
        exists,
        sizeBytes,
        lastWriteUtc);
}

static object BuildDbInfo(string dbPath)
{
    var fileInfo = new FileInfo(dbPath);
    return new
    {
        dbPath,
        exists = fileInfo.Exists,
        sizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
        lastWriteUtc = fileInfo.Exists ? fileInfo.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture) : null
    };
}

static SqliteConnection OpenConnection(string dbPath)
{
    var connection = new SqliteConnection($"Data Source={dbPath}");
    connection.Open();
    return connection;
}

static long CountTable(SqliteConnection connection, string table)
{
    using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM {table};";
    var result = command.ExecuteScalar();
    return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
}

static string? NormalizeHu(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static Location? ResolveLocation(SqliteDataStore store, string? code)
{
    if (string.IsNullOrWhiteSpace(code))
    {
        return null;
    }

    return store.FindLocationByCode(code.Trim());
}

static LocationResolution ResolveLocationForEvent(SqliteDataStore store, string? code, int? id)
{
    if (id.HasValue)
    {
        var byId = store.FindLocationById(id.Value);
        return byId != null
            ? new LocationResolution { Location = byId }
            : new LocationResolution { Error = "UNKNOWN_LOCATION" };
    }

    var trimmed = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
    if (string.IsNullOrWhiteSpace(trimmed))
    {
        return new LocationResolution { Error = "MISSING_LOCATION" };
    }

    var locations = store.GetLocations();
    var byCode = locations
        .Where(location => string.Equals(location.Code, trimmed, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (byCode.Count == 1)
    {
        return new LocationResolution { Location = byCode[0] };
    }

    if (byCode.Count > 1)
    {
        return new LocationResolution { Error = "AMBIGUOUS_LOCATION", Matches = byCode };
    }

    var byName = locations
        .Where(location => string.Equals(location.Name, trimmed, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (byName.Count == 1)
    {
        return new LocationResolution { Location = byName[0] };
    }

    if (byName.Count > 1)
    {
        return new LocationResolution { Error = "AMBIGUOUS_LOCATION", Matches = byName };
    }

    return new LocationResolution { Error = "UNKNOWN_LOCATION" };
}

static object BuildLocationErrorResult(LocationResolution resolution, OperationEventRequest request, SqliteDataStore store)
{
    var sampleCodes = store.GetLocations()
        .Select(location => location.Code)
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(5)
        .ToList();

    object? matches = null;
    if (resolution.Matches != null && resolution.Matches.Count > 0)
    {
        matches = resolution.Matches
            .Select(location => new { id = location.Id, code = location.Code, name = location.Name })
            .ToList();
    }

    return new
    {
        ok = false,
        error = resolution.Error,
        details = new
        {
            parsed = new
            {
                from_loc = request.FromLoc,
                to_loc = request.ToLoc,
                from_location_id = request.FromLocationId,
                to_location_id = request.ToLocationId
            },
            matches,
            sample_codes = sampleCodes
        }
    };
}

static Item? FindItemByBarcodeVariant(SqliteDataStore store, string barcode)
{
    if (barcode.Length == 13)
    {
        return store.FindItemByBarcode("0" + barcode);
    }

    if (barcode.Length == 14 && barcode.StartsWith("0", StringComparison.Ordinal))
    {
        return store.FindItemByBarcode(barcode.Substring(1));
    }

    return null;
}

sealed class LocationResolution
{
    public Location? Location { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<Location>? Matches { get; init; }
}
