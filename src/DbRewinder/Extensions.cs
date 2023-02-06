using System.Data;
using System.Data.Common;

namespace DbRewinder;

internal static class Extensions
{
    internal static DataTable SecureCopyToDataTable(this DataRow[] rows)
    {
        if (rows == null || rows.Length == 0) return new DataTable();
        return rows.CopyToDataTable();
    }

    internal static string? WithMaxLength(this string value, int maxLength)
    {
        return value?.Substring(0, Math.Min(value.Length, maxLength));
    }

    internal static async Task<DataTable> ExecuteDataTableAsync(this DbCommand command)
    {
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        DataTable schema = await reader.GetSchemaTableAsync().ConfigureAwait(false) ?? new DataTable();
        DataTable result = new DataTable();

        foreach (DataRow r in schema.Rows)
        {
            if (!result.Columns.Contains(r["ColumnName"]?.ToString() ?? string.Empty))
            {
                DataColumn col = new DataColumn()
                {
                    ColumnName = r["ColumnName"].ToString(),
                    Unique = Convert.ToBoolean(r["IsUnique"]),
                    AllowDBNull = Convert.ToBoolean(r["AllowDBNull"]),
                    ReadOnly = Convert.ToBoolean(r["IsReadOnly"])
                };
                result.Columns.Add(col);
            }
        }

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            DataRow novaLinha = result.NewRow();
            for (int i = 0; i < result.Columns.Count; i++)
            {
                novaLinha[i] = reader.GetValue(i);
            }
            result.Rows.Add(novaLinha);
        }

        return result;
    }
}