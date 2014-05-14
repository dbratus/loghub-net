// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace LogHub
{
	internal class Connection
	{
		private readonly TcpClient _client;
		private readonly Stream _stream;

		public Stream Stream { get { return _stream; } }

		private Connection(TcpClient client, Stream stream)
		{
			_client = client;
			_stream = stream;
		}

		public static async Task<Connection> NewConnection(string host, int port, bool useTls, bool skipCertValidation)
		{
			var client = new TcpClient();
			await client.ConnectAsync(host, port);

			if (useTls)
			{
				var sslStream = (skipCertValidation) ? 
					new SslStream(client.GetStream(), false, (sender, certificate, chain, errors) => true) :
					new SslStream(client.GetStream());

				await sslStream.AuthenticateAsClientAsync
				(
					host, 
					new X509CertificateCollection(), 
					SslProtocols.Default, 
					!skipCertValidation
				);

				return new Connection(client, sslStream);
			}

			return new Connection(client, client.GetStream());
		}

		public void Close()
		{
			_client.Close();
		}
	}
}
