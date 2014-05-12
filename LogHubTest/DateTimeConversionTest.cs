// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using LogHub;
using NUnit.Framework;

namespace LogHubTest
{
	[TestFixture]
	public class DateTimeConversionTest
	{
		[Test]
		public void Conversion()
		{
			var dateTime = DateTime.Now;
			var baseTime = dateTime.Date;
			var ts = LogHubClient.DateTimeToTs(dateTime);
			var dateTimeConverted = LogHubClient.DateTimeFromTs(ts);

			Assert.AreEqual(dateTime.Kind, dateTimeConverted.Kind);
			Assert.AreEqual
			(
				Math.Floor((dateTime - baseTime).TotalMilliseconds), 
				Math.Floor((dateTimeConverted - baseTime).TotalMilliseconds)
			);
		}

		[Test]
		public void Base()
		{
			var ts = LogHubClient.DateTimeToTs(LogHubClient.UnixEpoch);

			Assert.AreEqual(0, LogHubClient.DateTimeToTs(LogHubClient.UnixEpoch));
			Assert.AreEqual(LogHubClient.UnixEpoch.ToLocalTime(), LogHubClient.DateTimeFromTs(ts));
		}
	}
}
