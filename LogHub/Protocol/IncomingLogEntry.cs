// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace LogHub.Protocol
{
	[DataContract]
	internal class IncomingLogEntry
	{
		[DataMember]
		public int Sev { get; set; }

		[DataMember]
		public string Src { get; set; }

		[DataMember]
		public string Msg { get; set; }
	}
}
