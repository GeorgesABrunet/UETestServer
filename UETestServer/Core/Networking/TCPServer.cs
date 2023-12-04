using Core.Logging;
using Core.Networking.Sockets;
using System;
using System.Net;

namespace Core.Networking
{
    public class TCPServer
    {
        #region Member Variables
        
        // socket connection
        private SocketServer _socketServer;
        
        // flag to detirmine if the server is running
        public bool IsRunning { get; private set; }

        public event SocketBase.ConnectionHandler OnClientConnect;
        public event SocketBase.ConnectionHandler OnClientDisconnect;
        public event SocketBase.DataReceivedHandler OnDataReceived;

        #endregion

        #region Public Methods

        public void Start(string endpoint, bool withHeaders)
        {
            try
            {                
                Logger.Info("[SocketServer] Starting server.");
                
                InitializeSocketServer(endpoint, withHeaders);

                IsRunning = true;               
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketServer] Startup error: " + ex);
            }
        }

        
        /// <summary>
        /// Start the socket server for communicating with agents on remote machines
        /// </summary>
        private void InitializeSocketServer(string endpoint, bool withHeaders)
        {
            // get the server address
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Logger.Error("[SocketServer] Endpoint is invalid, socket server can't be started.");
                return;
            }

            string[] endpointParts = endpoint.Split(':');

            int endpointPort;

            if (endpointParts.Length != 2 || false == int.TryParse(endpointParts[1], out endpointPort))
            {
                Logger.Error("[SocketClient] Endpoint is invalid, socket client can't be started.");
                return;
            }


            IPAddress endpointIP;
            
            if (endpointParts[0].Trim().Equals("localhost", StringComparison.InvariantCultureIgnoreCase))
            {
                endpointIP = IPAddress.Loopback;
            }
            else if (false == IPAddress.TryParse(endpointParts[0], out endpointIP))
            {
                endpointIP = Utilities.GetLocalIPAddress();
            }

            if (null == endpointIP)
            {
                Logger.Error("[SocketServer] Endpoint IP address could not be found, socket server can't be started.");
                return;
            }

            try
            {
                _socketServer = new SocketServer(endpointIP.ToString(), endpointPort, withHeaders);
                _socketServer.OnClientConnect += _socketServer_OnClientConnect;
                _socketServer.OnDisconnect += _socketServer_OnDisconnect;
                _socketServer.OnDataReceived += _socketServer_OnDataReceived;

                _socketServer.Start();
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketServer] Error starting server: " + ex.Message);
            }
        }

        private void _socketServer_OnClientConnect(object sender, RemoteConnection connection)
        {
            if (null != OnClientConnect)
            {
                OnClientConnect(this, connection);
            }
        }

        private void _socketServer_OnDisconnect(object sender, RemoteConnection connection)
        {
            if (null != OnClientDisconnect)
            {
                OnClientDisconnect(this, connection);
            }
        }

        private void _socketServer_OnDataReceived(object sender, Package packet)
        {
            try
            {
                if (null != OnDataReceived)
                {
                    OnDataReceived(this, packet);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketServer] Error processing request from client: {0}", ex);
            }

        }

        /// <summary>
        /// Stop the server, close connection, kill any running processes
        /// </summary>
        public void Stop()
        {
            if (false == IsRunning)
                return;

            Logger.Info("[SocketServer] Stopping...");


            if (null != _socketServer)
                _socketServer.Stop();

            IsRunning = false;
        }
        
        public void Send(Guid client, string data)
        {
            if (false == IsRunning || null == _socketServer)
                return;

            _socketServer.Send(client, data);
        }

        public void Broadcast(string data)
        {
            if (false == IsRunning || null == _socketServer)
                return;

            string LogData = data;
            if (LogData.Length > 500)
            {
                LogData = LogData.Substring(0, 500) + "... (truncated)";
            }
            Logger.Info("[SENDING] {0}", LogData);

            _socketServer.Broadcast(data);
        }

        public void Broadcast(byte[] data)
        {
            if (false == IsRunning || null == _socketServer)
                return;

            Logger.Info("[SENDING] Binary data: {0}", data.Length);

            _socketServer.Broadcast(data);
        }
        #endregion
    }
}
