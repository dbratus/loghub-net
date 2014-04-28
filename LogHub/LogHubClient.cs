// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Threading.Tasks;
using LogHub.Protocol;
using NChannels;

namespace LogHub
{
	public sealed class LogHubClient : IDisposable
	{
		private readonly ConnectionPool _connectionPool;
		private readonly Chan<IncomingLogEntry> _writeChan;
		private readonly Task _writer;

		public LogHubClient(string host, int port, ClientOptions options)
		{
			_connectionPool = new ConnectionPool
			(
				host, 
				port, 
				options.UseTls, 
				options.SkipCertValidation, 
				options.MaxConnections
			);
			_connectionPool.Error += 
				(sender, args) => OnError(args);

			_writeChan = new Chan<IncomingLogEntry>();
			_writer = Writer();
		}

		private async Task Writer()
		{
			
		}

		public void Write(int severity, string source, string message)
		{
			//TODO: Implement.
		}

		public Chan<LogEntry> Read(DateTime from, DateTime to, int minSeverity, int maxSeverity, params string[] sources)
		{
			//TODO: Implement.
			return null;
		}

		public void Truncate(DateTime limit, params string[] sources)
		{
			//TODO: Implement.
		}

		public Chan<LogInfo> Stat()
		{
			//TODO: Implement.
			return null;
		} 

		public void Close()
		{
			//TODO: Implement.
		}

		public void Dispose()
		{
			//TODO: Implement.
		}

		public event EventHandler<ExceptionEventArgs> Error;

		private void OnError(ExceptionEventArgs e)
		{
			EventHandler<ExceptionEventArgs> handler = Error;
			if (handler != null) handler(this, e);
		}
	}
}
