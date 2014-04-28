// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogHub.Protocol
{
	internal static class Actions
	{
		public const string Write = "write";
		public const string Read = "read";
		public const string Truncate = "truncate";
		public const string Stat = "stat";
	}
}
