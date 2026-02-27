using MozzartPrintHub.WinForms.Domain;

namespace MozzartPrintHub.WinForms.Services;

public sealed class EmulatorStore
{
    private readonly int _maxEntries;
    private readonly LinkedList<EmulatorJob> _jobs = new();
    private readonly object _gate = new();

    public EmulatorStore(int maxEntries = 200)
    {
        _maxEntries = maxEntries;
    }

    public void Add(EmulatorJob job)
    {
        lock (_gate)
        {
            _jobs.AddFirst(job);
            while (_jobs.Count > _maxEntries)
            {
                _jobs.RemoveLast();
            }
        }
    }

    public EmulatorJob? GetLast()
    {
        lock (_gate)
        {
            return _jobs.First?.Value;
        }
    }

    public IReadOnlyList<EmulatorJob> GetHistory(int take)
    {
        lock (_gate)
        {
            return _jobs.Take(Math.Clamp(take, 1, _maxEntries)).ToList();
        }
    }
}
