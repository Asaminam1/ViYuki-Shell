using System.Text;
using ViYuki.Core;

namespace ViYuki.Input;

public sealed class CommandLineReader
{
    private readonly List<string> _history = [];
    private readonly ITabCompleter _tabCompleter;
    private readonly CommandHistory _commandHistory;

    public CommandLineReader(ITabCompleter tabCompleter)
    {
        _tabCompleter = tabCompleter;
        _commandHistory = new CommandHistory();
        _history.AddRange(_commandHistory.Load());
    }

    public string? ReadLine(Prompt prompt)
    {
        WritePrompt(prompt);

        var buffer = new StringBuilder();
        var historyIndex = _history.Count;
        var inputLeft = Console.CursorLeft;
        var inputTop = Console.CursorTop;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    var line = buffer.ToString();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        _history.Add(line);
                        if (_history.Count > CommandHistory.MaximumEntries)
                        {
                            _history.RemoveRange(0, _history.Count - CommandHistory.MaximumEntries);
                        }

                        _commandHistory.Save(_history);
                    }

                    return line;

                case ConsoleKey.Backspace:
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                        RenderInputLine(buffer.ToString(), inputLeft, inputTop);
                    }

                    break;

                case ConsoleKey.UpArrow:
                    if (_history.Count > 0 && historyIndex > 0)
                    {
                        historyIndex--;
                        buffer.Clear().Append(_history[historyIndex]);
                        RenderInputLine(buffer.ToString(), inputLeft, inputTop);
                    }

                    break;

                case ConsoleKey.DownArrow:
                    if (historyIndex < _history.Count - 1)
                    {
                        historyIndex++;
                        buffer.Clear().Append(_history[historyIndex]);
                    }
                    else
                    {
                        historyIndex = _history.Count;
                        buffer.Clear();
                    }

                    RenderInputLine(buffer.ToString(), inputLeft, inputTop);
                    break;

                case ConsoleKey.Tab:
                    var completed = _tabCompleter.Complete(buffer.ToString());
                    if (completed is not null)
                    {
                        buffer.Clear().Append(completed);
                        RenderInputLine(buffer.ToString(), inputLeft, inputTop);
                    }

                    break;

                case ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    Console.WriteLine();
                    return null;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }

                    break;
            }
        }
    }

    private static void WritePrompt(Prompt prompt)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(prompt.Title);
        Console.Write("╰─ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(prompt.CurrentDirectory);
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("> ");
        Console.ForegroundColor = previousColor;
    }

    private static void RenderInputLine(string text, int left, int top)
    {
        Console.SetCursorPosition(left, top);
        Console.Write(new string(' ', Math.Max(0, Console.BufferWidth - left - 1)));
        Console.SetCursorPosition(left, top);
        Console.Write(text);
    }
}
