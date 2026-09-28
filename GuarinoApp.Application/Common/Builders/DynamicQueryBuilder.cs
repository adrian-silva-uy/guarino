using GuarinoApp.Application.Common.Filters;
using Microsoft.Data.SqlClient;
using System.Text;

namespace GuarinoApp.Application.Common.Builders;
public static class DynamicQueryBuilder
    {
    private static readonly Dictionary<string, string> _fieldMap = new()
    {
        { "Año", "YEAR(LE.fechaProduccion)" },
        { "Mes", "MONTH(LE.fechaProduccion)" },
        { "Balanza", "Ba.descripcion" },
        { "Productor", "P.RazonSocial" },
        { "Variedad", "V.Descripcion" },
        { "Especie", "E.Descripcion" },
        { "Zafra", "LE.Zafra" }
    };

    public static (string WhereClause, List<SqlParameter> Parameters) BuildWhereClause(List<Filter> filters)
        {
        var where = new StringBuilder();
        var parameters = new List<SqlParameter>();
        int paramIndex = 0;

        if (filters == null || !filters.Any())
            return ("", parameters);

        for (int i = 0; i < filters.Count; i++)
            {
            var filter = filters[i];
            if (string.IsNullOrEmpty(filter.Field) || string.IsNullOrEmpty(filter.Operator))
                continue;

            if (!_fieldMap.TryGetValue(filter.Field, out string? fieldExpression))
                continue;

            string condition = "";
            // La lógica forma parte de la estructura SQL: nunca puede venir libremente de la UI.
            string logic = i > 0 && string.Equals(filter.Logic, "OR", StringComparison.OrdinalIgnoreCase)
                ? "OR"
                : "AND";

            switch (filter.Operator)
                {
                case "eq":
                    condition = $"{fieldExpression} = @p{paramIndex}";
                    parameters.Add(new SqlParameter($"@p{paramIndex}", filter.Value ?? ""));
                    paramIndex++;
                    break;
                case "contains":
                    condition = $"{fieldExpression} LIKE @p{paramIndex}";
                    parameters.Add(new SqlParameter($"@p{paramIndex}", $"%{filter.Value}%"));
                    paramIndex++;
                    break;
                case "gt":
                    condition = $"{fieldExpression} > @p{paramIndex}";
                    parameters.Add(new SqlParameter($"@p{paramIndex}", filter.Value ?? ""));
                    paramIndex++;
                    break;
                case "lt":
                    condition = $"{fieldExpression} < @p{paramIndex}";
                    parameters.Add(new SqlParameter($"@p{paramIndex}", filter.Value ?? ""));
                    paramIndex++;
                    break;
                case "between":
                    if (!string.IsNullOrEmpty(filter.Value) && !string.IsNullOrEmpty(filter.ValueTo))
                        {
                        condition = $"{fieldExpression} BETWEEN @p{paramIndex} AND @p{paramIndex + 1}";
                        parameters.Add(new SqlParameter($"@p{paramIndex}", filter.Value));
                        parameters.Add(new SqlParameter($"@p{paramIndex + 1}", filter.ValueTo));
                        paramIndex += 2;
                        }
                    break;
                default:
                    continue;
                }

            if (!string.IsNullOrEmpty(condition))
                {
                if (where.Length > 0)
                    where.Append($" {logic} ");
                where.Append(condition);
                }
            }

        return (where.ToString(), parameters);
        }
    }
