// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;

namespace LogHub
{
	/// <summary>
	/// Log entry.
	/// </summary>
	public sealed class LogEntry
	{
		/// <summary>
		/// The timestamp of the entry.
		/// </summary>
		public DateTime Timestamp { get; set; }

		/// <summary>
		/// The severity.
		/// </summary>
		public int Severity { get; set; }

		/// <summary>
		/// The logging source.
		/// </summary>
		public string Source { get; set; }

		/// <summary>
		/// The message.
		/// </summary>
		public string Message { get; set; }
	}
}
