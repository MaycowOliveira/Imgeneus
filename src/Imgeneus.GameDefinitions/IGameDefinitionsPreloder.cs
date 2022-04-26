﻿using Parsec.Shaiya.NpcQuest;

namespace Imgeneus.GameDefinitions
{
    public interface IGameDefinitionsPreloder
    {
        /// <summary>
        /// Preloads all needed game definitions from SData files.
        /// </summary>
        void Preload();

        /// <summary>
        /// Preloaded NPCs.
        /// </summary>
        Dictionary<(byte Type, short TypeId), BaseNpc> NPCs { get; }

        /// <summary>
        /// Preloaded quests.
        /// </summary>
        Dictionary<short, Quest> Quests { get; }
    }
}