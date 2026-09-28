using GuarinoApp.Application.Common.Filters;
using System.Linq.Expressions;

namespace GuarinoApp.Application.Common.Builders;

public static class LinqDynamicQueryBuilder
    {
    public static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, List<Filter> filters)
        {
        if (filters == null || filters.Count == 0)
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (var filter in filters)
            {
            var property = typeof(T).GetProperty(filter.Field);
            if (property == null) continue;

            var left = Expression.Property(parameter, property);
            Expression? condition = null;

            switch (filter.Operator)
                {
                case "eq":
                    condition = Expression.Equal(left, Expression.Constant(filter.Value));
                    break;
                case "contains":
                    condition = Expression.Call(
                        left,
                        typeof(string).GetMethod("Contains", [typeof(string)])!,
                        Expression.Constant(filter.Value)
                    );
                    break;
                case "gt":
                    condition = Expression.GreaterThan(left, Expression.Constant(Convert.ChangeType(filter.Value, property.PropertyType)));
                    break;
                case "lt":
                    condition = Expression.LessThan(left, Expression.Constant(Convert.ChangeType(filter.Value, property.PropertyType)));
                    break;
                }

            if (condition != null)
                combined = combined == null ? condition : Expression.AndAlso(combined, condition);
            }

        if (combined == null)
            return query;

        var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
        return query.Where(lambda);
        }
    }
