using System;
using System.Globalization;
using System.IO;

// appends one timestamped row of averaged fan RPMs per completed window
class CsvLogger
{
    readonly string _filePath;
    readonly string _header;

    public CsvLogger(string filePath, string header)
    {
        _filePath = filePath;
        _header = header;
    }

    public void Append(float[] values)
    {
        // invariant culture + explicit format: on a locale that uses a comma as the
        // decimal separator, the default ToString() would break the CSV itself.
        string[] items = new string[values.Length + 1];
        items[0] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        for (int i = 0; i < values.Length; i++)
            items[i + 1] = float.IsNaN(values[i]) ? "" : values[i].ToString("F2", CultureInfo.InvariantCulture);

        bool needsHeader = !File.Exists(_filePath);

        using (StreamWriter writer = new StreamWriter(_filePath, true))
        {
            if (needsHeader)
                writer.WriteLine(_header);
            writer.WriteLine(string.Join(", ", items));
        }
    }
}
