using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SonyHeadphonesNet.Transport;

/// <summary>
/// Native Windows Winsock RFCOMM Bluetooth transport implementation using ws2_32.dll (AF_BTH).
/// Does not require third-party dependencies.
/// </summary>
public sealed class WindowsRfcommTransport : IBluetoothTransport
{
    private const int AF_BTH = 32;
    private const int BTHPROTO_RFCOMM = 3;
    private const int SOL_RFCOMM = 0x03;
    private const int SO_BTH_AUTHENTICATE = unchecked((int)0x80000001);
    private const int SO_BTH_ENCRYPT = 0x00000002;
    private const nint INVALID_SOCKET = -1;
    private const int SOCKET_ERROR = -1;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SOCKADDR_BTH
    {
        public ushort addressFamily;
        public ulong btAddr;
        public Guid serviceClassId;
        public uint port;
    }

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int WSAStartup(ushort wVersionRequested, byte[] lpWSAData);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern nint socket(int af, int type, int protocol);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int connect(nint s, ref SOCKADDR_BTH name, int namelen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int send(nint s, byte[] buf, int len, int flags);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int recv(nint s, byte[] buf, int len, int flags);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int closesocket(nint s);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int setsockopt(nint s, int level, int optname, ref int optval, int optlen);

    private static bool s_wsaInitialized;
    private static readonly object s_wsaLock = new();

    private static void EnsureWsaInitialized()
    {
        if (s_wsaInitialized) return;
        lock (s_wsaLock)
        {
            if (s_wsaInitialized) return;
            byte[] wsaData = new byte[512];
            int ret = WSAStartup(0x0202, wsaData); // Winsock version 2.2
            if (ret != 0)
            {
                throw new SocketException(ret);
            }
            s_wsaInitialized = true;
        }
    }

    private nint _socket = INVALID_SOCKET;
    private Thread? _receiveThread;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public bool IsConnected => _socket != INVALID_SOCKET && !_disposed;

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<string?>? Disconnected;

    public Task ConnectAsync(string macAddress, Guid serviceUuid, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsConnected)
            throw new InvalidOperationException("Already connected.");

        ulong btAddr = ParseMacAddress(macAddress);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureWsaInitialized();

            _socket = socket(AF_BTH, (int)SocketType.Stream, BTHPROTO_RFCOMM);
            if (_socket == INVALID_SOCKET)
            {
                int error = Marshal.GetLastWin32Error();
                throw new SocketException(error);
            }

            // Enable Authentication and Encryption
            int enable = 1;
            setsockopt(_socket, SOL_RFCOMM, SO_BTH_AUTHENTICATE, ref enable, sizeof(int));
            setsockopt(_socket, SOL_RFCOMM, SO_BTH_ENCRYPT, ref enable, sizeof(int));

            var sa = new SOCKADDR_BTH
            {
                addressFamily = AF_BTH,
                btAddr = btAddr,
                serviceClassId = serviceUuid,
                port = 0 // Port 0 uses SDP query
            };

            int ret = connect(_socket, ref sa, Marshal.SizeOf<SOCKADDR_BTH>());
            if (ret == SOCKET_ERROR)
            {
                int error = Marshal.GetLastWin32Error();
                closesocket(_socket);
                _socket = INVALID_SOCKET;
                throw new SocketException(error);
            }

            _cts = new CancellationTokenSource();
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "SonyHeadphones_BT_Recv"
            };
            _receiveThread.Start();
        }, cancellationToken);
    }

    public Task DisconnectAsync()
    {
        if (_socket != INVALID_SOCKET)
        {
            _cts?.Cancel();
            closesocket(_socket);
            _socket = INVALID_SOCKET;
            Disconnected?.Invoke(this, "Disconnected by user.");
        }
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected)
            throw new InvalidOperationException("Not connected.");

        return Task.Run(() =>
        {
            byte[] bytes = data.ToArray();
            int sent = 0;
            while (sent < bytes.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] chunk = bytes;
                if (sent > 0)
                {
                    chunk = new byte[bytes.Length - sent];
                    Array.Copy(bytes, sent, chunk, 0, chunk.Length);
                }

                int result = send(_socket, chunk, chunk.Length, 0);
                if (result == SOCKET_ERROR)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new SocketException(error);
                }
                sent += result;
            }
        }, cancellationToken);
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        try
        {
            while (IsConnected && !(_cts?.IsCancellationRequested ?? true))
            {
                int bytesRead = recv(_socket, buffer, buffer.Length, 0);
                if (bytesRead <= 0)
                {
                    // Disconnected or closed
                    break;
                }

                byte[] received = new byte[bytesRead];
                Array.Copy(buffer, 0, received, 0, bytesRead);
                DataReceived?.Invoke(this, received);
            }
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke(this, ex.Message);
            return;
        }

        if (_socket != INVALID_SOCKET)
        {
            closesocket(_socket);
            _socket = INVALID_SOCKET;
        }
        Disconnected?.Invoke(this, "Bluetooth link disconnected.");
    }

    private static ulong ParseMacAddress(string mac)
    {
        string[] parts = mac.Split(new[] { ':', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 6)
        {
            ulong addr = 0;
            for (int i = 0; i < 6; i++)
            {
                byte b = byte.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                addr = (addr << 8) | b;
            }
            return addr;
        }

        string cleanMac = mac.Replace(":", "").Replace("-", "").Replace(" ", "").Trim();
        if (cleanMac.Length == 12 && ulong.TryParse(cleanMac, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong result))
        {
            return result;
        }

        throw new ArgumentException($"Invalid MAC address format: {mac}. Expected 6 hex pairs (e.g. AA:BB:CC:DD:EE:FF).", nameof(mac));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            DisconnectAsync().GetAwaiter().GetResult();
            _cts?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
