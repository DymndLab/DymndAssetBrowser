using Microsoft.Data.Sqlite;
using System.IO;

namespace DymndBuilder.V1;

public sealed record SutSummary(string ToolName, bool IsRibbon, bool UsesSpray, bool UsesPatternImage, int MaterialCount, double BrushSize)
{
    public override string ToString() =>
        $"{ToolName} · ribbon: {(IsRibbon ? "yes" : "no")} · spray: {(UsesSpray ? "yes" : "no")} · brush size: {BrushSize:0.##} · embedded materials: {MaterialCount}";
}

public static class SutInspector
{
    public static SutSummary Inspect(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("CSP .sut file not found.", path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        connection.Open();

        using var variant = connection.CreateCommand();
        variant.CommandText = "SELECT COALESCE(MAX(BrushRibbon),0), COALESCE(MAX(BrushUseSpray),0), COALESCE(MAX(BrushUsePatternImage),0), COALESCE(MAX(BrushSize),100) FROM Variant";
        using var reader = variant.ExecuteReader();
        reader.Read();
        var ribbon = reader.GetInt64(0) != 0;
        var spray = reader.GetInt64(1) != 0;
        var pattern = reader.GetInt64(2) != 0;
        var brushSize = reader.GetDouble(3);
        reader.Close();

        using var nameCommand = connection.CreateCommand();
        nameCommand.CommandText = "SELECT COALESCE(NodeName, '') FROM Node ORDER BY _PW_ID LIMIT 1";
        var name = Convert.ToString(nameCommand.ExecuteScalar()) ?? Path.GetFileNameWithoutExtension(path);
        using var materialCommand = connection.CreateCommand();
        materialCommand.CommandText = "SELECT COUNT(*) FROM MaterialFile";
        var materials = Convert.ToInt32(materialCommand.ExecuteScalar());
        return new SutSummary(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name,
            ribbon, spray, pattern, materials, brushSize);
    }
}
