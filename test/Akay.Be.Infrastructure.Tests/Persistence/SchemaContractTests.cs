using System.Data.Common;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Tests.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public sealed class SchemaContractTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task BaselineScripts_MatchEfModel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var services = await postgres.CreateServicesAsync(createSchema: false);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "db");
        var scripts = Directory.GetFiles(scriptDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(scripts);

        foreach (var scriptPath in scripts)
        {
            var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            await context.Database.ExecuteSqlRawAsync(script, cancellationToken);
        }

        var actual = await ReadActualSchemaAsync(context, cancellationToken);
        var expected = BuildExpectedSchema(context.Model);

        Assert.Equal(expected.Tables.Order(StringComparer.Ordinal),
                     actual.Tables.Order(StringComparer.Ordinal));

        Assert.Equal(expected.Columns.OrderBy(column => column.Key, StringComparer.Ordinal),
                     actual.Columns.OrderBy(column => column.Key, StringComparer.Ordinal));

        Assert.Equal(expected.PrimaryKeys.OrderBy(key => key.Key, StringComparer.Ordinal),
                     actual.PrimaryKeys.OrderBy(key => key.Key, StringComparer.Ordinal));

        Assert.Equal(expected.ForeignKeys.OrderBy(key => key.Key, StringComparer.Ordinal),
                     actual.ForeignKeys.OrderBy(key => key.Key, StringComparer.Ordinal));

        Assert.Equal(expected.Indexes.OrderBy(index => index.Key, StringComparer.Ordinal),
                     actual.Indexes.OrderBy(index => index.Key, StringComparer.Ordinal));
    }

    private static ExpectedSchema BuildExpectedSchema(IModel model)
    {
        var tables = new HashSet<string>(StringComparer.Ordinal);
        var columns = new List<ColumnInfo>();
        var primaryKeys = new List<PrimaryKeyInfo>();
        var foreignKeys = new List<ForeignKeyInfo>();
        var indexes = new List<IndexInfo>();

        foreach (var entity in model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is null)
                continue;

            var schema = entity.GetSchema() ?? "public";
            tables.Add($"{schema}.{tableName}");

            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (columnName is null)
                    continue;

                columns.Add(new ColumnInfo(schema,
                                           tableName,
                                           columnName,
                                           property.GetRelationalTypeMapping().StoreType,
                                           property.IsNullable));
            }

            if (entity.FindPrimaryKey()?.GetName() is { } primaryKeyName)
                primaryKeys.Add(new PrimaryKeyInfo(schema, tableName, primaryKeyName));

            foreach (var index in entity.GetIndexes())
            {
                var indexName = index.GetDatabaseName() ?? index.GetDefaultDatabaseName();
                if (indexName is null)
                    continue;

                var indexColumns = string.Join(",",
                                               index.Properties.Select(property => property.GetColumnName()));

                indexes.Add(new IndexInfo(schema, tableName, indexName, index.IsUnique, indexColumns));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var constraintName = foreignKey.GetConstraintName();
                var principalTable = foreignKey.PrincipalEntityType.GetTableName();
                if (constraintName is null || principalTable is null)
                    continue;

                var principalSchema = foreignKey.PrincipalEntityType.GetSchema() ?? "public";
                var keyColumns = string.Join(",",
                                             foreignKey.Properties.Select(property => property.GetColumnName()));

                foreignKeys.Add(new ForeignKeyInfo(schema,
                                                   tableName,
                                                   constraintName,
                                                   principalSchema,
                                                   principalTable,
                                                   keyColumns));
            }
        }

        return new ExpectedSchema(tables.ToList(), columns, primaryKeys, foreignKeys, indexes);
    }

    private static async Task<ActualSchema> ReadActualSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken);

        var tables = new HashSet<string>(StringComparer.Ordinal);
        var columns = new List<ColumnInfo>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT n.nspname,
                       c.relname,
                       a.attname,
                       format_type(a.atttypid, a.atttypmod),
                       NOT a.attnotnull
                FROM pg_attribute a
                JOIN pg_class c ON c.oid = a.attrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind = 'r'
                  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
                  AND a.attnum > 0
                  AND NOT a.attisdropped
                ORDER BY n.nspname, c.relname, a.attnum
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var schema = reader.GetString(0);
                var table = reader.GetString(1);
                tables.Add($"{schema}.{table}");
                columns.Add(new ColumnInfo(schema,
                                           table,
                                           reader.GetString(2),
                                           reader.GetString(3),
                                           reader.GetBoolean(4)));
            }
        }

        var primaryKeys = await ReadPrimaryKeysAsync(connection, cancellationToken);
        var foreignKeys = await ReadForeignKeysAsync(connection, cancellationToken);
        var indexes = await ReadIndexesAsync(connection, cancellationToken);

        return new ActualSchema(tables.ToList(), columns, primaryKeys, foreignKeys, indexes);
    }

    private static async Task<List<PrimaryKeyInfo>> ReadPrimaryKeysAsync(DbConnection connection,
                                                                         CancellationToken cancellationToken)
    {
        var result = new List<PrimaryKeyInfo>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.nspname, c.relname, con.conname
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE con.contype = 'p'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PrimaryKeyInfo(reader.GetString(0),
                                          reader.GetString(1),
                                          reader.GetString(2)));
        }

        return result;
    }

    private static async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(DbConnection connection,
                                                                         CancellationToken cancellationToken)
    {
        var groups = new Dictionary<(string Schema, string Table, string Name, string PrincipalSchema, string PrincipalTable),
                                    List<string>>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.nspname,
                   c.relname,
                   con.conname,
                   fn.nspname,
                   fc.relname,
                   a.attname,
                   k.ord
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_class fc ON fc.oid = con.confrelid
            JOIN pg_namespace fn ON fn.oid = fc.relnamespace
            JOIN LATERAL unnest(con.conkey, con.confkey) WITH ORDINALITY AS k(attnum, fattnum, ord) ON true
            JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum
            WHERE con.contype = 'f'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY n.nspname, c.relname, con.conname, k.ord
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = (reader.GetString(0),
                       reader.GetString(1),
                       reader.GetString(2),
                       reader.GetString(3),
                       reader.GetString(4));

            if (!groups.TryGetValue(key, out var keyColumns))
            {
                keyColumns = [];
                groups[key] = keyColumns;
            }

            keyColumns.Add(reader.GetString(5));
        }

        return groups
            .Select(group => new ForeignKeyInfo(group.Key.Schema,
                                                group.Key.Table,
                                                group.Key.Name,
                                                group.Key.PrincipalSchema,
                                                group.Key.PrincipalTable,
                                                string.Join(",", group.Value)))
            .ToList();
    }

    private static async Task<List<IndexInfo>> ReadIndexesAsync(DbConnection connection,
                                                                 CancellationToken cancellationToken)
    {
        var result = new List<IndexInfo>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.nspname,
                   c.relname,
                   i.relname,
                   ix.indisunique,
                   (SELECT string_agg(a.attname::text, ',' ORDER BY k.ord)
                    FROM unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ord)
                    JOIN pg_attribute a ON a.attrelid = ix.indrelid AND a.attnum = k.attnum)
            FROM pg_index ix
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_class c ON c.oid = ix.indrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
              AND NOT EXISTS (
                  SELECT 1
                  FROM pg_constraint con
                  WHERE con.conindid = ix.indexrelid
                    AND con.contype = 'p'
              )
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new IndexInfo(reader.GetString(0),
                                     reader.GetString(1),
                                     reader.GetString(2),
                                     reader.GetBoolean(3),
                                     reader.GetString(4)));
        }

        return result;
    }

    private sealed record ExpectedSchema(List<string> Tables,
                                         List<ColumnInfo> Columns,
                                         List<PrimaryKeyInfo> PrimaryKeys,
                                         List<ForeignKeyInfo> ForeignKeys,
                                         List<IndexInfo> Indexes);

    private sealed record ActualSchema(List<string> Tables,
                                       List<ColumnInfo> Columns,
                                       List<PrimaryKeyInfo> PrimaryKeys,
                                       List<ForeignKeyInfo> ForeignKeys,
                                       List<IndexInfo> Indexes);

    private sealed record ColumnInfo(string Schema, string Table, string Column, string Type, bool Nullable)
    {
        public string Key => $"{Schema}.{Table}.{Column}";
    }

    private sealed record PrimaryKeyInfo(string Schema, string Table, string Name)
    {
        public string Key => $"{Schema}.{Table}.{Name}";
    }

    private sealed record ForeignKeyInfo(string Schema,
                                         string Table,
                                         string Name,
                                         string PrincipalSchema,
                                         string PrincipalTable,
                                         string Columns)
    {
        public string Key => $"{Schema}.{Table}.{Name}.{Columns}";
    }

    private sealed record IndexInfo(string Schema, string Table, string Name, bool Unique, string Columns)
    {
        public string Key => $"{Schema}.{Table}.{Name}.{Columns}";
    }
}
