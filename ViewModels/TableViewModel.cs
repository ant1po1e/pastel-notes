using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using PastelNotes.Infrastructure;

namespace PastelNotes.ViewModels;

public class CellViewModel : ObservableObject
{
    private string _text;
    private bool _isHeader;

    public CellViewModel(BlockViewModel owner, string text, bool isHeader)
    {
        Owner = owner;
        _text = text;
        _isHeader = isHeader;
    }

    public BlockViewModel Owner { get; }

    public string Text { get => _text; set => Set(ref _text, value ?? ""); }

    public bool IsHeader { get => _isHeader; set => Set(ref _isHeader, value); }
}

public class TableRowViewModel
{
    public ObservableCollection<CellViewModel> Cells { get; } = new();
}

public class TableViewModel
{
    public TableViewModel(BlockViewModel owner)
    {
        Owner = owner;
    }

    public BlockViewModel Owner { get; }

    public ObservableCollection<TableRowViewModel> Rows { get; } = new();

    public event Action? Changed;

    public int ColumnCount => Rows.Count == 0 ? 0 : Rows[0].Cells.Count;

    public static TableViewModel Create(BlockViewModel owner, IReadOnlyList<IReadOnlyList<string>> data)
    {
        var table = new TableViewModel(owner);
        int columns = Math.Max(1, data.Count == 0 ? 1 : data.Max(r => r.Count));

        for (int r = 0; r < data.Count; r++)
        {
            var row = new TableRowViewModel();
            for (int c = 0; c < columns; c++)
                table.AddCell(row, c < data[r].Count ? data[r][c] : "", r == 0);
            table.Rows.Add(row);
        }

        while (table.Rows.Count < 2)
        {
            var row = new TableRowViewModel();
            for (int c = 0; c < columns; c++)
                table.AddCell(row, "", table.Rows.Count == 0);
            table.Rows.Add(row);
        }

        return table;
    }

    public static TableViewModel CreateDefault(BlockViewModel owner)
    {
        var data = new List<IReadOnlyList<string>>
        {
            new[] { "Column 1", "Column 2", "Column 3" },
            new[] { "", "", "" },
            new[] { "", "", "" },
        };
        return Create(owner, data);
    }

    public void AddRow()
    {
        var row = new TableRowViewModel();
        for (int c = 0; c < ColumnCount; c++) AddCell(row, "", false);
        Rows.Add(row);
        Changed?.Invoke();
    }

    public void RemoveRow()
    {
        if (Rows.Count <= 2) return;
        Rows.RemoveAt(Rows.Count - 1);
        Changed?.Invoke();
    }

    public void AddColumn()
    {
        int next = ColumnCount + 1;
        for (int r = 0; r < Rows.Count; r++)
            AddCell(Rows[r], r == 0 ? "Column " + next : "", r == 0);
        Changed?.Invoke();
    }

    public void RemoveColumn()
    {
        if (ColumnCount <= 1) return;
        foreach (var row in Rows)
        {
            var cell = row.Cells[row.Cells.Count - 1];
            cell.PropertyChanged -= OnCellChanged;
            row.Cells.RemoveAt(row.Cells.Count - 1);
        }
        Changed?.Invoke();
    }

    public List<List<string>> ToRows()
        => Rows.Select(r => r.Cells.Select(c => c.Text).ToList()).ToList();

    private void AddCell(TableRowViewModel row, string text, bool header)
    {
        var cell = new CellViewModel(Owner, text, header);
        cell.PropertyChanged += OnCellChanged;
        row.Cells.Add(cell);
    }

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CellViewModel.Text)) Changed?.Invoke();
    }
}
