using System.Threading;

public struct CesSpinLock
{
    const int UNLOCKED = 0;
    const int LOCKED = 1;

    int _lock;

    public static CesSpinLock Create() => new()
    {
        _lock = UNLOCKED,
    };

    public readonly bool IsLocked => _lock == LOCKED;

    public void Lock()
    {
        while (Interlocked.CompareExchange(ref _lock, LOCKED, UNLOCKED) == LOCKED) { }
    }

    public void Unlock()
    {
        Interlocked.Exchange(ref _lock, UNLOCKED);
    }
}
