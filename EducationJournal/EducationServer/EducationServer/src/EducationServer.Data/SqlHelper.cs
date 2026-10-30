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
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (targetType == typeof(int)) return (T)(object)Convert.ToInt32(result);
        if (targetType == typeof(long)) return (T)(object)Convert.ToInt64(result);
        if (targetType == typeof(short)) return (T)(object)Convert.ToInt16(result);
        if (targetType == typeof(byte)) return (T)(object)Convert.ToByte(result);
        if (targetType == typeof(decimal)) return (T)(object)Convert.ToDecimal(result);
        if (targetType == typeof(double)) return (T)(object)Convert.ToDouble(result);
        if (targetType == typeof(bool)) return (T)(object)Convert.ToBoolean(result);
        if (targetType == typeof(string)) return (T)(object)Convert.ToString(result)!;
        if (targetType == typeof(Guid)) return (T)(object)(Guid)result;
        if (targetType == typeof(DateTime)) return (T)(object)Convert.ToDateTime(result);
        if (targetType.IsEnum)
            return (T)Enum.ToObject(targetType, result);

        return (T)Convert.ChangeType(result, targetType);
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