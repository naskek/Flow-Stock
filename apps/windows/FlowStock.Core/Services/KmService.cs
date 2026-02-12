using System.Security.Cryptography;
using FlowStock.Core.Abstractions;
using FlowStock.Core.Models;

namespace FlowStock.Core.Services;

public sealed class KmService
{
    private readonly IDataStore _data;

    public KmService(IDataStore data)
    {
        _data = data;
    }

    public IReadOnlyList<KmCodeBatch> GetBatches()
    {
        return _data.GetKmCodeBatches();
    }

    public IReadOnlyList<KmCode> GetCodes(long batchId, string? search, KmCodeStatus? status, int take = 1000)
    {
        return _data.GetKmCodesByBatch(batchId, search, status, take);
    }

    public int CountUnmatchedSku(long batchId)
    {
        return _data.CountKmCodesWithoutSku(batchId);
    }

    public int GetAssignedCountForReceiptLine(long receiptLineId)
    {
        return _data.CountKmCodesByReceiptLine(receiptLineId);
    }

    public int GetAssignedCountForShipmentLine(long shipLineId)
    {
        return _data.CountKmCodesByShipmentLine(shipLineId);
    }

    public IReadOnlyList<KmCode> GetShipmentCodes(long shipLineId)
    {
        return _data.GetKmCodesByShipmentLine(shipLineId);
    }

