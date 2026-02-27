namespace MozzartPrintReceiver.Services;

public interface IJobDeduplicator
{
    bool IsDuplicate(string jobId);

    void Remember(string jobId);
}
