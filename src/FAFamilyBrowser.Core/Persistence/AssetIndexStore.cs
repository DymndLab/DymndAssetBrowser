using Microsoft.Data.Sqlite;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.Core.Persistence;

public sealed class AssetIndexStore(string databasePath)
{
    public string DatabasePath { get; } = databasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            CREATE TABLE IF NOT EXISTS asset_index (
                stable_identity TEXT PRIMARY KEY COLLATE NOCASE,
                source_id TEXT NOT NULL,
                source_root TEXT NOT NULL,
                file_path TEXT NOT NULL,
                file_name TEXT NOT NULL,
                relative_path TEXT NOT NULL,
                raw_tokens TEXT NOT NULL,
                group_name TEXT NOT NULL,
                subgroup TEXT NOT NULL,
                material TEXT NOT NULL,
                style TEXT NOT NULL,
                theme TEXT NOT NULL,
                family TEXT NOT NULL,
                variant TEXT NOT NULL,
                part_type TEXT NOT NULL,
                part_variant TEXT NOT NULL,
                encoded_size TEXT NOT NULL,
                domain TEXT NOT NULL,
                parse_confidence REAL NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_asset_source ON asset_index(source_id);
            DROP INDEX IF EXISTS ix_asset_group_subgroup;
            DROP INDEX IF EXISTS ix_asset_material;
            DROP INDEX IF EXISTS ix_asset_style;
            DROP INDEX IF EXISTS ix_asset_theme;
            DROP INDEX IF EXISTS ix_asset_family;
            DROP INDEX IF EXISTS ix_asset_variant;
            PRAGMA user_version=1;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<AssetRecord>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DatabasePath)) return [];
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_id, source_root, file_path, file_name, relative_path, raw_tokens,
                   group_name, subgroup, material, style, theme, family, variant,
                   part_type, part_variant, encoded_size, domain, parse_confidence
            FROM asset_index
            ORDER BY group_name COLLATE NOCASE, subgroup COLLATE NOCASE, material COLLATE NOCASE,
                     style COLLATE NOCASE, theme COLLATE NOCASE, family COLLATE NOCASE,
                     variant COLLATE NOCASE, file_name COLLATE NOCASE;
            """;
        var result = new List<AssetRecord>();
        var pool = new StringPool();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AssetRecord
            {
                SourceId = pool.Get(reader.GetString(0)),
                SourceRoot = pool.Get(reader.GetString(1)),
                FilePath = reader.GetString(2),
                FileName = reader.GetString(3),
                RelativePath = reader.GetString(4),
                RawTokens = reader.GetString(5),
                Group = pool.Get(reader.GetString(6)),
                SubGroup = pool.Get(reader.GetString(7)),
                Material = pool.Get(reader.GetString(8)),
                Style = pool.Get(reader.GetString(9)),
                Theme = pool.Get(reader.GetString(10)),
                Family = pool.Get(reader.GetString(11)),
                Variant = pool.Get(reader.GetString(12)),
                PartType = pool.Get(reader.GetString(13)),
                PartVariant = pool.Get(reader.GetString(14)),
                EncodedSize = pool.Get(reader.GetString(15)),
                ParseConfidence = reader.GetDouble(17)
            });
        }
        return result;
    }

    public async Task ReplaceAllAsync(IEnumerable<AssetRecord> assets, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM asset_index;";
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertAsync(connection, transaction, assets, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpsertAsync(IEnumerable<AssetRecord> assets, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await InsertAsync(connection, transaction, assets, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReplaceSourceAsync(string sourceId, IEnumerable<AssetRecord> assets,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM asset_index WHERE source_id = $source_id;";
            clear.Parameters.AddWithValue("$source_id", sourceId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertAsync(connection, transaction, assets, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DatabasePath)) return;
        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM asset_index WHERE source_id = $source_id;";
        command.Parameters.AddWithValue("$source_id", sourceId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction,
        IEnumerable<AssetRecord> assets, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO asset_index (
                stable_identity, source_id, source_root, file_path, file_name, relative_path, raw_tokens,
                group_name, subgroup, material, style, theme, family, variant, part_type, part_variant,
                encoded_size, domain, parse_confidence)
            VALUES ($stable_identity, $source_id, $source_root, $file_path, $file_name, $relative_path, $raw_tokens,
                $group_name, $subgroup, $material, $style, $theme, $family, $variant, $part_type, $part_variant,
                $encoded_size, $domain, $parse_confidence)
            ON CONFLICT(stable_identity) DO UPDATE SET
                source_id=excluded.source_id, source_root=excluded.source_root, file_path=excluded.file_path,
                file_name=excluded.file_name, relative_path=excluded.relative_path, raw_tokens=excluded.raw_tokens,
                group_name=excluded.group_name, subgroup=excluded.subgroup, material=excluded.material,
                style=excluded.style, theme=excluded.theme, family=excluded.family, variant=excluded.variant,
                part_type=excluded.part_type, part_variant=excluded.part_variant, encoded_size=excluded.encoded_size,
                domain=excluded.domain, parse_confidence=excluded.parse_confidence;
            """;
        var parameters = new[]
        {
            "$stable_identity", "$source_id", "$source_root", "$file_path", "$file_name", "$relative_path", "$raw_tokens",
            "$group_name", "$subgroup", "$material", "$style", "$theme", "$family", "$variant", "$part_type",
            "$part_variant", "$encoded_size", "$domain", "$parse_confidence"
        }.Select(name => command.Parameters.Add(name, SqliteType.Text)).ToArray();
        parameters[18].SqliteType = SqliteType.Real;
        await command.PrepareAsync(cancellationToken);

        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new object[]
            {
                asset.StableIdentity, asset.SourceId, asset.SourceRoot, asset.FilePath, asset.FileName,
                asset.RelativePath, asset.RawTokens, asset.Group, asset.SubGroup, asset.Material, asset.Style,
                asset.Theme, asset.Family, asset.Variant, asset.PartType, asset.PartVariant, asset.EncodedSize,
                string.Empty, asset.ParseConfidence
            };
            for (var index = 0; index < parameters.Length; index++) parameters[index].Value = values[index];
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private SqliteConnection Open() => new($"Data Source={DatabasePath};Mode=ReadWriteCreate;Cache=Shared");

    private sealed class StringPool
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        public string Get(string value)
        {
            if (_values.TryGetValue(value, out var shared)) return shared;
            _values[value] = value;
            return value;
        }
    }
}
