using System.Diagnostics.CodeAnalysis;

namespace H2MLauncher.Core.Models;

internal class InfoString
{
    private readonly Dictionary<string, string> _keyValuePairs = [];

    public ICollection<string> Keys => ((IDictionary<string, string>)_keyValuePairs).Keys;
    public ICollection<string> Values => ((IDictionary<string, string>)_keyValuePairs).Values;

    public int Count => ((ICollection<KeyValuePair<string, string>>)_keyValuePairs).Count;

    public string this[string key]
    {
        get => _keyValuePairs[key];
        set => _keyValuePairs[key] = value;
    }

    /// Constructor that takes a string and parses it
    public InfoString(string buffer)
    {
        Parse(buffer);
    }

    public InfoString(ReadOnlySpan<char> buffer) : this(buffer.ToString())
    {
    }

    // Set a key-value pair
    public void Set(string key, string value)
    {
        _keyValuePairs[key] = value;
    }

    // Get the value corresponding to a key
    public string? Get(string key)
    {
        return _keyValuePairs.GetValueOrDefault(key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
    {
        return _keyValuePairs.TryGetValue(key, out value);
    }

    // Parse the input string to extract key-value pairs
    private void Parse(string buffer)
    {
        if (buffer.StartsWith('\\'))
        {
            buffer = buffer[1..];
        }

        // Splitting the buffer into key-value pairs based on the '\\' delimiter
        var keyValues = buffer.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < keyValues.Length - 1; i += 2)
        {
            string key = keyValues[i];
            string value = keyValues[i + 1];

            if (!_keyValuePairs.ContainsKey(key))
            {
                _keyValuePairs[key] = value;
            }
        }
    }

    // Build the info string from the key-value pairs
    public override string ToString()
    {
        return string.Join("", _keyValuePairs.Select(kv => $"\\{kv.Key}\\{kv.Value}"));
    }
}
