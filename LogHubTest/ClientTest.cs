// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogHub;
using NChannels;
using NUnit.Framework;

namespace LogHubTest
{
	[TestFixture]
	public class ClientTest
	{
		private LogHubClient MakeClient(bool admin)
		{
			var cli = new LogHubClient
			(
				"localhost", 
				10001, 
				new ClientOptions 
				{
					MaxConnections = 1,
					User = (admin) ? Users.DefaultAdmin : Users.Anonymous,
					Password = (admin) ? Users.DefaultAdmin : string.Empty,
					SkipCertValidation = true,
					UseTls = true
				}
			);

			cli.Error += 
				(sender, args) => Console.WriteLine(args.Exception);

			return cli;
		}

		[Test]
		public void Integrational()
		{
			/*using(var cli = MakeClient(false))
			{
				for (int i = 0; i < 10; i++)
				{
					cli.Write(1, "Test", "Test message " + i);
				}

				Thread.Sleep(3000);

				var entriesChan = cli.Read(DateTime.Now - TimeSpan.FromSeconds(5), DateTime.Now, 0, 10, "Test");
				var entries = new List<LogEntry>();

				entriesChan.ForEach(ent => entries.Add(ent)).Wait();

				Assert.Greater(entries.Count, 0);
			}

			using(var cli = MakeClient(true))
			{
				cli.Truncate(DateTime.Now).Wait();

				Thread.Sleep(3000);

				var entriesChan = cli.Read(DateTime.Now - TimeSpan.FromSeconds(5), DateTime.Now, 0, 10, "Test");
				var entries = new List<LogEntry>();

				entriesChan.ForEach(ent => entries.Add(ent)).Wait();

				Assert.AreEqual(0, entries.Count);

				var statChan = cli.Stat();
				var stat = new List<LogInfo>();

				statChan.ForEach(s => stat.Add(s)).Wait();

				Assert.Greater(stat.Count, 0);
			}*/
		}
	}
}
