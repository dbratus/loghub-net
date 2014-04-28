// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NChannels;

namespace LogHub
{
	internal class ConnectionPool : IDisposable
	{
		private readonly Chan<Chan<Connection>> _getConnChan = new Chan<Chan<Connection>>();
		private readonly Chan<Connection> _putConnChan = new Chan<Connection>();
		private readonly Chan<Connection> _brokenConnChan = new Chan<Connection>();
		private readonly Chan<bool> _closeChan = new Chan<bool>();
		private readonly Task _actor;

		private readonly string _host;
		private readonly int _port;
		private readonly bool _useTls;
		private readonly bool _skipCertValidation;
		private readonly int _maxSize;

		private long _activeOpsCnt;
		private int _isClosed;

		public ConnectionPool(string host, int port, bool useTls, bool skipCertValidation, int maxSize)
		{
			_host = host;
			_port = port;
			_maxSize = maxSize;
			_useTls = useTls;
			_skipCertValidation = skipCertValidation;
			_actor = Actor();
		}

		public async Task<ChanResult<Connection>> GetConnection()
		{
			var result = new Chan<Connection>();
			await _getConnChan.Send(result);
			return await result.Receive();
		}

		public async Task ReleaseConnection(Connection connection, bool isBroken)
		{
			if (isBroken)
			{
				await _brokenConnChan.Send(connection);
			}
			else
			{
				await _putConnChan.Send(connection);
			}
		}

		public void Dispose()
		{
			Interlocked.Increment(ref _isClosed);

			_getConnChan.Close();

			while (Interlocked.Read(ref _activeOpsCnt) > 0)
			{
				Thread.Yield();
			}

			_putConnChan.Close();
			_brokenConnChan.Close();
			_closeChan
				.Send(true)
				.ContinueWith(_ => _actor.Wait())
				.Wait();
		}

		private async Task Actor()
		{
			var connections = new Queue<Connection>();
			var waiters = new Queue<Chan<Connection>>();
			var totalConnections = 0;

			try
			{
				connections.Enqueue(await Connection.NewConnection(_host, _port, _useTls, _skipCertValidation));
				totalConnections++;
			}
			catch(Exception ex)
			{
				OnError(new ExceptionEventArgs(ex));
			}

			var run = true;

			while (run)
			{
				try
				{
					await new Select()
						.Case(_getConnChan, async (connChan, ok) =>
						{
							if (ok)
							{
								try
								{
									if (connections.Count > 0)
									{
										await connChan.Send(connections.Dequeue());
									}
									else if (totalConnections < _maxSize)
									{
										await connChan.Send
										(
											await Connection.NewConnection
											(
												_host, 
												_port, 
												_useTls, 
												_skipCertValidation
											)
										);
										totalConnections++;
									}
									else
									{
										waiters.Enqueue(connChan);
									}
								}
								catch (Exception ex)
								{
									OnError(new ExceptionEventArgs(ex));
								}
								finally
								{
									connChan.Close();
								}
							}
						})
						.Case(_putConnChan, async (conn, ok) =>
						{
							if (ok)
							{
								try
								{
									if (waiters.Count == 0)
									{
										connections.Enqueue(conn);
									}
									else
									{
										var waiter = waiters.Dequeue();
										await waiter.Send(conn);
									}
								}
								catch (Exception ex)
								{
									OnError(new ExceptionEventArgs(ex));
								}
							}
						})
						.Case(_brokenConnChan, (conn, ok) =>
						{
							if (ok)
							{
								try
								{
									conn.Close();
								}
								catch(Exception ex)
								{
									OnError(new ExceptionEventArgs(ex));
								}

								totalConnections--;
							}
						})
						.Case(_closeChan, async (_, ok) =>
						{
							run = false;

							await _getConnChan.ForEach(c => c.Close());
							await _putConnChan.ForEach
							(
								c => 
								{
									try
									{
										c.Close();
									} 
									catch(Exception ex)
									{
										OnError(new ExceptionEventArgs(ex));
									}
								}
							);
							await _brokenConnChan.ForEach
							(
								c =>
								{
									try
									{
										c.Close();
									}
									catch (Exception ex)
									{
										OnError(new ExceptionEventArgs(ex));
									}
								}
							);

							foreach (var conn in connections)
							{
								try
								{
									conn.Close();
								}
								catch(Exception ex)
								{
									OnError(new ExceptionEventArgs(ex));
								}
							}
						})
						.End();
				}
				catch(Exception ex)
				{
					OnError(new ExceptionEventArgs(ex));
				}
			}
		}

		public event EventHandler<ExceptionEventArgs> Error;

		protected virtual void OnError(ExceptionEventArgs e)
		{
			EventHandler<ExceptionEventArgs> handler = Error;
			if (handler != null) handler(this, e);
		}
	}
}
