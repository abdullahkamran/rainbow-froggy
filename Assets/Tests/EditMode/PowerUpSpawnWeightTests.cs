using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    // AC4: verify that Lotus Bloom has a strictly lower spawn weight than
    // both Time Freeze and the standard-pad reference weight.
    [TestFixture]
    public class PowerUpSpawnWeightTests
    {
        [Test]
        public void LotusBloomWeight_IsStrictlyLessThan_TimeFreezeWeight()
        {
            Assert.Less(PowerUpWeights.LotusBloomWeight, PowerUpWeights.TimeFreezeWeight,
                "LotusBloomWeight must be strictly less than TimeFreezeWeight");
        }

        [Test]
        public void LotusBloomWeight_IsStrictlyLessThan_StandardPadWeight()
        {
            Assert.Less(PowerUpWeights.LotusBloomWeight, PowerUpWeights.StandardPadWeight,
                "LotusBloomWeight must be strictly less than StandardPadWeight");
        }
    }
}
