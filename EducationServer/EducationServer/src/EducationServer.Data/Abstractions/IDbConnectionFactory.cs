using System.Data.Common;

namespace EducationServer.Data.Abstractions;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken ct = default);
}