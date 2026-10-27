namespace Corely.Billing.IAM.UnitTests.Authorization;

internal static class TaskExtensions
{
    extension<T>(Task<T> task)
    {
        public async Task<object?> AsResult() => await task;
    }
}
