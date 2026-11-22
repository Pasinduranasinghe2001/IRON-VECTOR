using System;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;

namespace IronVector
{
    public struct RoomPasswordRequest : IBroadcast
    {
        public string Password;
    }

    public struct RoomPasswordResponse : IBroadcast
    {
        public bool Passed;
        public string Message;
    }

    /// <summary>
    /// FishNet HostAuthenticator supporting open rooms (empty password) or password-locked rooms
    /// across LAN and Internet connections.
    /// </summary>
    public class RoomPasswordAuthenticator : HostAuthenticator
    {
        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        /// <summary>Server checks this; leave empty for open matches.</summary>
        public string ServerPassword = "";

        /// <summary>Client sends this upon connecting.</summary>
        public string ClientPassword = "";

        public event Action<bool, string> OnLocalAuthFeedback;

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            SetAllowHostAuthentication(true);
            NetworkManager.ClientManager.OnClientConnectionState += OnClientState;
            NetworkManager.ServerManager.RegisterBroadcast<RoomPasswordRequest>(OnPasswordRequest, false);
            NetworkManager.ClientManager.RegisterBroadcast<RoomPasswordResponse>(OnPasswordResponse);
        }

        void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            if (TryAuthenticateAsClientHost()) return;

            NetworkManager.ClientManager.Broadcast(new RoomPasswordRequest
            {
                Password = ClientPassword ?? ""
            });
        }

        void OnPasswordRequest(NetworkConnection conn, RoomPasswordRequest req, Channel channel)
        {
            if (conn.IsAuthenticated)
            {
                conn.Disconnect(true);
                return;
            }

            bool ok = string.IsNullOrEmpty(ServerPassword) || (req.Password ?? "") == ServerPassword;
            string msg = ok ? "Joined match." : "Wrong room password!";
            NetworkManager.ServerManager.Broadcast(conn, new RoomPasswordResponse { Passed = ok, Message = msg }, false);
            OnAuthenticationResult?.Invoke(conn, ok);
        }

        void OnPasswordResponse(RoomPasswordResponse resp, Channel channel)
        {
            OnLocalAuthFeedback?.Invoke(resp.Passed, resp.Message);
        }

        protected override void OnHostAuthenticationResult(NetworkConnection conn, bool authenticated)
        {
            NetworkManager.ServerManager.Broadcast(conn, new RoomPasswordResponse
            {
                Passed = authenticated,
                Message = authenticated ? "Host ready." : "Host auth failed."
            }, false);
            OnAuthenticationResult?.Invoke(conn, authenticated);
        }
    }
}
