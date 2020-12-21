﻿using Imgeneus.Core.Structures.Configuration;
using Imgeneus.Network.Data;
using Imgeneus.Network.Packets;
using Imgeneus.Network.Packets.Game;
using Imgeneus.Network.Packets.InternalServer;
using Imgeneus.Network.Server;
using Imgeneus.Network.Server.Crypto;
using Imgeneus.World.Game;
using Imgeneus.World.InternalServer;
using Imgeneus.World.SelectionScreen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Imgeneus.World
{
    public sealed class WorldServer : Server<WorldClient>, IWorldServer
    {
        private readonly ILogger<WorldServer> _logger;
        private readonly WorldConfiguration _worldConfiguration;
        private readonly IGameWorld _gameWorld;

        /// <summary>
        /// Gets the Inter-Server client.
        /// </summary>
        public ISClient InterClient { get; private set; }

        public WorldServer(ILogger<WorldServer> logger, IOptions<WorldConfiguration> configuration, IGameWorld gameWorld)
            : base(new ServerConfiguration(configuration.Value.Host, configuration.Value.Port, configuration.Value.MaximumNumberOfConnections))
        {
            _logger = logger;
            _worldConfiguration = configuration.Value;
            _gameWorld = gameWorld;
            InterClient = new ISClient(new InterServerConfiguration());
            InterClient.OnPacketArrived += InterClient_OnPacketArrived;
        }

        protected override void OnStart()
        {
            _logger.LogTrace("Host: {0}, Port: {1}, MaxNumberOfConnections: {2}",
                _worldConfiguration.Host,
                _worldConfiguration.Port,
                _worldConfiguration.MaximumNumberOfConnections);
            InterClient.Connect();
        }

        /// <inheritdoc />
        protected override void OnError(Exception exception)
        {
            _logger.LogInformation($"World Server error: {exception.Message}");
        }

        /// <inheritdoc />
        protected override void OnClientDisconnected(WorldClient client)
        {
            base.OnClientDisconnected(client);

            SelectionScreenManagers.Remove(client.Id, out var manager);
            manager.Dispose();
            client.OnPacketArrived -= Client_OnPacketArrived;

            _gameWorld.RemovePlayer(client.CharID);
        }

        /// <inheritdoc />
        protected override void OnClientConnected(WorldClient client)
        {
            base.OnClientConnected(client);

            SelectionScreenManagers.Add(client.Id, new SelectionScreenManager(client));
            client.OnPacketArrived += Client_OnPacketArrived;
        }

        private async void Client_OnPacketArrived(ServerClient sender, IDeserializedPacket packet)
        {
            if (packet is HandshakePacket)
            {
                var handshake = (HandshakePacket)packet;
                (sender as WorldClient).SetClientUserID(handshake.UserId);

                // As soon as we change id, we should update id in dictionary.
                clients.TryRemove(sender.Id, out var client);
                SelectionScreenManagers.Remove(sender.Id, out var manager);

                // Now give client new id.
                client.Id = handshake.SessionId;

                // Return client back to dictionary.
                clients.TryAdd(client.Id, client);
                SelectionScreenManagers.Add(client.Id, manager);

                // Send request to login server and get client key.
                using var requestPacket = new Packet(PacketType.AES_KEY_REQUEST);
                requestPacket.Write(sender.Id.ToByteArray());
                InterClient.SendPacket(requestPacket);
            }

            if (packet is PingPacket)
            {
                // TODO: implement disconnect, if client is not sending ping packet.
            }

            if (packet is CashPointPacket)
            {
                // TODO: implement cash point packet.
                using var dummyPacket = new Packet(PacketType.CASH_POINT);
                dummyPacket.Write(0);
                sender.SendPacket(dummyPacket);
            }

            if (packet is LogOutPacket)
            {
                // TODO: For sure, here should be timer!
                await Task.Delay(1000 * 10); // 10 seconds * 1000 milliseconds

                if (sender.IsDispose)
                    return;

                using var logoutPacket = new Packet(PacketType.LOGOUT);
                sender.SendPacket(logoutPacket);

                sender.CryptoManager.UseExpandedKey = false;

                SelectionScreenManagers[sender.Id].SendSelectionScrenInformation(((WorldClient)sender).UserID);
            }
        }

        private void InterClient_OnPacketArrived(IDeserializedPacket packet)
        {
            // Packet, that login server sends, when user tries to connect world server.
            if (packet is AesKeyResponsePacket)
            {
                var aesPacket = (AesKeyResponsePacket)packet;

                clients.TryGetValue(aesPacket.Guid, out var worldClient);

                worldClient.CryptoManager.GenerateAES(aesPacket.Key, aesPacket.IV);

                // Maybe I need to refactor this?
                using var sendPacket = new Packet(PacketType.GAME_HANDSHAKE);
                sendPacket.WriteByte(0); // 0 means there was no error.
                sendPacket.WriteByte(2); // no idea what is it, it just works.
                sendPacket.Write(CryptoManager.XorKey);
                worldClient.SendPacket(sendPacket);

                SelectionScreenManagers[worldClient.Id].SendSelectionScrenInformation(worldClient.UserID);
            }
        }

        #region Screen selection

        /// <summary>
        /// Screen selection manager helps with packets, that must be sent right after gameshake.
        /// </summary>
        private readonly Dictionary<Guid, SelectionScreenManager> SelectionScreenManagers = new Dictionary<Guid, SelectionScreenManager>();

        #endregion
    }
}
