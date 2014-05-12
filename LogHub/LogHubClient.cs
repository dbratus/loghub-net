// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogHub.Protocol;
using NChannels;

namespace LogHub
{
	public sealed class LogHubClient : IDisposable
	{
		private static readonly TimeSpan WriteBufferFlushInterval = TimeSpan.FromMilliseconds(100);
		private const int WriteBufferFlushLength = 100;
		internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private readonly ConnectionPool _connectionPool;
		private readonly Chan<IncomingLogEntry> _writeChan;
		private readonly Chan<bool> _closeChan;
		private readonly Task _writer;
		private readonly string _user;
		private readonly string _password;

		private long _activeOpsCount;
		private bool _isClosed;

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

			_closeChan = new Chan<bool>();

			_user = options.User;
			_password = options.Password;
		}

		private async Task Writer()
		{
			var buf = new List<IncomingLogEntry>();
			var bufFlushedAt = DateTime.Now;

			Func<bool, Task> flushBuffer = async sync => 
			{
				var entriesToWrite = new Chan<IncomingLogEntry>((sync) ? 1 : buf.Count);

				Write(entriesToWrite);

				foreach (var incomingLogEntry in buf)
				{
					await entriesToWrite.Send(incomingLogEntry);
				}

				entriesToWrite.Close();
				buf.Clear();
				bufFlushedAt = DateTime.Now;
			};

			var run = true;

			while (run)
			{
				await new Select()
					.Case(WriteBufferFlushInterval.After(), async (now, ok) => 
					{
						if (now - bufFlushedAt > WriteBufferFlushInterval && buf.Count > 0)
						{
							await flushBuffer(false);
						}
					})
					.Case(_writeChan, async (entry, ok) => 
					{
						if (ok)
						{
							buf.Add(entry);

							if (buf.Count == WriteBufferFlushLength)
							{
								await flushBuffer(false);
							}
						}
					})
					.Case(_closeChan, async (_, ok) =>
					{
						run = false;

						await _writeChan.ForEach(entry => buf.Add(entry));

						if (buf.Count > 0)
						{
							flushBuffer(true);
						}
					})
					.End();
			}
		}

		private async Task Connect(Func<Connection, Task> task, Func<Task> onCancel)
		{
			Interlocked.Increment(ref _activeOpsCount);

			var connResult = await _connectionPool.GetConnection();

			if (!connResult.IsSuccess)
			{
				Interlocked.Decrement(ref _activeOpsCount);
				
				if (onCancel != null)
				{
					await onCancel();
				}

				return;
			}

			var isConnectionBroken = false;

			try
			{
				await task(connResult.Result);
			}
			catch (Exception ex)
			{
				try
				{
					OnError(new ExceptionEventArgs(ex));
				}
				catch (Exception) { }

				isConnectionBroken = true;
			}

			Interlocked.Decrement(ref _activeOpsCount);
			await _connectionPool.ReleaseConnection(connResult.Result, isConnectionBroken);
		}

		private async void Write(Chan<IncomingLogEntry> entriesToWrite)
		{
			await Connect
			(
				async conn => 
				{
					var jStream = new JStream(conn.Stream);

					await jStream.Write
					(
						new MessageHeader 
						{
							Action = Actions.Write,
							Pass = _password,
							Usr = _user
						}
					);

					await entriesToWrite.ForEach
					(
						async ent => 
							await jStream.Write(ent)
					);
				},
				async () => 
					await entriesToWrite.Purge()
			);
		}

		public void Write(int severity, string source, string message)
		{
			if (_isClosed) throw new ObjectDisposedException("The object is disposed.");

			_writeChan.Send
			(
				new IncomingLogEntry 
				{
					Sev = severity,
					Src = source,
					Msg = message
				}
			)
			.ContinueWith
			(
				task => 
				{
					if (task.Exception != null)
					{
						OnError(new ExceptionEventArgs(task.Exception));
					}
				}
			);
		}

