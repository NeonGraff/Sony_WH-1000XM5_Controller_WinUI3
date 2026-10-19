namespace SonyHeadphonesNet.Transport;

/// <summary>
/// Abstraction for the underlying Bluetooth communication channel.
/// Allows swapping between Windows Sockets, WinRT RFCOMM, 32feet.NET, or Mocks for testing.
/// </summary>
public interface IBluetoothTransport : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets whether the transport is currently connected to the device.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the Bluetooth device by MAC address and RFCOMM Service UUID.
    /// </summary>
    /// <param name="macAddress">Bluetooth MAC address in form "XX:XX:XX:XX:XX:XX" or "XXXXXXXXXXXX"</param>
    /// <param name="serviceUuid">Service UUID (e.g. 956C7B26-D49A-4BA8-B03F-B17D393CB6E2 for XM5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ConnectAsync(string macAddress, Guid serviceUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the current session.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Sends raw bytes over the Bluetooth connection.
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fired when incoming raw bytes are received from the Bluetooth connection.
    /// </summary>
    event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// Fired when the connection is disconnected or lost.
    /// </summary>
    event EventHandler<string?>? Disconnected;
}
