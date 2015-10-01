﻿using N2.Engine;
using N2.Persistence;
using N2.Plugin.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace N2.Management.Statistics
{
	[Service]
	public class BucketRepository
	{
		private Persistence.IRepository<Bucket> buckets;
		private Persistence.IRepository<Statistic> statistics;

		public BucketRepository(N2.Persistence.IRepository<Bucket> buckets, N2.Persistence.IRepository<Statistic> statistics)
		{
			this.buckets = buckets;
			this.statistics = statistics;
		}

		public virtual void Save(IEnumerable<Bucket> buckets)
		{
			bool any = false;
			foreach (var bucket in buckets)
			{
				this.buckets.SaveOrUpdate(bucket);
				any = true;
			}
			if (any)
				this.buckets.Flush();
		}

		public virtual void Transfer(DateTime uptil, TimeUnit interval)
		{
			var slot = uptil.GetSlot(interval);
			var collectedBuckets = buckets.Find().Where(b => b.TimeSlot < slot).ToArray();
			if (collectedBuckets.Length == 0)
				return;

			var start = collectedBuckets[0].TimeSlot;
			var pageViews = collectedBuckets
				.GroupBy(b => new KeyValuePair<DateTime, int>(b.TimeSlot.GetSlot(interval), b.PageID), b => b.Views)
				.ToDictionary(b => b.Key, b => b.Sum());

			var existingStatistics = statistics.Find(Parameter.GreaterOrEqual("TimeSlot", start) & Parameter.LessThan("TimeSlot", uptil)).ToList();
			foreach (var s in existingStatistics)
			{
				var key = new KeyValuePair<DateTime, int>(s.TimeSlot.GetSlot(interval), s.PageID);
				if (!pageViews.ContainsKey(key))
					continue;
				s.Views += pageViews[key];
				pageViews.Remove(key);
			}
			statistics.SaveOrUpdate(existingStatistics);
			statistics.Flush();
			buckets.Delete(collectedBuckets);
			buckets.Flush();

			var addedStatistics = new List<Statistic>(pageViews.Count);
			foreach (var pageView in pageViews)
			{
				var s = new Statistic { TimeSlot = pageView.Key.Key, PageID = pageView.Key.Value, Views = pageView.Value };
				addedStatistics.Add(s);
			}
			statistics.SaveOrUpdate(addedStatistics);
			statistics.Flush();
		}
	}
}
