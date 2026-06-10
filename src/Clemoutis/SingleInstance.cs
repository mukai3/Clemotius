namespace Clemoutis;

/// <summary>
/// Mutex による多重起動防止。2つ目の起動は既存インスタンスへ
/// 「設定画面を開く」要求をイベントで通知して終了する。
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = "Clemoutis_SingleInstance";
    private const string ShowSettingsEventName = "Clemoutis_ShowSettings";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSettingsEvent;

    private SingleInstance(Mutex mutex, EventWaitHandle showSettingsEvent)
    {
        _mutex = mutex;
        _showSettingsEvent = showSettingsEvent;
    }

    /// <summary>1つ目の起動なら取得成功、2つ目以降なら null。</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }
        var ev = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
        return new SingleInstance(mutex, ev);
    }

    /// <summary>既存インスタンスに設定画面を開くよう通知する（2つ目の起動側）。</summary>
    public static void SignalExisting()
    {
        if (EventWaitHandle.TryOpenExisting(ShowSettingsEventName, out var ev))
        {
            using (ev)
            {
                ev.Set();
            }
        }
    }

    /// <summary>UI スレッドのタイマーからポーリングして、通知が来ていれば true。</summary>
    public bool ConsumeShowSettingsRequest() => _showSettingsEvent.WaitOne(0);

    public void Dispose()
    {
        _showSettingsEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
