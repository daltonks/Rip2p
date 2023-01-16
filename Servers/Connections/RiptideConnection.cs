﻿using Riptide;

namespace Rip2p.Servers.Connections
{
    public class RiptideConnection : BaseConnection
    {
        public override ushort Id => Connection.Id;
        public Connection Connection { get; }
        
        public RiptideConnection(Connection connection)
        {
            Connection = connection;
        }
    }
}