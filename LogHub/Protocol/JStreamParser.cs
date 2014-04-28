// Copyright (C) 2014 Dmitry Bratus
//
// The use of this source code is governed by the license
// that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace LogHub.Protocol
{
	internal class JStreamParser
	{
		private const int DefaultCapacity = 256;
		private static readonly byte[] Terminator = new byte[] { 0 };

		private readonly Stream _stream;
		private readonly MemoryStream _accumulator = new MemoryStream(DefaultCapacity);
		private readonly byte[] _buf = new byte[DefaultCapacity];

		private int _readLim;
		private int _readIdx;

		public JStreamParser(Stream stream)
		{
			_stream = stream;
		}

		public async Task<T> Read<T>() where T:class
		{
			while (true)
			{
				if (_readIdx == _readLim)
				{
					_readLim = await _stream.ReadAsync(_buf, 0, _buf.Length);

					if (_readLim == 0)
					{
						return null;
					}

					_readIdx = 0;
				}

				while(_readIdx < _readLim)
				{
					byte b;

					if ((b = _buf[_readIdx++]) != 0)
					{
						_accumulator.WriteByte(b);
					}
					else
					{
						if (_accumulator.Length == 0)
						{
							return null;
						}

						var serializer = new DataContractJsonSerializer(typeof(T));
						
						_accumulator.Position = 0;
						
						var obj = (T)serializer.ReadObject(_accumulator);
						
						_accumulator.Position = 0;
						_accumulator.SetLength(0);
						
						return obj;
					}
				}
			}
		}

		public async Task Write<T>(T obj) where T : class
		{
			_accumulator.Position = 0;

			var serializer = new DataContractJsonSerializer(typeof(T));

			serializer.WriteObject(_accumulator, obj);

			await _stream.WriteAsync(_accumulator.GetBuffer(), 0, (int)_accumulator.Length);
			await _stream.WriteAsync(Terminator, 0, 1);

			_accumulator.SetLength(0);
			_accumulator.Position = 0;
		}

		public async Task Terminate()
		{
			await _stream.WriteAsync(Terminator, 0, 1);
		}
	}
}
