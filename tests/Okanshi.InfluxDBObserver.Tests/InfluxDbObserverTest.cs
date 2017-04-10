﻿using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using InfluxDB.WriteOnly;
using NSubstitute;
using Okanshi.Observers;
using Xunit;

namespace Okanshi.InfluxDBObserver.Tests {
    public class InfluxDbObserverTest {
        private readonly IInfluxDbClient influxDbClient = Substitute.For<IInfluxDbClient>();

        [Fact]
        public void Poller_is_not_allowed_to_be_null() {
            Action action = () => new InfluxDbObserver(null, influxDbClient, new InfluxDbObserverOptions("anything"));
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Client_is_not_allowed_to_be_null() {
            Action action = () => new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), null, new InfluxDbObserverOptions("anything"));
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Options_is_not_allowed_to_be_null() {
            Action action = () => new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient, null);
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Points_are_written_to_database_passed_into_options() {
            const string databaseName = "databaseName";
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                new InfluxDbObserverOptions(databaseName));

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0) });

            influxDbClient.Received(1).WriteAsync(Arg.Any<string>(), databaseName, Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Database_selector_is_used_to_select_database() {
            const string expectedDatabase = "expectedDatabase";
            const string incorrectDatabase = "databaseName";
            var options = new InfluxDbObserverOptions(incorrectDatabase) {
                DatabaseSelector = _ => expectedDatabase
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0) });

            influxDbClient.DidNotReceive().WriteAsync(Arg.Any<string>(), incorrectDatabase, Arg.Any<IEnumerable<Point>>());
            influxDbClient.Received(1).WriteAsync(Arg.Any<string>(), expectedDatabase, Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Point_are_by_default_written_to_autogen_retention_policy() {
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                new InfluxDbObserverOptions("databaseName"));

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0) });

            influxDbClient.Received(1).WriteAsync("autogen", Arg.Any<string>(), Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Retention_policy_is_used_to_select_retention_policy() {
            const string expectedRetentionPolicy = "expectedRetentionPolicy";
            var options = new InfluxDbObserverOptions("databaseName") {
                RetentionPolicySelector = (metric, database) => expectedRetentionPolicy,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0) });

            influxDbClient.DidNotReceive().WriteAsync("autogen", Arg.Any<string>(), Arg.Any<IEnumerable<Point>>());
            influxDbClient.Received(1).WriteAsync(expectedRetentionPolicy, Arg.Any<string>(), Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Tags_can_be_converted_to_fields_as_defined_by_options() {
            const string tagName = "tag";
            const string tagValue = "100";
            var options = new InfluxDbObserverOptions("databaseName") {
                TagToFieldSelector = tag => tag.Key == tagName,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, tagValue),  }, 0) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(point => point.Tags.All(tag => tag.Key != tagName) &&
                                                                             point.Fields.Any(
                                                                                 field => field.Key == tagName && (float)field.Value ==
                                                                                          Convert.ToSingle(tagValue)))));
        }

        [Fact]
        public void Tags_can_be_ignored_as_defined_by_options() {
            const string tagName = "tag";
            var options = new InfluxDbObserverOptions("databaseName") {
                TagsToIgnore = new List<string> { tagName }
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, "100"),  }, 0) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(point => point.Tags.All(tag => tag.Key != tagName))));
        }
    }
}
