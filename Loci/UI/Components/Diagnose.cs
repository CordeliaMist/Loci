using MemoryPack;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Loci;

public static class MemoryPackDiagnostics
{
    public static void DiagnoseStatusBlob<TStatus>(byte[] data)
        where TStatus : class
    {
        var sb = new StringBuilder();

        sb.AppendLine("===== MEMORYPACK DIAGNOSTIC START =====");
        sb.AppendLine($"Target Type: {typeof(TStatus).FullName}");
        sb.AppendLine($"Data Length: {data.Length} bytes");
        sb.AppendLine();

        // 1️⃣ Peek list count
        if (data.Length >= 4)
        {
            var count = BitConverter.ToInt32(data, 0);
            sb.AppendLine($"[List Header] Peeked Count = {count}");
        }
        else
        {
            sb.AppendLine("[List Header] Data too short to read count");
        }

        sb.AppendLine();

        // 2️⃣ Enum underlying types
        sb.AppendLine("[Enum Underlying Types]");
        foreach (var f in typeof(TStatus).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.FieldType.IsEnum)
            {
                sb.AppendLine($"  {f.Name} -> {Enum.GetUnderlyingType(f.FieldType)}");
            }
        }

        sb.AppendLine();

        // 3️⃣ Field layout and sizes
        sb.AppendLine("[Field Layout]");
        foreach (var f in typeof(TStatus).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            int size = -1;
            try
            {
                if (f.FieldType.IsEnum)
                    size = Marshal.SizeOf(Enum.GetUnderlyingType(f.FieldType));
                else if (f.FieldType.IsValueType)
                    size = Marshal.SizeOf(f.FieldType);
            }
            catch { }

            sb.AppendLine($"  {f.Name,-24} {f.FieldType,-28} size={size}");
        }

        sb.AppendLine();

        // 4️⃣ Try normal deserialize (ground truth)
        sb.AppendLine("[Full Deserialize]");
        try
        {
            var list = MemoryPackSerializer.Deserialize<List<TStatus>>(data);
            sb.AppendLine($"  Deserialized List Count = {list?.Count ?? -1}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  FAILED: {ex.GetType().Name} - {ex.Message}");
        }

        sb.AppendLine("===== MEMORYPACK DIAGNOSTIC END =====");

        Svc.Logger.Information(sb.ToString());
    }

    private static string HexDump(ReadOnlySpan<byte> data, int maxBytes, int bytesPerLine = 16)
    {
        var sb = new StringBuilder();
        var len = Math.Min(data.Length, maxBytes);

        for (int i = 0; i < len; i += bytesPerLine)
        {
            sb.Append($"{i:X6}: ");

            for (int j = 0; j < bytesPerLine; j++)
            {
                if (i + j < len)
                    sb.Append($"{data[i + j]:X2} ");
                else
                    sb.Append("   ");
            }

            sb.Append(" | ");

            for (int j = 0; j < bytesPerLine && i + j < len; j++)
            {
                var b = data[i + j];
                sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}