using ModApi.Craft.Program;
using ModApi.Craft.Program.Craft;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


namespace Assets.Scripts.Vizzy.SocketsService
{
    public static class SocketsServiceManager
    {

        public static bool CreateServer(IThreadContext context, int port, int buffer, bool useVzVariableBuffer)
        {
            //Debug.Log($"Creating server on port {port}");
            return ServerManager.StartServer(context, port, buffer, useVzVariableBuffer);
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

        public static ExpressionResult GetNextVzVariableBufferItem(int port)
        {
            var server = ServerManager.GetServer(port);
            return server?.GetNextVzVariableBufferItem();
        }

        public static int GetVzVariableBufferCount(int port)
        {
            var server = ServerManager.GetServer(port);
            return server?.GetVzVariableBufferCount() ?? 0;
        }

        public static void ClearVzVariableBuffer(int port)
        {
            var server = ServerManager.GetServer(port);
            server?.ClearVzVariableBuffer();
        }
        public static void Receive(IThreadContext context, int port, byte[] data, bool useVzVariableBuffer)
        {
            //context.Craft.BroadcastMessage(BroadcastScope.Program, context.Craft.Name, data);
            //Debug.Log($"Message received from port {port}");
            //Debug.Log($"Message content: {System.Text.Encoding.UTF8.GetString(data)}");

            if (context.Craft.ExecutingPart.Activated == true || context.Craft.ExecutingPart.IsDestroyed == false)
            {
                string[] array = Encoding.UTF8.GetString(data).Split(new string[] { "<<" }, StringSplitOptions.None);
                var list = new List<ExpressionListItem>();
                foreach (string text in array)
                {
                    list.Add(text);
                }

                var expressionResult = new ExpressionResult(list);

                // 如果启用了VzVariableBuffer，将结果添加到对应端口的缓冲区
                if (useVzVariableBuffer)
                {
                    var server = ServerManager.GetServer(port);
                    if (server != null)
                    {
                        server.AddToVzVariableBuffer(expressionResult);
                    }
                }
                else
                {
                    context.Craft.BroadcastMessage(BroadcastScope.Program, port.ToString(), expressionResult);
                }

                // SendData(port, data);
                //context.GetOrCreateGlobalVariable("Socket_Received_Data").Value.Set(new ExpressionResult(list));


            }
            else
            {
                Debug.Log("Part is not active or destroyed");
                CloseServer(port);
            }

        }

    }


    public class SocketServer
    {
        public event Action<IThreadContext, int, bool, byte[]> OnMessageReceived;
        private readonly int _port;
        private IThreadContext _Context;
        private TcpListener _listener;
        private int _buffer;
        private bool _useVzVariableBuffer;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Queue<ExpressionResult> VzVariableBuffer = new Queue<ExpressionResult>();
        private readonly object _bufferLock = new object();

        public void UpdateCraft(IThreadContext newContext)
        {
            _Context = newContext;
        }

        public void UpdateBuffer(int Buffer)
        {
            _buffer = Buffer;
        }

        public void UpdateUseVzVariableBuffer(bool useVzVariableBuffer)
        {
            _useVzVariableBuffer = useVzVariableBuffer;
        }

        public void AddToVzVariableBuffer(ExpressionResult expressionResult)
        {
            lock (_bufferLock)
            {
                // 如果队列已满（20个元素），抛弃新的入队数据
                if (VzVariableBuffer.Count >= 20)
                {
                    Debug.Log($"VzVariableBuffer is full (20 items) on port {_port}, discarding new data");
                    return; // 满队列时抛弃入队的数据
                }
                
                VzVariableBuffer.Enqueue(expressionResult);
                Debug.Log($"Added item to VzVariableBuffer on port {_port}, current count: {VzVariableBuffer.Count}");
            }
        }

        public ExpressionResult GetNextVzVariableBufferItem()
        {
            lock (_bufferLock)
            {
                if (VzVariableBuffer.Count > 0)
                {
                    return VzVariableBuffer.Dequeue(); // 先入先出：读取即为取出
                }
                return null;
            }
        }

        public int GetVzVariableBufferCount()
        {
            lock (_bufferLock)
            {
                return VzVariableBuffer.Count;
            }
        }

        public void ClearVzVariableBuffer()
        {
            lock (_bufferLock)
            {
                VzVariableBuffer.Clear();
            }
        }

        public SocketServer(IThreadContext context, int port, bool useVzVariableBuffer)
        {
            _Context = context;
            _port = port;
            _useVzVariableBuffer = useVzVariableBuffer;
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
                client.NoDelay = true;
                _clients.Add(client);
                Debug.Log($"Client connected on port {_port}");
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        //private async Task HandleClientAsync(TcpClient client)
        //{
        //    var stream = client.GetStream();
        //    var lengthBuffer = new byte[4];
        //    var buffer = new byte[1024];

        //    try
        //    {
        //        while (!_cts.IsCancellationRequested)
        //        {
        //            // Read message length prefix
        //            await ReadFullAsync(stream, lengthBuffer, 0, 4);
        //            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

        //            // Reallocate buffer if necessary
        //            if (messageLength > buffer.Length)
        //            {
        //                buffer = new byte[messageLength];
        //            }

        //            // Read message content
        //            await ReadFullAsync(stream, buffer, 0, messageLength);
        //            var data = new byte[messageLength];
        //            Buffer.BlockCopy(buffer, 0, data, 0, messageLength);

        //            // Trigger event, passing ICraftService, port, and data
        //            OnMessageReceived?.Invoke(_Craft, _port, data);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.LogError($"Error handling client: {ex.Message}");
        //        _clients.Remove(client);
        //        client.Dispose();
        //    }
        //}


        private async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = new byte[_buffer];

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    // int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;
                    var data = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
                    OnMessageReceived?.Invoke(_Context, _port, _useVzVariableBuffer, data);

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
            //Sending = true;
            var lengthPrefix = BitConverter.GetBytes(data.Length);

            foreach (var client in _clients.ToArray())
            {
                try
                {
                    var stream = client.GetStream();
                    // Send length prefix first
                    //await stream.WriteAsync(lengthPrefix, 0, 4);
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
            //Sending = false;
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
        internal static readonly ConcurrentDictionary<int, SocketServer> _servers =
            new ConcurrentDictionary<int, SocketServer>();

        internal static SocketServer GetServer(int port)
        {
            _servers.TryGetValue(port, out var server);
            return server;
        }

        public static bool StartServer(IThreadContext context, int port, int buffer, bool useVzVariableBuffer)
        {
            if (_servers.ContainsKey(port))
            {
                Debug.Log($"Server already exists on port {port}, updating its context");
                var existingServer = _servers[port];
                existingServer.UpdateBuffer(buffer);
                existingServer.UpdateCraft(context);
                existingServer.UpdateUseVzVariableBuffer(useVzVariableBuffer);
                return true;
            }

            var newServer = new SocketServer(context, port, useVzVariableBuffer);
            newServer.OnMessageReceived += Receive;
            newServer.UpdateBuffer(buffer);
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

            //if (server.Sending)
            //{
            //    return false;
            //}
            //else
            //{
            //    _ = server.SendAsync(data);
            //    return true;
            //}

        }

        private static void Receive(IThreadContext context, int port, bool useVzVariableBuffer, byte[] data)
        {

            SocketsServiceManager.Receive(context, port, data, useVzVariableBuffer);

        }
    }
}