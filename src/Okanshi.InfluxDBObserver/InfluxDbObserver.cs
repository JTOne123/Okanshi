﻿using System;
using System.Collections.Generic;
using System.Linq;
using InfluxDB.WriteOnly;

namespace Okanshi.Observers
{
    /// <summary>
    /// Observer for posting metrics to InfluxDB.
    /// </summary>
    public class InfluxDbObserver : IMetricObserver
    {
        private readonly IMetricPoller poller;
        private readonly IInfluxDbClient client;
        private readonly InfluxDbObserverOptions options;

        /// <summary>
        /// Creates a new instance of the observer.
        /// </summary>
        /// <param name="poller">The poller.</param>
        /// <param name="client">The InfluxDB client.</param>
        /// <param name="options">The observer options.</param>
        public InfluxDbObserver(IMetricPoller poller, IInfluxDbClient client, InfluxDbObserverOptions options)
        {
            if (poller == null)
            {
                throw new ArgumentNullException(nameof(poller));
            }

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.poller = poller;
            this.client = client;
            this.options = options;
            poller.MetricsPolled += OnMetricsPolled;
        }

        private void OnMetricsPolled(object sender, MetricEventArgs args)
        {
            Update(args.Metrics);
        }

        public void Dispose()
        {
            poller.MetricsPolled -= OnMetricsPolled;
        }

        /// <summary>
        /// Method used to write metrics to InfluxDB. This method are not meant to be used externally.
        /// </summary>
        /// <param name="metrics"></param>
        public void Update(Metric[] metrics)
        {
            var groupedMetrics = metrics.GroupBy(options.DatabaseSelector);
            foreach (var metricGroup in groupedMetrics)
            {
                var groupedByRetention = metricGroup.GroupBy(x => options.RetentionPolicySelector(x, metricGroup.Key));
                foreach (var retentionGroup in groupedByRetention)
                {
                    var points = ConvertToPoints(retentionGroup);
                    client.WriteAsync(retentionGroup.Key, metricGroup.Key, points);
                }
            }
        }

        private IEnumerable<Point> ConvertToPoints(IEnumerable<Metric> metrics)
        {
            foreach (var metric in metrics)
            {
                var metricTags = FilterTags(metric.Tags).ToArray();
                var tags = metricTags.Where(x => !options.TagToFieldSelector(x)).Select(t => new InfluxDB.WriteOnly.Tag(t.Key, t.Value));
                var fields = Enumerable.Repeat(ConvertToField("value", metric.Value), 1)
                    .Concat(metricTags.Where(options.TagToFieldSelector).Select(ConvertTagToField))
                    .Concat(ConvertSubMetricsToFields(metric.SubMetrics));
                yield return new Point {
                    Measurement = options.MeasurementNameSelector(metric),
                    Timestamp = metric.Timestamp.DateTime,
                    Fields = fields,
                    Tags = tags
                };
            }
        }

        private IEnumerable<Field> ConvertSubMetricsToFields(IEnumerable<Metric> subMetrics)
        {
            return subMetrics
                .Select(metric => new { metric, statTag = metric.Tags.FirstOrDefault(x => x.Key.Equals("statistic", StringComparison.OrdinalIgnoreCase)) })
                .Where(t => t.statTag != null)
                .Select(t => ConvertToField(t.statTag.Value, t.metric.Value));
        }

        private IEnumerable<Tag> FilterTags(IEnumerable<Tag> tags)
        {
            return tags.Where(x => !options.TagsToIgnore.Contains(x.Key) && !x.Key.Equals("dataSource", StringComparison.OrdinalIgnoreCase) &&
                                   !x.Key.Equals("statistic", StringComparison.OrdinalIgnoreCase));
        }

        private Field ConvertTagToField(Tag tag)
        {
            int i;
            if (int.TryParse(tag.Value, out i))
            {
                return ConvertToField(tag.Key, i);
            }

            long l;
            if (long.TryParse(tag.Value, out l))
            {
                return ConvertToField(tag.Key, l);
            }

            float f;
            if (float.TryParse(tag.Value, out f))
            {
                return ConvertToField(tag.Key, f);
            }

            bool b;
            if (bool.TryParse(tag.Value, out b))
            {
                return ConvertToField(tag.Key, b);
            }

            return ConvertToField(tag.Key, tag.Value);
        }

        private Field ConvertToField(string key, object value)
        {
            var convertedValue = options.ConvertFieldType(value);
            if (convertedValue is int)
            {
                return new Field(key, (int)convertedValue);
            }

            if (convertedValue is long) {
                return new Field(key, (long)convertedValue);
            }

            if (convertedValue is float)
            {
                return new Field(key, (float)convertedValue);
            }

            if (convertedValue is double)
            {
                return new Field(key, Convert.ToSingle(convertedValue));
            }

            if (convertedValue is bool)
            {
                return new Field(key, (bool)convertedValue);
            }

            return new Field(key, convertedValue.ToString());
        }

        /// <summary>
        /// Get observations. This is not supported in this observer.
        /// </summary>
        public Metric[][] GetObservations()
        {
            throw new NotSupportedException("This observer doesn't support getting observations");
        }
    }
}