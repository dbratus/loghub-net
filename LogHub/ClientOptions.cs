// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

namespace LogHub
{
	/// <summary>
	/// LogHub client options.
	/// </summary>
	public sealed class ClientOptions
	{
		/// <summary>
		/// Maximum number of connections the client may use.
		/// </summary>
		public int MaxConnections { get; set; }

		/// <summary>
		/// Whether to use TLS.
		/// </summary>
		public bool UseTls { get; set; }

		/// <summary>
		/// Whether to skip the server certificate validation.
		/// </summary>
		public bool SkipCertValidation { get; set; }

		/// <summary>
		/// User name.
		/// </summary>
		public string User { get; set; }

		/// <summary>
		/// Password.
		/// </summary>
		public string Password { get; set; }
	}
}
