namespace Infrastructure.Services;

public sealed class EmailDeliveryHealthState
{
    private int _hasFailure;

    public bool HasFailure => Volatile.Read(ref _hasFailure) == 1;

    public void ReportSuccess() => Interlocked.Exchange(ref _hasFailure, 0);

    public void ReportFailure() => Interlocked.Exchange(ref _hasFailure, 1);
}
