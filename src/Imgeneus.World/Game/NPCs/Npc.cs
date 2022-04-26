﻿using Imgeneus.Database.Entities;
using Imgeneus.World.Game.Zone;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Imgeneus.World.Game.NPCs
{
    public class Npc : IMapMember, IDisposable
    {
        private readonly ILogger _logger;
        private readonly DbNpc _dbNpc;

        public Npc(List<(float X, float Y, float Z, ushort Angle)> moveCoordinates, Map map, ILogger<Npc> logger, DbNpc dbNpc): this(logger, dbNpc)
        {
            _logger = logger;
            _dbNpc = dbNpc;
            PosX = moveCoordinates[0].X;
            PosY = moveCoordinates[0].Y;
            PosZ = moveCoordinates[0].Z;
            Angle = moveCoordinates[0].Angle;
            Map = map;
        }

        public Npc(ILogger<Npc> logger, DbNpc dbNpc)
        {
            // Set products.
            var dbProductsString = dbNpc.Products;
            if (!string.IsNullOrWhiteSpace(dbProductsString))
            {
                var items = dbProductsString.Split(" | ");
                foreach (var item in items)
                {
                    try
                    {
                        var itemTypeAndId = item.Split(".");
                        byte itemType = byte.Parse(itemTypeAndId[0]);
                        byte itemTypeId = byte.Parse(itemTypeAndId[1]);
                        _products.Add(new NpcProduct(itemType, itemTypeId));
                    }
                    catch
                    {
                        _logger.LogError($"Couldn't parse npc item definition, plase check this npc: {_dbNpc.Id}.");
                    }
                }
            }

            // Set quests.
            var dbStartQuestsString = dbNpc.QuestStart;
            if (!string.IsNullOrWhiteSpace(dbStartQuestsString))
            {
                var startQuests = dbStartQuestsString.Split(" | ");
                foreach (var quest in startQuests)
                {
                    try
                    {
                        _startQuestIds.Add(short.Parse(quest));
                    }
                    catch
                    {
                        _logger.LogError($"Couldn't parse npc start quest {quest}, plase check this npc: {_dbNpc.Id}.");
                    }
                }
            }

            var dbEndQuestsString = dbNpc.QuestEnd;
            if (!string.IsNullOrWhiteSpace(dbEndQuestsString))
            {
                var endQuests = dbNpc.QuestEnd.Split(" | ");
                foreach (var quest in endQuests)
                {
                    try
                    {
                        _endQuestIds.Add(short.Parse(quest));
                    }
                    catch
                    {
                        _logger.LogError($"Couldn't parse npc end quest {quest}, plase check this npc: {_dbNpc.Id}.");
                    }
                }
            }

            // Set teleport gates.
            var dbMapsString = dbNpc.Maps;
            if (!string.IsNullOrWhiteSpace(dbMapsString))
            {
                var gates = dbMapsString.Split(" | ");
                foreach (var gateStr in gates)
                {
                    try
                    {
                        var gateDefs = gateStr.Split(",");
                        if (string.IsNullOrEmpty(gateDefs[4])) // Empty gate with 0-values.
                        {
                            continue;
                        }

                        var gate = new NpcGate()
                        {
                            MapId = ushort.Parse(gateDefs[0]),
                            X = float.Parse(gateDefs[1]),
                            Y = float.Parse(gateDefs[2]),
                            Z = float.Parse(gateDefs[3]),
                            Name = gateDefs[4],
                            Cost = int.Parse(gateDefs[5])
                        };
                        _gates.Add(gate);
                    }
                    catch
                    {
                        _logger.LogError($"Couldn't parse npc gate definition, plase check this npc: {_dbNpc.Id}.");
                    }
                }
            }
        }

        public int Id { get; set; }

        /// <inheritdoc />
        public float PosX { get; set; }

        /// <inheritdoc />
        public float PosY { get; set; }

        /// <inheritdoc />
        public float PosZ { get; set; }

        /// <inheritdoc />
        public ushort Angle { get; set; }

        public Map Map { get; private set; }

        public int CellId { get; set; }

        public int OldCellId { get; set; }

        /// <summary>
        /// Type of NPC.
        /// </summary>
        public byte Type { get => _dbNpc.Type; }

        /// <summary>
        /// Type id of NPC.
        /// </summary>
        public ushort TypeId { get => _dbNpc.TypeId; }

        #region Products

        private readonly IList<NpcProduct> _products = new List<NpcProduct>();
        private IList<NpcProduct> _readonlyProducts;

        /// <summary>
        /// Items, that npc sells.
        /// </summary>
        public IList<NpcProduct> Products
        {
            get
            {
                if (_readonlyProducts is null)
                    _readonlyProducts = new ReadOnlyCollection<NpcProduct>(_products);
                return _readonlyProducts;
            }
        }

        /// <summary>
        /// Checks if Product list contains product at index. Logs warning, if product is not found.
        /// </summary>
        /// <param name="index">index, that we want to check.</param>
        /// <returns>return true, if there is some product at index</returns>
        public bool ContainsProduct(byte index)
        {
            if (Products.Count <= index)
            {
                _logger.LogWarning($"NPC {_dbNpc.Id} doesn't contain product at index {index}. Check it out.");
                return false;
            }

            return true;
        }

        #endregion

        #region Start quests

        private readonly IList<short> _startQuestIds = new List<short>();
        private IList<short> _readonlyStartQuestIds;

        /// <summary>
        /// Collection of quests, that player can start at this npc.
        /// </summary>
        public IList<short> StartQuestIds
        {
            get
            {
                if (_readonlyStartQuestIds is null)
                    _readonlyStartQuestIds = new ReadOnlyCollection<short>(_startQuestIds);

                return _readonlyStartQuestIds;
            }
        }

        #endregion

        #region End quests

        private readonly IList<short> _endQuestIds = new List<short>();
        private IList<short> _readonlyEndQuestIds;

        /// <summary>
        /// Collection of quests, that player can start at this npc.
        /// </summary>
        public IList<short> EndQuestIds
        {
            get
            {
                if (_readonlyEndQuestIds is null)
                    _readonlyEndQuestIds = new ReadOnlyCollection<short>(_endQuestIds);

                return _readonlyEndQuestIds;
            }
        }

        #endregion

        #region Gates

        private readonly IList<NpcGate> _gates = new List<NpcGate>();
        private IList<NpcGate> _readonlyGates;

        /// <summary>
        /// Gates, where npc can teleport.
        /// </summary>
        public IList<NpcGate> Gates
        {
            get
            {
                if (_readonlyGates is null)
                    _readonlyGates = new ReadOnlyCollection<NpcGate>(_gates);
                return _readonlyGates;
            }
        }

        /// <summary>
        /// Checks if Gate list contains gate at index. Logs warning, if gate is not found.
        /// </summary>
        /// <param name="index">index, that we want to check.</param>
        /// <returns>return true, if there is some gate at index</returns>
        public bool ContainsGate(byte index)
        {
            if (Gates.Count <= index)
            {
                _logger.LogWarning("NPC {id} doesn't contain gate at index {index}. Check it out.", _dbNpc.Id, index);
                return false;
            }

            return true;
        }

        #endregion

        #region Dispose

        private bool _isDisposed = false;

        public void Dispose()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(Npc));

            _isDisposed = true;

            _products.Clear();
            _startQuestIds.Clear();
            _endQuestIds.Clear();

            Map = null;
        }

        #endregion
    }
}
