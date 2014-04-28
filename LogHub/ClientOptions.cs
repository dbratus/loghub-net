// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

namespace LogHub
{
	public sealed class ClientOptions
	{
		public int MaxConnections { get; set; }
		public bool UseTls { get; set; }
		public bool SkipCertValidation { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
	}
}
