using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace H2MLauncher.Core.Services
{
    public class GameServerCommunication : IAsyncDisposable
    {
        private readonly UdpClient _client;
        private readonly Dictionary<string, List<Action<ReceivedCommandMessage>>> _commandHandlers = [];

        private readonly CancellationTokenSource _listeningCancellation;
        private readonly Task _listeningTask;
        private readonly SynchronizationContext _synchronizationContext;

        const uint IOC_IN = 0x80000000U;
        const uint IOC_VENDOR = 0x18000000U;

        /// <summary>
        /// Controls whether UDP PORT_UNREACHABLE messages are reported. 
        /// </summary>
        const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));

        public GameServerCommunication()
        {
            _client = new(AddressFamily.InterNetworkV6);
            _client.Client.DualMode = true; // enables both IPv4 and IPv6
            _client.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, 0)); // bind to any available port
            _client.Client.ReceiveTimeout = 5000;
            _client.Client.SendBufferSize = 1000;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Set control flag to avoid connection reset when port is unreachable
                // (https://stackoverflow.com/a/7478498/4711541
                _client.Client.IOControl(SIO_UDP_CONNRESET, [0x00], null);
            }

            _listeningCancellation = new();
            _listeningTask = Task.Run(ReceiveLoop);

            _synchronizationContext = SynchronizationContext.Current ?? new();
        }

        private void HandleMessage(UdpReceiveResult result, DateTimeOffset timestamp)
        {
            string receivedMessage = Encoding.UTF8.GetString(result.Buffer);

            // Split the string based on the \xFF\xFF\xFF\xFF delimiter
            bool startsWithDelimiter = IsDelimiterAtStart(result.Buffer);
            if (!startsWithDelimiter)
            {
                // Invalid message: Message does not start with the expected delimiter
                return;
            }

            int indexOfSeparator = receivedMessage.IndexOfAny([' ', '\n']);
            if (indexOfSeparator < 0)
            {
                // No seperator found in message
                return;
            }

            // Extract the command name
            string commandName = receivedMessage[4..indexOfSeparator];

            // Extract the seperator char
            char separator = receivedMessage[indexOfSeparator];

            // Extract the data after the separator char
            string data = receivedMessage[(indexOfSeparator + 1)..];

            if (!_commandHandlers.TryGetValue(commandName.ToLower(), out var commandHandlers))
            {
                // No command handler found
                return;
            }

            commandHandlers.ForEach(commandHandler =>
                commandHandler.Invoke(new ReceivedCommandMessage()
                {
                    Message = new()
                    {
                        CommandName = commandName,
                        Data = data,
                        Separator = separator,
                    },
                    RawMessage = receivedMessage,
                    RemoteEndPoint = result.RemoteEndPoint,
                    Timestamp = timestamp,
                }));
        }

        private async Task ReceiveLoop()
        {
            try
            {
                // listen for incoming packets
                while (!_listeningCancellation.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for incoming UDP data
                        UdpReceiveResult result = await _client.ReceiveAsync(_listeningCancellation.Token);
                        DateTimeOffset timestamp = DateTimeOffset.Now;

                        // Pass on to main thread to avoid blocking
                        // and immediately handle subsequent incoming packets
                        _synchronizationContext.Post((_) => HandleMessage(result, timestamp), null);
                    }
                    catch (SocketException)
                    {
                        // probably some remote port was unreachable
                    }
                }
            }
            catch
            {
                // canceled or error
            }
        }

        /// <summary>
        /// Helper function to check if the message starts with the delimiter \xFF\xFF\xFF\xFF
        /// </summary>
        /// <param name="responseBytes"></param>
        /// <returns></returns>
        private static bool IsDelimiterAtStart(byte[] responseBytes)
        {
            return responseBytes.Length >= 4 &&
                   responseBytes[0] == 0xFF &&
                   responseBytes[1] == 0xFF &&
                   responseBytes[2] == 0xFF &&
                   responseBytes[3] == 0xFF;
        }

        /// <summary>
        /// Registers a command handler callback that is invoked 
        /// when a command message is received from a game server.
        /// </summary>
        /// <param name="command">The command name to match.</param>
        /// <param name="onCommandReceived">The handler callback.</param>
        public IDisposable On(string command, Action<ReceivedCommandMessage> onCommandReceived)
        {
            ref List<Action<ReceivedCommandMessage>>? commandHandlers =
                ref CollectionsMarshal.GetValueRefOrAddDefault(_commandHandlers, command.ToLower(), out _);
            commandHandlers ??= [];

            commandHandlers.Add(onCommandReceived);

            return new Disposable(() =>
                _commandHandlers.GetValueOrDefault(command.ToLower())?.Remove(onCommandReceived));
        }

        /// <summary>
        /// Sends a command message to a game server.
        /// </summary>
        /// <param name="endPoint">Endpoint of the server.</param>
        /// <returns>Number of sent bytes.</returns>
        public ValueTask<int> SendAsync(IPEndPoint endPoint, CommandMessage commandMessage, CancellationToken cancellationToken = default)
        {
            return SendAsync(endPoint, commandMessage.CommandName, commandMessage.Data, commandMessage.Separator, cancellationToken);
        }

        /// <summary>
        /// Sends a command message to a game server.
        /// </summary>
        /// <param name="endPoint">Endpoint of the server.</param>
        /// <returns>Number of sent bytes.</returns>
        public ValueTask<int> SendAsync(IPEndPoint endPoint, string command, string data = "", char seperator = ' ',
            CancellationToken cancellationToken = default)
        {
            string packet = ""; // "\xFF\xFF\xFF\xFF";

            packet += command;
            packet += seperator;
            packet += data;

            byte[] message = [0xff, 0xff, 0xff, 0xff, .. Encoding.ASCII.GetBytes(packet)];
            return _client.Client.SendToAsync(message.AsMemory(), endPoint, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            // stop receiving packets
            await _listeningCancellation.CancelAsync();
            await _listeningTask;

            _client.Dispose();
        }

        private sealed class Disposable(Action onDispose) : IDisposable
        {
            private volatile Action? _onDispose = onDispose;

            public void Dispose()
            {
                var dispose = Interlocked.Exchange(ref _onDispose, null);
                dispose?.Invoke();
            }
        }
    }
}