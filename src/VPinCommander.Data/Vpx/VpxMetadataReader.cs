using System.Text;
using System.Text.RegularExpressions;
using OpenMcdf;
using VPinCommander.Core.Scanning;

namespace VPinCommander.Data.Vpx;

/// <summary>
/// Reads metadata from .vpx files. A VPX table is an OLE compound file:
/// the table script lives in the GameStg\GameData stream as a BIFF record
/// tagged CODE, and human metadata lives as UTF-16 streams under TableInfo.
/// </summary>
public sealed partial class VpxMetadataReader : IVpxMetadataReader
{
    [GeneratedRegex("""cGameName\s*=\s*"([^"]*)"\s*""", RegexOptions.IgnoreCase)]
    private static partial Regex GameNameRegex();

    public VpxMetadata? Read(string vpxPath)
    {
        try
        {
            using var root = RootStorage.OpenRead(vpxPath);

            string? romName = null;
            var script = ReadScript(root);
            if (script is not null)
            {
                var match = GameNameRegex().Match(script);
                if (match.Success && match.Groups[1].Value.Trim() is { Length: > 0 } rom)
                    romName = rom;
            }

            return new VpxMetadata(
                RomName: romName,
                TableName: ReadTableInfo(root, "TableName"),
                AuthorName: ReadTableInfo(root, "AuthorName"),
                TableVersion: ReadTableInfo(root, "TableVersion"),
                FileVersion: ReadFileVersion(root));
        }
        catch (Exception)
        {
            // Corrupt/locked/not-actually-a-vpx files must not break a scan.
            return null;
        }
    }

    private static string? ReadScript(RootStorage root)
    {
        try
        {
            var storage = root.OpenStorage("GameStg");
            using var stream = storage.OpenStream("GameData");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return ExtractScriptFromBiff(buffer.ToArray());
        }
        catch (Exception)
        {
            return null; // storage/stream absent or unreadable — no script, not an error
        }
    }

    /// <summary>
    /// GameData is a sequence of records: [int32 length][4-byte tag][length-4 bytes of data].
    /// The CODE tag is followed by a raw [int32 length][script bytes] block instead of inline data.
    /// </summary>
    internal static string? ExtractScriptFromBiff(byte[] data)
    {
        int pos = 0;
        while (pos + 8 <= data.Length)
        {
            int recordLength = BitConverter.ToInt32(data, pos);
            pos += 4;
            if (recordLength < 4 || pos + 4 > data.Length)
                return null;

            var tag = Encoding.ASCII.GetString(data, pos, 4);
            if (tag == "CODE")
            {
                pos += 4;
                if (pos + 4 > data.Length)
                    return null;
                int scriptLength = BitConverter.ToInt32(data, pos);
                pos += 4;
                if (scriptLength < 0)
                    return null;
                scriptLength = Math.Min(scriptLength, data.Length - pos);
                return Encoding.Latin1.GetString(data, pos, scriptLength);
            }

            if (tag == "ENDB")
                return null;

            pos += recordLength; // record length includes the 4 tag bytes
        }
        return null;
    }

    /// <summary>GameStg\Version holds the file-format version as int32 (e.g. 1070 = saved by VPX 10.7).</summary>
    private static int? ReadFileVersion(RootStorage root)
    {
        try
        {
            var storage = root.OpenStorage("GameStg");
            using var stream = storage.OpenStream("Version");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            return bytes.Length >= 4 ? BitConverter.ToInt32(bytes, 0) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ReadTableInfo(RootStorage root, string streamName)
    {
        try
        {
            var storage = root.OpenStorage("TableInfo");
            using var stream = storage.OpenStream(streamName);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var value = Encoding.Unicode.GetString(buffer.ToArray()).TrimEnd('\0').Trim();
            return value.Length == 0 ? null : value;
        }
        catch (Exception)
        {
            return null; // metadata streams are optional
        }
    }
}
