﻿using Imgeneus.Network.Data;
using Imgeneus.Network.Packets.Game;
using Imgeneus.Network.Server;
using Imgeneus.Network.Server.Crypto;
using System;

namespace Imgeneus.World
{
    public interface IWorldClient
    {
        /// <summary>
        /// Gets the client's logged user id.
        /// </summary>
        int UserID { get; }

        /// <summary>
        /// Gets the client's logged char id.
        /// </summary>
        int CharID { get; }

        /// <summary>
        /// Crypto manager is responsible for the whole cryptography.
        /// </summary>
        CryptoManager CryptoManager { get; }

        void SendPacket(Packet packet);
    }
}