    public KmImportResult ImportCodes(string filePath, long? orderId, string? importedBy)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new KmImportResult
            {
                FileName = string.Empty,
                FileHash = string.Empty,
                Errors = 1
            };
        }

        var fileHash = ComputeFileHash(filePath);
        if (_data.FindKmCodeBatchByHash(fileHash) != null)
        {
            return new KmImportResult
            {
                FileName = Path.GetFileName(filePath),
                FileHash = fileHash,
                IsDuplicateFile = true
            };
        }

        var firstLine = File.ReadLines(filePath).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        var delimiter = DetectDelimiter(firstLine);
        var batchId = 0L;
        var imported = 0;
        var duplicates = 0;
        var errors = 0;
        var invalidGtins = 0;
        var emptyCodes = 0;
        var unmatchedSku = 0;

        _data.ExecuteInTransaction(store =>
        {
            batchId = store.AddKmCodeBatch(new KmCodeBatch
            {
                OrderId = orderId,
                FileName = Path.GetFileName(filePath),
                FileHash = fileHash,
                ImportedAt = DateTime.Now,
                ImportedBy = importedBy,
                TotalCodes = 0,
                ErrorCount = 0
            });

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rawLine in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                var parts = rawLine.Split(delimiter);
                var codeRaw = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                var gtinRaw = parts.Length > 1 ? parts[1].Trim() : null;
                var nameRaw = parts.Length > 2 ? parts[2].Trim() : null;

                if (string.IsNullOrWhiteSpace(codeRaw))
                {
                    emptyCodes++;
                    errors++;
                    continue;
                }

                if (!seen.Add(codeRaw))
                {
                    duplicates++;
                    continue;
                }

                var gtin14 = NormalizeGtin(gtinRaw, out var gtinInvalid);
                if (gtinInvalid)
                {
                    invalidGtins++;
                    errors++;
                }

                var skuId = gtin14 != null ? store.FindItemByGtin(gtin14)?.Id : null;
                if (!skuId.HasValue)
                {
                    unmatchedSku++;
                }

                try
                {
                    if (store.FindKmCodeByRaw(codeRaw) != null)
                    {
                        duplicates++;
                        continue;
                    }

                    store.AddKmCode(new KmCode
                    {
                        BatchId = batchId,
                        CodeRaw = codeRaw,
                        Gtin14 = gtin14,
                        SkuId = skuId,
                        ProductName = string.IsNullOrWhiteSpace(nameRaw) ? null : nameRaw,
                        Status = KmCodeStatus.Imported,
                        OrderId = orderId
                    });
                    imported++;
                }
                catch
                {
                    errors++;
                }
            }

            store.UpdateKmCodeBatchStats(batchId, imported, errors);
        });

        return new KmImportResult
        {
            BatchId = batchId,
            FileName = Path.GetFileName(filePath),
            FileHash = fileHash,
            Imported = imported,
            Duplicates = duplicates,
            Errors = errors,
            InvalidGtins = invalidGtins,
            EmptyCodes = emptyCodes,
            UnmatchedSku = unmatchedSku
        };
    }

    public int AssignCodesToReceipt(long docId, DocLine line, Item item, long? batchId, long? orderId)
    {
        if (!line.ToLocationId.HasValue)
        {
            throw new InvalidOperationException("Не указана локация приемки.");
        }

        if (string.IsNullOrWhiteSpace(line.ToHu))
        {
            throw new InvalidOperationException("Для выпуска продукции требуется HU.");
        }

        var required = EnsureIntegerQty(line.Qty);
        var gtin14 = NormalizeGtin(item.Gtin, out _);
        var ids = _data.GetAvailableKmCodeIds(batchId, orderId, item.Id, gtin14, required);
        if (ids.Count < required)
        {
            throw new InvalidOperationException($"Недостаточно кодов для {item.Name}. Нужно {required}, доступно {ids.Count}.");
        }

        var huId = ResolveHuId(line.ToHu);
        var locationId = line.ToLocationId;
        var updated = _data.AssignKmCodesToReceipt(ids, docId, line.Id, huId, locationId);
        if (updated != required)
        {
            throw new InvalidOperationException($"Не удалось привязать все коды. Привязано {updated} из {required}.");
        }

        return updated;
    }

    public void AssignCodeToShipment(string codeRaw, long docId, DocLine line, Item item, long? orderId)
    {
        if (string.IsNullOrWhiteSpace(codeRaw))
        {
            throw new ArgumentException("Код КМ не задан.");
        }

        var trimmed = codeRaw.Trim();
        var code = _data.FindKmCodeByRaw(trimmed);
        if (code == null)
        {
            throw new InvalidOperationException("Код не найден в реестре.");
        }

        if (code.Status != KmCodeStatus.OnHand)
        {
            throw new InvalidOperationException($"Код имеет статус {KmCodeStatusMapper.ToDisplayName(code.Status)}.");
        }

        if (!IsCodeMatchingItem(code, item))
        {
            throw new InvalidOperationException("Код не соответствует выбранному SKU.");
        }

        _data.MarkKmCodeShipped(code.Id, docId, line.Id, orderId);
    }

    private long? ResolveHuId(string? huCode)
    {
        if (string.IsNullOrWhiteSpace(huCode))
        {
            return null;
        }

        var record = _data.GetHuByCode(huCode.Trim());
        return record?.Id;
    }

    private static bool IsCodeMatchingItem(KmCode code, Item item)
    {
        if (code.SkuId.HasValue)
        {
            return code.SkuId.Value == item.Id;
        }

        var gtin14 = NormalizeGtin(item.Gtin, out _);
        if (string.IsNullOrWhiteSpace(gtin14))
        {
            return false;
        }

        return string.Equals(code.Gtin14, gtin14, StringComparison.OrdinalIgnoreCase);
    }

    private static int EnsureIntegerQty(double qty)
    {
        var rounded = Math.Round(qty);
        if (Math.Abs(qty - rounded) > 0.0001)
        {
            throw new InvalidOperationException("Количество для маркируемого товара должно быть целым.");
        }

        return (int)rounded;
    }

    private static string? NormalizeGtin(string? value, out bool invalid)
    {
        invalid = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 14 || !trimmed.All(char.IsDigit))
        {
            invalid = true;
            return null;
        }

        return trimmed;
    }

    private static char DetectDelimiter(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return '\t';
        }

        var tab = line.Count(ch => ch == '\t');
        var semicolon = line.Count(ch => ch == ';');
        var comma = line.Count(ch => ch == ',');
        var max = Math.Max(tab, Math.Max(semicolon, comma));
        if (max == 0)
        {
            return '\t';
        }
        if (tab == max)
        {
            return '\t';
        }
        if (semicolon == max)
        {
            return ';';
        }
        return ',';
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
