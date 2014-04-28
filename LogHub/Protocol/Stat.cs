﻿// Copyright (C) 2014 Dmitry Bratus
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
	public class Stat
	{
		[DataMember]
		public string Addr { get; set; }

		[DataMember]
		public long Sz { get; set; }

		[DataMember]
		public long Lim { get; set; }
	}
}