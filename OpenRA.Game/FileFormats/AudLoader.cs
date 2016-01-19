#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.IO;

namespace OpenRA.FileFormats
{
	public class AudLoader : ISoundLoader
	{
		public bool TryParseSound(Stream stream, out ISoundFormat sound)
		{
			try
			{
				sound = new AudFormat(stream);
				return true;
			}
			catch
			{
				// Not a (supported) AUD
			}

			sound = null;
			return false;
		}
	}

	public class AudFormat : ISoundFormat
	{
		public int Channels { get { return 1; } }
		public int SampleBits { get { return 16; } }
		public int SampleRate { get; set; }

		public float LengthInSeconds {
			get
			{
				var samples = outputSize;
				if (flags.HasFlag(SoundFlags.Stereo)) samples /= 2;
				if (flags.HasFlag(SoundFlags._16Bit)) samples /= 2;
				return (float)samples / SampleRate;
			}
		}

		int dataSize;
		int outputSize;
		SoundFlags flags;
		int format;

		Stream stream;

		[Flags]
		enum SoundFlags
		{
			Stereo = 0x1,
			_16Bit = 0x2,
		}

		enum SoundFormat
		{
			WestwoodCompressed = 1,
			ImaAdpcm = 99,
		}

		struct Chunk
		{
			public int CompressedSize;
			public int OutputSize;

			public static Chunk Read(Stream s)
			{
				Chunk c;
				c.CompressedSize = s.ReadUInt16();
				c.OutputSize = s.ReadUInt16();

				if (s.ReadUInt32() != 0xdeaf)
					throw new InvalidDataException("Chunk header is bogus");
				return c;
			}
		}

		static readonly int[] IndexAdjust = { -1, -1, -1, -1, 2, 4, 6, 8 };
		static readonly int[] StepTable =
		{
			7, 8, 9, 10, 11, 12, 13, 14, 16,
			17, 19, 21, 23, 25, 28, 31, 34, 37,
			41, 45, 50, 55, 60, 66, 73, 80, 88,
			97, 107, 118, 130, 143, 157, 173, 190, 209,
			230, 253, 279, 307, 337, 371, 408, 449, 494,
			544, 598, 658, 724, 796, 876, 963, 1060, 1166,
			1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749,
			3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
			7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289,
			16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
		};

		public AudFormat(Stream stream)
		{
			this.stream = stream;
			ParseHeader();
		}

		static short DecodeSample(byte b, ref int index, ref int current)
		{
			var sb = (b & 8) != 0;
			b &= 7;

			var delta = (StepTable[index] * b) / 4 + StepTable[index] / 8;
			if (sb) delta = -delta;

			current += delta;
			if (current > short.MaxValue) current = short.MaxValue;
			if (current < short.MinValue) current = short.MinValue;

			index += IndexAdjust[b];
			if (index < 0) index = 0;
			if (index > 88) index = 88;

			return (short)current;
		}

		public static byte[] LoadSound(byte[] raw, ref int index)
		{
			var s = new MemoryStream(raw);
			var dataSize = raw.Length;
			var outputSize = raw.Length * 4;

			var output = new byte[outputSize];
			var offset = 0;
			var currentSample = 0;

			while (dataSize-- > 0)
			{
				var b = s.ReadUInt8();

				var t = DecodeSample(b, ref index, ref currentSample);
				output[offset++] = (byte)t;
				output[offset++] = (byte)(t >> 8);

				t = DecodeSample((byte)(b >> 4), ref index, ref currentSample);
				output[offset++] = (byte)t;
				output[offset++] = (byte)(t >> 8);
			}

			return output;
		}

		void ParseHeader()
		{
			SampleRate = stream.ReadUInt16();
			dataSize = stream.ReadInt32();
			outputSize = stream.ReadInt32();
			flags = (SoundFlags)stream.ReadByte();
			format = stream.ReadByte();

			if (!Enum.IsDefined(typeof(SoundFlags), flags))
				throw new InvalidDataException("Unsupported AUD flag \"" + flags.ToString("X") + "\"");

			if (!Enum.IsDefined(typeof(SoundFormat), format))
				throw new InvalidDataException("Unsupported AUD format \"" + format.ToString("X") + "\"");
		}

		public Stream GetPCMInputStream()
		{
			return new AudStream(this);
		}

		class AudStream : Stream
		{
			AudFormat format;
			byte[] buffer = new byte[4096];
			int currentSample = 0;
			int index = 0;
			int samplesInChunk = 0;
			int offset = 0;
			int bytesLeft;
			public AudStream(AudFormat format)
			{
				this.format = format;
				bytesLeft = format.dataSize;
			}

			public override bool CanRead { get { return bytesLeft > 0; } } 
			public override bool CanSeek { get { return false; } }
			public override bool CanWrite { get { return false; } }
			public override long Length { get { return format.outputSize; } }

			public override long Position
			{
				get { return offset; }
				set { throw new NotImplementedException(); }
			}

			int FillBuffer(int count)
			{
				var samplesInBuffer = 0;
				var samplesToBuffer = Math.Min(buffer.Length, count);
				while (samplesToBuffer > 0 && bytesLeft > 0)
				{
					if (samplesInChunk <= 0)
					{
						var chunk = Chunk.Read(format.stream);
						samplesInChunk = chunk.CompressedSize;
						bytesLeft -= 8;
					}

					var b = format.stream.ReadUInt8();
					samplesInChunk--;
					bytesLeft--;

					var t = DecodeSample(b, ref index, ref currentSample);
					buffer[samplesInBuffer++] = (byte)t;
					buffer[samplesInBuffer++] = (byte)(t >> 8);
					offset += 2;
					samplesToBuffer -= 2;

					if (samplesToBuffer >= 2)
					{
						t = DecodeSample((byte)(b >> 4), ref index, ref currentSample);
						buffer[samplesInBuffer++] = (byte)t;
						buffer[samplesInBuffer++] = (byte)(t >> 8);
						offset += 2;
						samplesToBuffer -= 2;
					}
				}
				return samplesInBuffer;
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				var bytesWritten = 0;
				var samplesLeft = Math.Min(count, buffer.Length - offset);
				while (samplesLeft > 0)
				{
					var len = FillBuffer(samplesLeft);
					if (len == 0)
						break;
					Buffer.BlockCopy(this.buffer, 0, buffer, offset, len);
					samplesLeft -= len;
					offset += len;
					bytesWritten += len;
				}

				return bytesWritten;
			}

			public override void Flush() { throw new NotImplementedException(); }
			public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
			public override void SetLength(long value) { throw new NotImplementedException(); }
			public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }
		}
	}
}
