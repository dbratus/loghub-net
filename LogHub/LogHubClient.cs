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
	/// <summary>
	/// LogHub client.
	/// </summary>
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

		/// <summary>
		/// Creates a new client.
		/// </summary>
		/// <param name="host">Host of a log or hub.</param>
		/// <param name="port">Port of a log or hub.</param>
		/// <param name="options">Client options.</param>
		public LogHubClient(string host, int port, ClientOptions options)
		{
			if (string.IsNullOrEmpty(host)) throw new ArgumentException("Host must be specified.");
			if (port <= 0) throw new ArgumentException("Invalid port.");
			if (options.MaxConnections < 1) throw new ArgumentException("Invalid maximum connections limit.");

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
			_closeChan = new Chan<bool>();

			_writer = Writer();

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
							await flushBuffer(true);
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

					await jStream.Terminate();
				},
				async () => 
					await entriesToWrite.Purge()
			);
		}

		/// <summary>
		/// Writes a single entry to the log.
		/// </summary>
		/// <param name="severity">The severity of the entry.</param>
		/// <param name="source">The source of the entry.</param>
		/// <param name="message">The message.</param>
		public void Write(int severity, string source, string message)
		{
			if (_isClosed) throw new ObjectDisposedException("The object is disposed.");
			if (severity < 0 || severity > 255) throw new ArgumentException("Severity must be within [1; 255].");
			if (string.IsNullOrEmpty(source)) throw new ArgumentException("Source must be specified.");
			if (string.IsNullOrEmpty(message)) throw new ArgumentException("Message must be specified.");

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

		/// <summary>
		/// Reads log entries.
		/// </summary>
		/// <param name="from">The start of the timestamp range.</param>
		/// <param name="to">The end of the timestamp range.</param>
		/// <param name="minSeverity">Minimal severity of the entries to return.</param>
		/// <param name="maxSeverity">Maximal severity of the entries to return.</param>
		/// <param name="sources">Regular expressions that the log sources must match.</param>
		/// <returns>Channel of log entries.</returns>
		public Chan<LogEntry> Read(DateTime from, DateTime to, int minSeverity, int maxSeverity, params string[] sources)
		{
			if (_isClosed) throw new ObjectDisposedException("The object is disposed.");
			if (minSeverity < 0 || minSeverity > 255) throw new ArgumentException("Severity must be within [1; 255].");
			if (maxSeverity < 0 || maxSeverity > 255) throw new ArgumentException("Severity must be within [1; 255].");

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

					if (sources.Length > 0)
					{
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
					}
					else
					{
						await jStream.Write
						(
							new LogQuery
							{
								From = DateTimeToTs(from),
								To = DateTimeToTs(to),
								MinSev = minSeverity,
								MaxSev = maxSeverity,
								Src = string.Empty
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
					Task.Factory.StartNew(result.Close)
			);
		}

		/// <summary>
		/// Truncates the log from the specified limit.
		/// </summary>
		/// <param name="limit">The time before which the log entries must be truncated.</param>
		/// <param name="sources">Regular expressions that the log sources must match.</param>
		/// <returns>A task representing the completion of the message sending operation.</returns>
		public async Task Truncate(DateTime limit, params string[] sources)
		{
			if (_isClosed) throw new ObjectDisposedException("The object is disposed.");
			
			await Connect
			(
				async conn =>
				{
					var jStream = new JStream(conn.Stream);

					if (sources.Length > 0)
					{
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
					}
					else
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
								Src = string.Empty
							}
						);
					}
				},
				null
			);
		}

		/// <summary>
		/// Returns information on the logs.
		/// </summary>
		/// <returns>A channel of log information entries.</returns>
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

					result.Close();
				},
				null
			);
		}

		/// <summary>
		/// Closes the client.
		/// </summary>
		public void Close()
		{
			if (_isClosed) return;

			_isClosed = true;

			while (Interlocked.Read(ref _activeOpsCount) > 0)
			{
				Thread.Yield();
			}

			_writeChan.Close();
			_closeChan
				.Send(true)
				.ContinueWith(_ => _writer.Wait())
				.Wait();

			_connectionPool.Dispose();
		}

		/// <summary>
		/// Disposes the client.
		/// </summary>
		public void Dispose()
		{
			Close();
		}

		internal static long DateTimeToTs(DateTime dateTime)
		{
			return (long)(Math.Floor((dateTime.ToUniversalTime() - UnixEpoch).TotalMilliseconds) * 1000000);
		}

		internal static DateTime DateTimeFromTs(long jsDateTime)
		{
			return (UnixEpoch + TimeSpan.FromMilliseconds(Math.Floor(jsDateTime / 1000000.0))).ToLocalTime();
		}

		public event EventHandler<ExceptionEventArgs> Error;

		private void OnError(ExceptionEventArgs e)
		{
			EventHandler<ExceptionEventArgs> handler = Error;
			if (handler != null) handler(this, e);
		}
	}
}
