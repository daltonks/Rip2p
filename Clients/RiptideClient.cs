﻿using System;
using System.Threading.Tasks;
using Riptide;
using Riptide.Transports.Udp;

namespace Rip2p.Clients
{
    public class RiptideClient : BaseClient
    {
        private TaskCompletionSource<bool> _connectCompletionSource;

        private Client _client;

        private void Awake()
        {
            // TODO: Change UdpClient to steam client:
            // TODO: https://github.com/RiptideNetworking/SteamTransport
            
            var transport = new UdpClient();
            _client = new Client(transport);
            
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
        }

        protected override Task<bool> ConnectInternalAsync(string address, ushort port)
        {
            if (!_client.Connect($"{address}:{port}"))
            {
                return Task.FromResult(false);
            }
            
            _connectCompletionSource = new TaskCompletionSource<bool>();
            return _connectCompletionSource.Task;
        }
        
        private void OnConnected(object sender, EventArgs e)
        {
            _connectCompletionSource.TrySetResult(true);
        }
        
        private void OnDisconnected(object sender, DisconnectedEventArgs e)
        {
            OnDisconnected();
            
            _connectCompletionSource.TrySetResult(false);
        }
        
        public override void Disconnect()
        {
            _client.Disconnect();
            
            _client.Connected -= OnConnected;
            _client.Disconnected -= OnDisconnected;
        }
        
        private void FixedUpdate()
        {
            _client.Update();
        }
    }
}