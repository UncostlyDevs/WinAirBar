using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WindowHistoryService
{
    private readonly string _historyDirectory;
    private readonly string _historyFilePath;
    private List<WindowHistory> _history = new();

    public WindowHistoryService()
    {
        _historyDirectory = Path.Combine(AppIdentity.AppDataDirectory, "History");
        Directory.CreateDirectory(_historyDirectory);
        _historyFilePath = Path.Combine(_historyDirectory, "history.json");
        LoadHistory();
    }

    public void AddWindowHistory(WindowHistory entry)
    {
        try
        {
            _history.Insert(0, entry);

            // Keep only last 1000 entries to prevent file bloat
            if (_history.Count > 1000)
                _history = _history.Take(1000).ToList();

            SaveHistory();
        }
        catch { }
    }

    public List<WindowHistory> GetHistory(HistoryFilter filter, int page = 0, int pageSize = 30)
    {
        try
        {
            var cutoffDate = filter switch
            {
                HistoryFilter.Session => DateTime.MinValue,
                HistoryFilter.Day => DateTime.Now.AddDays(-1),
                HistoryFilter.Week => DateTime.Now.AddDays(-7),
                HistoryFilter.Forever => DateTime.MinValue,
                _ => DateTime.MinValue
            };

            var filtered = _history.Where(h => h.ClosedTime >= cutoffDate).ToList();

            var startIndex = page * pageSize;
            if (startIndex >= filtered.Count)
                return new List<WindowHistory>();

            return filtered.Skip(startIndex).Take(pageSize).ToList();
        }
        catch { return new List<WindowHistory>(); }
    }

    public int GetTotalCount(HistoryFilter filter)
    {
        try
        {
            var cutoffDate = filter switch
            {
                HistoryFilter.Session => DateTime.MinValue,
                HistoryFilter.Day => DateTime.Now.AddDays(-1),
                HistoryFilter.Week => DateTime.Now.AddDays(-7),
                HistoryFilter.Forever => DateTime.MinValue,
                _ => DateTime.MinValue
            };

            return _history.Count(h => h.ClosedTime >= cutoffDate);
        }
        catch { return 0; }
    }

    public void ClearHistory()
    {
        try
        {
            _history.Clear();
            SaveHistory();
        }
        catch { }
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = File.ReadAllText(_historyFilePath);
                _history = JsonSerializer.Deserialize<List<WindowHistory>>(json) ?? new List<WindowHistory>();
            }
        }
        catch { _history = new List<WindowHistory>(); }
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFilePath, json);
        }
        catch { }
    }
}
