// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using LogHub.Protocol;

namespace LogHubTest
{
	[TestFixture]
	public class JStreamTest
	{
		[Test]
		public void WriteRead()
		{
			var stream = new MemoryStream();
			var parser = new JStreamParser(stream);

			parser.Write
			(
				new MessageHeader 
				{
					Action = Actions.Read,
					Pass = "secret",
					Usr = "username"
				}
			).Wait();

			parser.Write
			(
				new IncomingLogEntry
				{
					Sev = 1,
					Src = "Source",
					Msg = "Message"
				}
			).Wait();

			parser.Write
			(
				new LogQuery
				{
					From = 1,
					To = 2,
					MinSev = 1,
					MaxSev = 2,
					Src = "Source"
				}
			).Wait();

			parser.Write
			(
				new OutgoingLogEntry 
				{
					Ts = 1000,
					Src = "Source",
					Msg = "Message",
					Sev = 1
				}
			).Wait();

			parser.Write
			(
				new Truncate
				{
					Lim = 1000,
					Src = "Source"
				}
			).Wait();

			parser.Write
			(
				new Stat
				{
					Addr = "hostname",
					Lim = 1000,
					Sz = 10000
				}
			).Wait();

			parser.Terminate().Wait();

			stream.Position = 0;

			//Console.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));

			Assert.IsNotNull(parser.Read<MessageHeader>().Result);
			Assert.IsNotNull(parser.Read<IncomingLogEntry>().Result);
			Assert.IsNotNull(parser.Read<LogQuery>().Result);
			Assert.IsNotNull(parser.Read<OutgoingLogEntry>().Result);
			Assert.IsNotNull(parser.Read<Truncate>().Result);
			Assert.IsNotNull(parser.Read<Stat>().Result);
			Assert.IsNull(parser.Read<Stat>().Result);
		}
	}
}