		public Chan<LogEntry> Read(DateTime from, DateTime to, int minSeverity, int maxSeverity, params string[] sources)
		{
			if (_isClosed) throw new ObjectDisposedException("The object is disposed.");

			var result = new Chan<LogEntry>();
			
			DoRead(from, to, minSeverity, maxSeverity, sources, result);

			return result;
		}

		private async void DoRead(DateTime from, DateTime to, int minSeverity, int maxSeverity, string[] sources, Chan<LogEntry> result)
		{
			await Connect
			(
				async conn =>
				{
					var jStream = new JStream(conn.Stream);

					await jStream.Write
					(
						new MessageHeader
						{
							Action = Actions.Read,
							Pass = _password,
							Usr = _user
						}
					);

					foreach (var src in sources)
					{
						await jStream.Write
						(
							new LogQuery 
							{
								From = DateTimeToTs(from),
								To = DateTimeToTs(to),
								MinSev = minSeverity,
								MaxSev = maxSeverity,
								Src = src
							}
						);
					}

					await jStream.Terminate();

					OutgoingLogEntry ent;

					while((ent = await jStream.Read<OutgoingLogEntry>()) != null)
					{
						await result.Send
						(
							new LogEntry 
							{
								Timestamp = DateTimeFromTs(ent.Ts),
								Message = ent.Msg,
								Severity = ent.Sev,
								Source = ent.Src
							}
						);
					}

					result.Close();
				},
				() =>
					Task.Run(() => result.Close())
			);
		}

		public async Task Truncate(DateTime limit, params string[] sources)
		{
			if (_isClosed) throw new ObjectDisposedException("The object is disposed.");
			
			await Connect
			(
				async conn =>
				{
					var jStream = new JStream(conn.Stream);

					foreach (var src in sources)
					{
						await jStream.Write
						(
							new MessageHeader
							{
								Action = Actions.Truncate,
								Pass = _password,
								Usr = _user
							}
						);

						await jStream.Write
						(
							new Truncate
							{
								Lim = DateTimeToTs(limit),
								Src = src
							}
						);
					}
				},
				null
			);
		}

		public Chan<LogInfo> Stat()
		{
			if (_isClosed) throw new ObjectDisposedException("The object is disposed.");

			var result = new Chan<LogInfo>();

			DoStat(result);

			return result;
		}

		public async void DoStat(Chan<LogInfo> result)
		{
			await Connect
			(
				async conn =>
				{
					var jStream = new JStream(conn.Stream);

					await jStream.Write
					(
						new MessageHeader
						{
							Action = Actions.Stat,
							Pass = _password,
							Usr = _user
						}
					);

					Stat st;

					while ((st = await jStream.Read<Stat>()) != null)
					{
						await result.Send
						(
							new LogInfo 
							{
								Address = st.Addr,
								Limit = st.Lim,
								Size = st.Sz
							}
						);
					}
				},
				null
			);
		}

		public void Close()
		{
			if (_isClosed) return;

			_isClosed = true;

			_connectionPool.Dispose();

			while (Interlocked.Read(ref _activeOpsCount) > 0)
			{
				Thread.Yield();
			}

			_closeChan
				.Send(true)
				.ContinueWith(_ => _writer.Wait())
				.Wait();
		}

		public void Dispose()
		{
			Close();
		}

		internal static long DateTimeToTs(DateTime dateTime)
		{
			return (long)((dateTime.ToUniversalTime() - UnixEpoch).TotalMilliseconds * 1000000);
		}

		internal static DateTime DateTimeFromTs(long jsDateTime)
		{
			return (UnixEpoch + TimeSpan.FromMilliseconds(jsDateTime / 1000000.0)).ToLocalTime();
		}

		public event EventHandler<ExceptionEventArgs> Error;

		private void OnError(ExceptionEventArgs e)
		{
			EventHandler<ExceptionEventArgs> handler = Error;
			if (handler != null) handler(this, e);
		}
	}
}
