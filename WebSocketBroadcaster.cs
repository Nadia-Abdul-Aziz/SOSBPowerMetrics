using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// self-hosted websocket server. clients connect to ws://<host>:<port>/sosb_data_stream
// and get one JSON frame per poll cycle. broadcasting never blocks the poll loop:
// a client that can't keep up misses frames rather than stalling the sensor reads.
class WebSocketBroadcaster
{
    public const string Channel = "sosb_data_stream";

    readonly HttpListener _listener = new HttpListener();
    readonly List<Client> _clients = new List<Client>();
    readonly object _clientsLock = new object();
    readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
    readonly string _host;
    readonly int _port;
    bool _started;

    public WebSocketBroadcaster(string host, int port)
    {
        _host = host;
        _port = port;
        _listener.Prefixes.Add($"http://{host}:{port}/");
    }

    public int ClientCount
    {
        get { lock (_clientsLock) return _clients.Count; }
    }

    // a failure here is not fatal: the OSC feed and the CSV log should keep
    // working even if the websocket port is taken or blocked.
    public void Start()
    {
        try
        {
            _listener.Start();
            _started = true;
            Logger.Always($"Websocket: ws://{_host}:{_port}/{Channel}\n");
            _ = Task.Run(AcceptLoop);
        }
        catch (Exception e)
        {
            Logger.Always($"Websocket: disabled - {e.Message}\n");
        }
    }

    public void Stop()
    {
        if (!_started)
            return;

        _shutdown.Cancel();

        List<Client> snapshot;
        lock (_clientsLock)
        {
            snapshot = new List<Client>(_clients);
            _clients.Clear();
        }
        foreach (var client in snapshot)
            client.Kill();

        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
    }

    // called once per poll cycle from the main loop
    public void Broadcast(string dataJson)
    {
        if (!_started)
            return;

        List<Client> snapshot;
        lock (_clientsLock)
        {
            if (_clients.Count == 0)
                return;
            snapshot = new List<Client>(_clients);
        }

        byte[] frame = Encoding.UTF8.GetBytes(Envelope(dataJson));

        foreach (var client in snapshot)
        {
            if (client.Socket.State != WebSocketState.Open)
            {
                Remove(client);
                continue;
            }
            client.TrySend(frame, _shutdown.Token);
        }
    }

    // Channel is a literal and the timestamp is ISO-8601, so neither needs escaping,
    // and dataJson is already serialised: splice it in rather than re-encoding it.
    static string Envelope(string dataJson)
    {
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        return $"{{\"channel\":\"{Channel}\",\"timestamp\":\"{timestamp}\",\"data\":{dataJson}}}";
    }

    async Task AcceptLoop()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                return; // listener stopped
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            // only serve the one channel this tool publishes
            string path = context.Request.Url.AbsolutePath.Trim('/');
            if (!string.Equals(path, Channel, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                continue;
            }

            try
            {
                var accepted = await context.AcceptWebSocketAsync(null);
                var client = new Client(accepted.WebSocket);
                lock (_clientsLock)
                    _clients.Add(client);
                Logger.Always($"Websocket: client connected ({ClientCount} total)");

                _ = Task.Run(async () =>
                {
                    await client.ReceiveLoop(_shutdown.Token);
                    Remove(client);
                    Logger.Always($"Websocket: client disconnected ({ClientCount} remaining)");
                });
            }
            catch (Exception e)
            {
                Logger.Log($"Websocket: handshake failed - {e.Message}");
            }
        }
    }

    void Remove(Client client)
    {
        lock (_clientsLock)
            _clients.Remove(client);
        client.Kill();
    }

    // one connected client
    class Client
    {
        public readonly WebSocket Socket;
        int _sending;

        public Client(WebSocket socket)
        {
            Socket = socket;
        }

        // websockets don't allow overlapping SendAsync calls on the same socket, so a
        // frame that arrives while the previous one is still going out gets dropped.
        // that's the right trade for 4Hz telemetry: a stale frame is worthless anyway.
        public void TrySend(byte[] frame, CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref _sending, 1, 0) != 0)
                return;

            _ = SendAsync(frame, token);
        }

        async Task SendAsync(byte[] frame, CancellationToken token)
        {
            try
            {
                await Socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Text, true, token);
            }
            catch
            {
                try { Socket.Abort(); } catch { }
            }
            finally
            {
                Interlocked.Exchange(ref _sending, 0);
            }
        }

        // we don't expect inbound messages. this loop exists to notice a client going
        // away promptly and to complete the close handshake when it does.
        public async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[1024];
            try
            {
                while (Socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await Socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", token);
                        return;
                    }
                }
            }
            catch { }
        }

        public void Kill()
        {
            try { Socket.Abort(); } catch { }
            try { Socket.Dispose(); } catch { }
        }
    }
}
