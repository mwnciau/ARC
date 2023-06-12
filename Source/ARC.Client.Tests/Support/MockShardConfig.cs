using ACE.Database.Models.Shard;
using ACE.Database;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARC.Client.Tests.Support;
internal class MockShardConfig
{
    private Mock<ShardConfigDatabase>  shardConfigMock;
    public MockShardConfig() {
        shardConfigMock = new Mock<ShardConfigDatabase>();
        DatabaseManager.ShardConfig = shardConfigMock.Object;
    }

    public MockShardConfig MockLong(string key, long value)
    {
        var longProperty = new ConfigPropertiesLong();
        longProperty.Value = value;
        shardConfigMock.Setup(x => x.GetLong(key)).Returns(longProperty);

        return this;
    }
    public MockShardConfig MockBool(string key, bool value)
    {
        var boolProperty = new ConfigPropertiesBoolean();
        boolProperty.Value = value;
        shardConfigMock.Setup(x => x.GetBool(key)).Returns(boolProperty);

        return this;
    }
}
