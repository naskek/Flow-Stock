using LightWms.Core.Abstractions;
using LightWms.Core.Models;

namespace LightWms.Core.Services;

public sealed class DocumentService
{
    private readonly IDataStore _data;

    public DocumentService(IDataStore data)
    {
        _data = data;
    }

    public IReadOnlyList<Doc> GetDocs()
    {
        return _data.GetDocs();
    }

    public IReadOnlyList<DocLineView> GetDocLines(long docId)
    {
        return _data.GetDocLineViews(docId);
    }

    public IReadOnlyList<StockRow> GetStock(string? search)
    {
        return _data.GetStock(search);
    }

    public void CloseDoc(long docId)
    {
        var closedAt = DateTime.Now;

        _data.ExecuteInTransaction(store =>
        {
            var doc = store.GetDoc(docId);
            if (doc == null || doc.Status == DocStatus.Closed)
            {
                return;
            }

            var lines = store.GetDocLines(docId);
            foreach (var line in lines)
            {
                switch (doc.Type)
                {
                    case DocType.Inbound:
                        if (line.ToLocationId.HasValue)
                        {
                            store.AddLedgerEntry(new LedgerEntry
                            {
                                Timestamp = closedAt,
                                DocId = docId,
                                ItemId = line.ItemId,
                                LocationId = line.ToLocationId.Value,
                                QtyDelta = line.Qty
                            });
                        }
                        break;
                    case DocType.WriteOff:
                        if (line.FromLocationId.HasValue)
                        {
                            store.AddLedgerEntry(new LedgerEntry
                            {
                                Timestamp = closedAt,
                                DocId = docId,
                                ItemId = line.ItemId,
                                LocationId = line.FromLocationId.Value,
                                QtyDelta = -line.Qty
                            });
                        }
                        break;
                    case DocType.Move:
                        if (line.FromLocationId.HasValue)
                        {
                            store.AddLedgerEntry(new LedgerEntry
                            {
                                Timestamp = closedAt,
                                DocId = docId,
                                ItemId = line.ItemId,
                                LocationId = line.FromLocationId.Value,
                                QtyDelta = -line.Qty
                            });
                        }
                        if (line.ToLocationId.HasValue)
                        {
                            store.AddLedgerEntry(new LedgerEntry
                            {
                                Timestamp = closedAt,
                                DocId = docId,
                                ItemId = line.ItemId,
                                LocationId = line.ToLocationId.Value,
                                QtyDelta = line.Qty
                            });
                        }
                        break;
                    case DocType.Inventory:
                        // MVP: inventory ledger logic is deferred; keep the document close only.
                        break;
                }
            }

            store.UpdateDocStatus(docId, DocStatus.Closed, closedAt);
        });
    }
}
