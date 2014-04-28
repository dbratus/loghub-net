// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;

namespace LogHub
{
	public sealed class LogEntry
	{
		public DateTime Timestamp { get; set; }
		public int Severity { get; set; }
		public string Source { get; set; }
		public string Message { get; set; }
	}
}
