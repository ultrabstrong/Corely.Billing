using Corely.Billing.Grants.Models;

namespace Corely.Billing.Grants.Services;

public interface IGrantWriter
{
    Task<SaveGrantResult> SaveAsync(Grant grant, CancellationToken ct = default);

    Task<UpdateGrantResult> UpdateAsync(Grant grant, CancellationToken ct = default);

    Task<DeleteGrantResult> DeleteAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    );
}
