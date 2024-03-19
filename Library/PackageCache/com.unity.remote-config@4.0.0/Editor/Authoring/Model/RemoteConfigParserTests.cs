using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;

namespace Unity.Services.RemoteConfig.Authoring.Editor.Model
{
    class RemoteConfigParserTests
    {
        [TestCase((long)1, ConfigType.FLOAT, typeof(double))]
        [TestCase((long)1, ConfigType.INT, typeof(int))]
        public void CastToCorrectType(object value, ConfigType configType, Type resultType)
        {
            var kvp = new KeyValuePair<string, object>("key", value);
            var result = RemoteConfigParser.CastToCorrectType(kvp, configType);
            Assert.AreEqual(resultType, result.GetType());
        }
    }
}
