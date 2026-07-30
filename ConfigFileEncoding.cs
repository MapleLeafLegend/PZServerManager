using System.IO;
using System.Text;

namespace PZServerManager;

public static class ConfigFileEncoding
{
    public static (string Text, Encoding Encoding) Read(string path, string mode = "Auto")
    {
        var bytes = File.ReadAllBytes(path);
        if (mode.Equals("RepairUtf8FromBig5", StringComparison.OrdinalIgnoreCase))
            return ReadAndRepairUtf8Mojibake(bytes);
        if (!mode.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            if (mode.Equals("Big5", StringComparison.OrdinalIgnoreCase) && IsStrictUtf8(bytes))
                throw new InvalidDataException(
                    "檔案位元組已通過嚴格 UTF-8 驗證，不能強制用 Big5 解碼。請選擇「自動」或「UTF-8」；檔案未被修改。");
            var selected = EncodingForMode(mode);
            var preamble = selected.GetPreamble();
            var offset = StartsWith(bytes, preamble) ? preamble.Length : 0;
            return (StrictClone(selected).GetString(bytes, offset, bytes.Length - offset), selected);
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), new UTF8Encoding(true));
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Encoding.Unicode);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Encoding.BigEndianUnicode);
        if (bytes.All(value => value <= 0x7F))
            return (Encoding.ASCII.GetString(bytes), new UTF8Encoding(false));

        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            return (strictUtf8.GetString(bytes), new UTF8Encoding(false));
        }
        catch (DecoderFallbackException)
        {
            var big5 = Encoding.GetEncoding(950,
                EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            return (big5.GetString(bytes), big5);
        }
    }

    public static string ReadText(string path, string mode = "Auto") => Read(path, mode).Text;

    public static void WritePreservingEncoding(string path, string text, string mode = "Auto")
    {
        if (mode.Equals("RepairUtf8FromBig5", StringComparison.OrdinalIgnoreCase))
        {
            var keepBom = File.Exists(path) &&
                StartsWith(File.ReadAllBytes(path), new byte[] { 0xEF, 0xBB, 0xBF });
            var encoding = new UTF8Encoding(keepBom, true);
            AtomicWrite(path, WithPreamble(encoding, encoding.GetBytes(text)));
            return;
        }
        if (!File.Exists(path))
        {
            var newEncoding = mode.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                ? new UTF8Encoding(false) : EncodingForMode(mode);
            AtomicWrite(path, WithPreamble(newEncoding, StrictClone(newEncoding).GetBytes(text)));
            return;
        }
        var (_, originalEncoding) = mode.Equals("Auto", StringComparison.OrdinalIgnoreCase)
            ? Read(path) : (string.Empty, EncodingForMode(mode));
        var strictEncoding = StrictClone(originalEncoding);
        var content = strictEncoding.GetBytes(text);
        var preamble = originalEncoding.GetPreamble();
        var bytes = WithPreamble(originalEncoding, content);
        var decoded = DecodeUsingExactEncoding(bytes, originalEncoding);
        if (!string.Equals(decoded, text, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"使用原始編碼 {originalEncoding.WebName} 寫入後無法完整還原文字，已拒絕儲存。");
        AtomicWrite(path, bytes);
    }

    public static string[] ReadAllLines(string path, string mode = "Auto") =>
        ReadText(path, mode).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static Encoding EncodingForMode(string mode) => mode switch
    {
        "Utf8" => new UTF8Encoding(false, true),
        "Utf8Bom" => new UTF8Encoding(true, true),
        "Big5" => Encoding.GetEncoding(950, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
        "Utf16LE" => new UnicodeEncoding(false, true, true),
        "Utf16BE" => new UnicodeEncoding(true, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知的設定檔編碼。")
    };

    private static bool IsStrictUtf8(byte[] bytes)
    {
        var offset = StartsWith(bytes, new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
        if (bytes.Length == offset || bytes.Skip(offset).All(value => value <= 0x7F)) return false;
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            return true;
        }
        catch (DecoderFallbackException) { return false; }
    }

    private static (string Text, Encoding Encoding) ReadAndRepairUtf8Mojibake(byte[] bytes)
    {
        var hasBom = StartsWith(bytes, new byte[] { 0xEF, 0xBB, 0xBF });
        var offset = hasBom ? 3 : 0;
        string mojibake;
        try
        {
            mojibake = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException(
                "修復模式只適用於「已被存成 UTF-8 的 Big5 誤讀文字」。目前檔案本身不是有效 UTF-8。", ex);
        }

        try
        {
            var big5 = Encoding.GetEncoding(950,
                EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            var originalBytes = big5.GetBytes(mojibake);
            var repaired = new UTF8Encoding(false, true).GetString(originalBytes);
            if (string.Equals(repaired, mojibake, StringComparison.Ordinal))
                throw new InvalidDataException("內容不符合可修復的 Big5 誤讀特徵。");
            return (repaired, new UTF8Encoding(hasBom, true));
        }
        catch (EncoderFallbackException ex)
        {
            throw new InvalidDataException(
                "內容無法以 Big5 位元組往返，不能安全自動還原；檔案未被修改。", ex);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException(
                "內容不是可逆的 UTF-8／Big5 亂碼；若已出現 ? 或 �，遺失的字元需由備份還原。檔案未被修改。", ex);
        }
    }

    private static byte[] WithPreamble(Encoding encoding, byte[] content)
    {
        var preamble = encoding.GetPreamble();
        return preamble.Length == 0 ? content : preamble.Concat(content).ToArray();
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix) =>
        prefix.Length > 0 && bytes.Length >= prefix.Length &&
        bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private static Encoding StrictClone(Encoding encoding) => encoding.CodePage switch
    {
        65001 => new UTF8Encoding(encoding.GetPreamble().Length > 0, true),
        1200 => new UnicodeEncoding(false, true, true),
        1201 => new UnicodeEncoding(true, true, true),
        _ => Encoding.GetEncoding(encoding.CodePage,
            EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
    };

    private static string DecodeUsingExactEncoding(byte[] bytes, Encoding encoding)
    {
        var preambleLength = encoding.GetPreamble().Length;
        return StrictClone(encoding).GetString(bytes, preambleLength, bytes.Length - preambleLength);
    }

    private static void AtomicWrite(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var backup = path + ".manager-backup";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(true);
            }
            if (File.Exists(path))
            {
                if (File.Exists(backup)) File.Delete(backup);
                File.Replace(temporary, path, backup, true);
            }
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
