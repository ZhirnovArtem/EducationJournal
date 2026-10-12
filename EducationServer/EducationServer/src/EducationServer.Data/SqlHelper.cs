using System.Data;
using System.Data.Common;

namespace EducationServer.Data;

public static class SqlHelper
{
    public static DbCommand Bind(this DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
        return cmd;
    }

    public static async Task<int> ExecuteAsync(
        this DbConnection conn, string sql,
        Action<DbCommand>? bind = null,
        CancellationToken ct = default)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;
        bind?.Invoke(cmd);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public static async Task<T?> ExecuteScalarAsync<T>(
        this DbConnection conn, string sql,
        Action<DbCommand>? bind = null,
        CancellationToken ct = default)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind?.Invoke(cmd);
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null || result is DBNull) return default;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    public static async Task<List<T>> QueryAsync<T>(
        this DbConnection conn, string sql,
        Func<DbDataReader, T> map,
        Action<DbCommand>? bind = null,
        CancellationToken ct = default)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind?.Invoke(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<T>();
        while (await reader.ReadAsync(ct))
            list.Add(map(reader));
        return list;
    }

    public static async Task<T?> QueryFirstAsync<T>(
        this DbConnection conn, string sql,
        Func<DbDataReader, T> map,
        Action<DbCommand>? bind = null,
        CancellationToken ct = default)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind?.Invoke(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? map(reader) : default;
    }
}