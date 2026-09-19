using System.Text;

namespace ViYuki.Core;

public sealed class CommandParser
{
    public ParsedCommand Parse(string input)
    {
        var tokens = Tokenize(input);
        if (tokens.Count == 0)
        {
            return ParsedCommand.Empty;
        }

        return new ParsedCommand(tokens[0], tokens.Skip(1).ToArray());
    }

    public IReadOnlyList<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (c == '"' && (i == 0 || input[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                AddTokenIfNeeded(tokens, current);
                continue;
            }

            if (c == '\\' && i + 1 < input.Length && input[i + 1] == '"')
            {
                current.Append('"');
                i++;
                continue;
            }

            current.Append(c);
        }

        if (inQuotes)
        {
            throw new CommandParseException("Missing closing quote.");
        }

        AddTokenIfNeeded(tokens, current);
        return tokens;
    }

    private static void AddTokenIfNeeded(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }
}
