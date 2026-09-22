using System.Linq.Expressions;
using Corely.Common.Filtering;
using Corely.Common.Filtering.Ordering;
using Corely.DataAccess.Interfaces.Repos;

namespace Corely.Billing.Models;

internal static class ListQueryHelper
{
    public static async Task<RetrieveListResult<TModel>> ExecuteListAsync<TModel, TEntity>(
        IReadonlyRepo<TEntity> repo,
        Expression<Func<TEntity, bool>> scopePredicate,
        FilterBuilder<TModel>? filter,
        OrderBuilder<TModel>? order,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> defaultOrder,
        int skip,
        int take,
        Func<TEntity, TModel> toModel,
        CancellationToken ct
    )
        where TEntity : class
    {
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "Must be non-negative.");
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take), "Must be positive.");

        var predicate = scopePredicate;
        var filterExpression = filter?.Build();
        if (filterExpression != null)
        {
            var mappedFilter = ExpressionMapper.MapPredicate<TModel, TEntity>(filterExpression);
            var param = Expression.Parameter(typeof(TEntity), "e");
            predicate = Expression.Lambda<Func<TEntity, bool>>(
                Expression.AndAlso(
                    Expression.Invoke(scopePredicate, param),
                    Expression.Invoke(mappedFilter, param)
                ),
                param
            );
        }

        var entities = await repo.QueryAsync(
            q =>
            {
                var query = q.Where(predicate);
                query =
                    order != null
                        ? ExpressionMapper.ApplyOrder<TModel, TEntity>(query, order)
                        : defaultOrder(query);
                return query.Skip(skip).Take(take);
            },
            ct
        );

        var totalCount = await repo.CountAsync(predicate, ct);

        return new RetrieveListResult<TModel>(
            RetrieveResultCode.Success,
            string.Empty,
            PagedResult<TModel>.Create([.. entities.Select(toModel)], totalCount, skip, take)
        );
    }
}
