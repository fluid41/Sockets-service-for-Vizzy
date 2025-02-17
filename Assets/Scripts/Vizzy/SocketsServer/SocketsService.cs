using ModApi.Craft.Program;
using ModApi.Craft.Program.Craft;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


namespace Assets.Scripts.Vizzy.SocketsService
{
    public static class SocketsServiceManager
    {

        public static void Broadcast(ICraftService Craft, int port, byte[] data)
        {
            Debug.Log($"Broadcasting message from port {port}");
        }

        public static bool CreateServer(ICraftService Craft, int port)
        {
            //Debug.Log($"Creating server on port {port}");
            return ServerManager.StartServer(Craft, port);
        }

        public static bool CloseServer(int port)
        {
            Debug.Log($"Closing server on port {port}");
            return ServerManager.StopServer(port);
        }
        public static void CloseAllServers()
        {
            Debug.Log("All SocketServer stopped");
            ServerManager.StopAllServers();
        }
        public static bool Send(int port, byte[] data)
        {
            //Debug.Log($"Sending data to client on port {port}");
            return ServerManager.SendData(port, data);
        }
    }


    public class SocketServer
    {
        public event Action<ICraftService, int, byte[]> OnMessageReceived;

        private readonly int _port;
        private ICraftService _Craft;
        private TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public void UpdateCraft(ICraftService newCraft)
        {
            _Craft = newCraft;
        }

        public SocketServer(ICraftService Craft, int port)
        {
            _Craft = Craft;
            _port = port;
            //Debug.Log($"SocketServer created on port {_port}");
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            Debug.Log($"SocketServer started on port {_port}");
            Task.Run(() => AcceptClientsAsync());
        }

        private async Task AcceptClientsAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync();
                client.NoDelay = true; // Disable Nagle algorithm to reduce latency
                _clients.Add(client);
                Debug.Log($"Client connected on port {_port}");
                Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var lengthBuffer = new byte[4];
            var buffer = new byte[1024];

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // Read message length prefix
                    await ReadFullAsync(stream, lengthBuffer, 0, 4);
                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    // Reallocate buffer if necessary
                    if (messageLength > buffer.Length)
                    {
                        buffer = new byte[messageLength];
                    }

                    // Read message content
                    await ReadFullAsync(stream, buffer, 0, messageLength);
                    var data = new byte[messageLength];
                    Buffer.BlockCopy(buffer, 0, data, 0, messageLength);

                    // Trigger event, passing ICraftService, port, and data
                    OnMessageReceived?.Invoke(_Craft, _port, data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling client: {ex.Message}");
                _clients.Remove(client);
                client.Dispose();
            }
        }

        public async Task SendAsync(byte[] data)
        {
            var lengthPrefix = BitConverter.GetBytes(data.Length);

            foreach (var client in _clients.ToArray())
            {
                try
                {
                    var stream = client.GetStream();
                    // Send length prefix first
                    await stream.WriteAsync(lengthPrefix, 0, 4);
                    // Then send data content
                    await stream.WriteAsync(data, 0, data.Length);
                    //Debug.Log($"Message sent to client on port {_port}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error sending message to client: {ex.Message}");
                    _clients.Remove(client);
                }
            }
        }

        private static async Task ReadFullAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = await stream.ReadAsync(buffer, offset, count);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
                count -= read;
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            foreach (var client in _clients)
            {
                client.Dispose();
            }
            _clients.Clear();
            Debug.Log($"SocketServer stopped on port {_port}");
        }




    }

    public static class ServerManager
    {
        private static readonly ConcurrentDictionary<int, SocketServer> _servers =
            new ConcurrentDictionary<int, SocketServer>();

        public static bool StartServer(ICraftService Craft, int port)
        {
            if (_servers.ContainsKey(port))
            {
                Debug.Log($"Server already exists on port {port}, updating its context");
                var existingServer = _servers[port];
                existingServer.UpdateCraft(Craft);
                return true;
            }

            var newServer = new SocketServer(Craft, port);
            newServer.OnMessageReceived += Receive;
            newServer.Start();

            bool result = _servers.TryAdd(port, newServer);
            Debug.Log(result ? $"Server started on port {port}" : $"Failed to start server on port {port}");
            return result;
        }

        public static bool StopServer(int port)
        {
            if (!_servers.TryRemove(port, out var server))
            {
                Debug.LogWarning($"Server not found on port {port}");
                return false;
            }

            server.Stop();
            server.OnMessageReceived -= Receive;
            Debug.Log($"Server stopped on port {port}");
            return true;
        }
        public static void StopAllServers()
        {
            foreach (var port in _servers.Keys)
            {
                if (_servers.TryRemove(port, out var server))
                {
                    server.Stop();
                    server.OnMessageReceived -= Receive;
                }
            }
        }


        public static bool SendData(int port, byte[] data)
        {
            if (!_servers.TryGetValue(port, out var server))
            {
                Debug.LogWarning($"Server not found on port {port}");
                return false;
            }

            _ = server.SendAsync(data);
            return true;
        }

        private static void Receive(ICraftService Craft, int port, byte[] data)
        {
            //Craft.Craft.BroadcastMessage(BroadcastScope.Program, Craft.Name, data);
            //Debug.Log($"Message received from port {port}");
            //Debug.Log($"Message content: {System.Text.Encoding.UTF8.GetString(data)}");

            if (Craft.ExecutingPart.Activated == true || Craft.ExecutingPart.IsDestroyed == false)
            {
                string[] array = System.Text.Encoding.UTF8.GetString(data).Split(new string[] { "<<" }, StringSplitOptions.None);
                List<ExpressionListItem> list = new List<ExpressionListItem>();
                foreach (string text in array)
                {
                    list.Add(text.Trim());
                }
                Craft.BroadcastMessage(BroadcastScope.Program, port.ToString(), new ExpressionResult(list));
            }
            else
            {
                Debug.Log("Part is not active or destroyed");
                SocketsServiceManager.CloseServer(port);
            }

        }
    }
}