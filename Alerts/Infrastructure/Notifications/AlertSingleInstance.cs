using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Project_auto_push_to_gitlab.Alerts.Infrastructure.Notifications;

/// <summary>以使用者專用Mutex及CurrentUserOnly Pipe保持單一起動，轉送履歷ID而非任意指令。</summary>
public sealed class AlertSingleInstance : IAsyncDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipe;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;
    public bool IsPrimary { get; }
    public event Action<Guid?>? Activated;
    public string? Error { get; private set; }

    /// <summary>建立目前Windows使用者專用的單一起動識別，不啟動畫面或DB。</summary>
    /// <param name="testScope">測試可指定隔離識別；null使用正式應用程式識別，內容不形成路徑。</param>
    /// <remarks>UI：Program在啟動任何服務之前使用；避免二重排程與MCP重複啟動。</remarks>
    public AlertSingleInstance(string? testScope = null)
    {
        // 1151005 codex GPT-6_しずくちゃん：同じWindowsユーザーのアプリだけを単一起動にします。
        string user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user + ":" + testScope)))[..24];
        _pipe = "AutoCallGit.Alerts." + key;
        _mutex = new Mutex(false, "Global\\" + _pipe, out bool primary);
        IsPrimary = primary;
    }

    /// <summary>既存プロセスへ履歷ID或通常の畫面表示要求を送信。</summary>
    /// <param name="id">通知履歷ID；null表示一般開啟。</param><returns>送信完成Task，接續失敗拋出。</returns>
    /// <remarks>UI：Program的第二實例直接呼叫；第一實例經Activated開啟FrmAlerts。</remarks>
    public async Task RedirectAsync(Guid? id)
    {
        // 1151005 codex GPT-6_しずくちゃん：二つ目のプロセスから既存画面へ履歴IDを送ります。
        using NamedPipeClientStream client = new(".", _pipe, PipeDirection.Out, PipeOptions.Asynchronous);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
        await using StreamWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync(id?.ToString("D") ?? "open").ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>開始目前使用者限定的背景啟動接收。</summary>
    /// <remarks>UI：Program呼叫；Activated由UI路由轉送到FrmAlerts。</remarks>
    public void Start()
    {
        // 1151005 codex GPT-6_しずくちゃん：他のWindowsユーザーからのIPCを許可しません。
        if (!IsPrimary || _listener is not null) return;
        _listener = Task.Run(ListenAsync);
    }

    /// <summary>接收有限長度Guid或open，不接受檔案路徑、SQL或其他命令。</summary>
    /// <returns>停止要求前持續接收的Task。</returns><remarks>UI：Program經Activated間接顯示FrmAlerts。</remarks>
    private async Task ListenAsync()
    {
        // 1151005 codex GPT-6_しずくちゃん：起動要求を短い固定形式に限定します。
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using NamedPipeServerStream server = new(_pipe, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using StreamReader reader = new(server, Encoding.UTF8, leaveOpen: true);
                using CancellationTokenSource readLimit = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                readLimit.CancelAfter(TimeSpan.FromSeconds(5));
                char[] buffer = new char[40];
                int length = 0;
                while (length < buffer.Length)
                {
                    int received = await reader.ReadAsync(buffer.AsMemory(length), readLimit.Token).ConfigureAwait(false);
                    if (received == 0) break;
                    length += received;
                    if (Array.IndexOf(buffer, '\n', 0, length) >= 0) break;
                }
                string value = new string(buffer, 0, length).Trim();
                if (value == "open") Activated?.Invoke(null);
                else if (Guid.TryParse(value, out Guid id) && id != Guid.Empty) Activated?.Invoke(id);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { Error = "啟動接收已停止。"; }
        catch (Exception) { Error = "啟動接收失敗；請重新啟動應用程式。"; }
    }

    /// <summary>停止Pipe接收並釋放Mutex，允許下一次啟動。</summary>
    /// <returns>接收已取消且資源釋放完成的ValueTask。</returns><remarks>UI：Program結束時直接呼叫。</remarks>
    public async ValueTask DisposeAsync()
    {
        // 1151005 codex GPT-6_しずくちゃん：終了時に起動受付とプロセス識別を解放します。
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_listener is not null) await _listener.ConfigureAwait(false);
        _mutex.Dispose();
        _stop.Dispose();
    }
}
