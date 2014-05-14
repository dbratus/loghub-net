// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

namespace LogHub
{
	/// <summary>
	/// Log information.
	/// </summary>
	public sealed class LogInfo
	{
		/// <summary>
		/// Address of the log.
		/// </summary>
		public string Address { get; set; }

		/// <summary>
		/// The current size of the log.
		/// </summary>
		public long Size { get; set; }

		/// <summary>
		/// The soft size limit of the log.
		/// </summary>
		public long Limit { get; set; }
	}
}
