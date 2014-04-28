// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogHub
{
	public class ExceptionEventArgs : EventArgs
	{
		public Exception Exception { get; private set; }

		public ExceptionEventArgs(Exception ex)
		{
			Exception = ex;
		}
	}
}
