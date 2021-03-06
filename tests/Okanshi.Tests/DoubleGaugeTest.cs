using System.Linq;
using FluentAssertions;
using Xunit;

namespace Okanshi.Test
{
    public class DoubleGaugeTest
    {
        [Fact]
        public void Initial_value_is_zero()
        {
            var gauge = new DoubleGauge(MonitorConfig.Build("Test"));

            gauge.GetValues().First().Value.Should().Be(0);
        }

        [Theory]
        [InlineData(1.67)]
        [InlineData(500.0)]
        [InlineData(10.672)]
        public void Updating_the_value_updates_the_value_correctly(double expectedValue)
        {
            var gauge = new DoubleGauge(MonitorConfig.Build("Test"));

            gauge.Set(expectedValue);

            gauge.GetValues().First().Value.Should().Be(expectedValue);
        }

        [Fact]
        public void Reset_sets_the_value_to_zero()
        {
            var gauge = new DoubleGauge(MonitorConfig.Build("Test"));
            gauge.Set(100.0);

            gauge.Reset();

            gauge.GetValues().First().Value.Should().Be(0.0);
        }

        [Fact]
        public void Get_and_reset_sets_the_value_to_zero()
        {
            var gauge = new DoubleGauge(MonitorConfig.Build("Test"));
            gauge.Set(100L);

            gauge.GetValuesAndReset();

            gauge.GetValues().First().Value.Should().Be(0L);
        }

        [Fact]
        public void Get_and_reset_gets_the_maximum_value()
        {
            const long expected = 100L;
            var gauge = new DoubleGauge(MonitorConfig.Build("Test"));
            gauge.Set(expected);

            gauge.GetValuesAndReset().First().Value.Should().Be(expected);
        }

        [Fact]
        public void Value_is_called_value()
        {
            var gauge = new DoubleGauge(MonitorConfig.Build("Test"));
            gauge.GetValues().Single().Name.Should().Be("value");
        }
    }
}