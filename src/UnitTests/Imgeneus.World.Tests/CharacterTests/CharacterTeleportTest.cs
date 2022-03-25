﻿using Imgeneus.World.Game.Zone;
using Imgeneus.World.Game.Zone.MapConfig;
using System.ComponentModel;
using Xunit;

namespace Imgeneus.World.Tests.CharacterTests
{
    public class CharacterTeleportTest : BaseTest
    {
        [Fact]
        [Description("It should be possible to teleport inside one map.")]
        public void Character_Teleport()
        {
            var character = CreateCharacter();
            testMap.LoadPlayer(character);
            Assert.Equal(0, character.PosX);
            Assert.Equal(0, character.PosY);
            Assert.Equal(0, character.PosZ);

            character.TeleportationManager.Teleport(Map.TEST_MAP_ID, 10, 20, 30);
            Assert.Equal(Map.TEST_MAP_ID, character.MapProvider.NextMapId);
            Assert.Equal(10, character.PosX);
            Assert.Equal(20, character.PosY);
            Assert.Equal(30, character.PosZ);
        }

        [Fact]
        [Description("It should be possible to teleport to another map.")]
        public void Character_TeleportAnotherMap()
        {
            var map1 = new Map(
                    1,
                    new MapDefinition(),
                    new MapConfiguration() { Size = 100, CellSize = 100 },
                    mapLoggerMock.Object,
                    packetFactoryMock.Object,
                    databasePreloader.Object,
                    mobFactoryMock.Object,
                    npcFactoryMock.Object,
                    obeliskFactoryMock.Object,
                    timeMock.Object);
            var map2 = new Map(
                    2,
                    new MapDefinition(),
                    new MapConfiguration() { Size = 100, CellSize = 100 },
                    mapLoggerMock.Object,
                    packetFactoryMock.Object,
                    databasePreloader.Object,
                    mobFactoryMock.Object,
                    npcFactoryMock.Object,
                    obeliskFactoryMock.Object,
                    timeMock.Object);

            var character = CreateCharacter();
            map1.LoadPlayer(character);
            Assert.NotNull(map1.GetPlayer(character.Id));

            character.TeleportationManager.Teleport(map2.Id, 10, 20, 30);
            Assert.Null(map1.GetPlayer(character.Id));
        }
    }
}
