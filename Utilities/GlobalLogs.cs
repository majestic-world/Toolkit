using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace L2Toolkit.Utilities;

public class GlobalLogs
{
    private TextBox _textBlock;
    private readonly Queue<string> _logQueue = new();
    private readonly object _locker = new();

    public void AddLog(string log)
    {
        lock (_locker)
        {
            _logQueue.Enqueue($"[{DateTime.Now:hh:mm:ss}] {log}");
            if (_logQueue.Count > 120)
                _logQueue.Dequeue();

            UpdateTextBlock();
        }
    }

    public void ClearLog()
    {
        lock (_locker)
        {
            _logQueue.Clear();
            UpdateTextBlock();
        }
    }

    private void UpdateTextBlock()
    {
        if (_textBlock == null)
            return;

        var text = string.Join("\n", _logQueue.Reverse());

        if (_textBlock.Dispatcher.CheckAccess())
        {
            _textBlock.Text = text;
        }
        else
        {
            _textBlock.Dispatcher.Invoke(() =>
            {
                _textBlock.Text = text;
            });
        }
    }

    public void RegisterBlock(TextBox block)
    {
        lock (_locker)
        {
            _textBlock = block;
        }
    }
}