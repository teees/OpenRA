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
	public class WavLoader : ISoundLoader
	{
		public bool TryParseSound(Stream stream, out ISoundFormat sound)
		{
			try
			{
				sound = new WavFormat(stream);
				return true;
			}
			catch
			{
				// Not a (supported) WAV
			}

			sound = null;
			return false;
		}
	}

	public class WavFormat : ISoundFormat
	{
		public int Channels { get; private set; }
		public int SampleBits { get; private set; }
		public int SampleRate { get; private set; }
		public float LengthInSeconds { get { return (float)dataLength / (Channels * SampleRate * SampleBits); } }
		public Stream GetPCMInputStream() { return new MemoryStream(GetRawData()); }

		RiffChunk riffWaveChunk;

		WaveType waveType;
		int blockAlign;

		int uncompressedSize;

		long dataOffset;
		int dataLength;

		Stream stream;

		public enum WaveType
		{
			Pcm = 0x1,
			ImaAdpcm = 0x11
		}

		struct RiffChunk
		{
			public string ChunkID;
			public uint ChunkSize;
			public string RiffType;

			public static RiffChunk Read(Stream s)
			{
				RiffChunk rc;
				rc.ChunkID = s.ReadASCII(4);
				rc.ChunkSize = s.ReadUInt32();
				rc.RiffType = s.ReadASCII(4);
				return rc;
			}
		}

		public WavFormat(Stream stream)
		{
			this.stream = stream;

			ParseHeader();
			Preload();
		}

		void ParseHeader()
		{
			riffWaveChunk = RiffChunk.Read(stream);
			if (riffWaveChunk.ChunkID != "RIFF")
				throw new InvalidDataException("Unsupported WAV type \"" + riffWaveChunk.ChunkID + "\"");
			if (riffWaveChunk.RiffType != "WAVE")
				throw new InvalidDataException("Unsupported WAV format \"" + riffWaveChunk.RiffType + "\"");
		}

		void Preload()
		{
			while (stream.Position < stream.Length)
			{
				if ((stream.Position & 1) == 1)
					stream.ReadByte(); // Alignment

				var type = stream.ReadASCII(4);
				var chunkSize = stream.ReadInt32();
				var skip = chunkSize;
				switch (type)
				{
					case "fmt ":
						{
							waveType = (WaveType)stream.ReadInt16();

							if (waveType != WaveType.Pcm && waveType != WaveType.ImaAdpcm)
								throw new NotSupportedException("Compression type is not supported.");

							Channels = stream.ReadInt16();
							SampleRate = stream.ReadInt32();
							var byteRate = stream.ReadInt32();
							blockAlign = stream.ReadInt16();
							SampleBits = stream.ReadInt16();
							skip = chunkSize - 16;
							break;
						}

					case "fact":
						{
							uncompressedSize = stream.ReadInt32();
							skip = chunkSize - 4;
							break;
						}

					case "data":
						{
							dataOffset = stream.Position;
							dataLength = chunkSize;
							break;
						}

					default:
						// Ignore unknown chunks
						break;
				}

				if (skip > 0)
					stream.Seek(skip, SeekOrigin.Current);
			}
		}

		byte[] DecodeImaAdpcmData(byte[] rawData)
		{
			var s = new MemoryStream(rawData);

			var numBlocks = dataLength / blockAlign;
			var blockDataSize = blockAlign - (Channels * 4);
			var outputSize = uncompressedSize * Channels * 2;

			var outOffset = 0;
			var output = new byte[outputSize];

			var predictor = new int[Channels];
			var index = new int[Channels];

			// Decode each block of IMA ADPCM data in RawOutput
			for (var block = 0; block < numBlocks; block++)
			{
				// Each block starts with a initial state per-channel
				for (var c = 0; c < Channels; c++)
				{
					predictor[c] = s.ReadInt16();
					index[c] = s.ReadUInt8();
					/* unknown/reserved */
					s.ReadUInt8();

					// Output first sample from input
					output[outOffset++] = (byte)predictor[c];
					output[outOffset++] = (byte)(predictor[c] >> 8);

					if (outOffset >= outputSize)
						return output;
				}

				// Decode and output remaining data in this block
				var blockOffset = 0;
				while (blockOffset < blockDataSize)
				{
					for (var c = 0; c < Channels; c++)
					{
						// Decode 4 bytes (to 16 bytes of output) per channel
						var chunk = s.ReadBytes(4);
						var decoded = ImaAdpcmLoader.LoadImaAdpcmSound(chunk, ref index[c], ref predictor[c]);

						// Interleave output, one sample per channel
						var outOffsetChannel = outOffset + (2 * c);
						for (var i = 0; i < decoded.Length; i += 2)
						{
							var outOffsetSample = outOffsetChannel + i;
							if (outOffsetSample >= outputSize)
								return output;

							output[outOffsetSample] = decoded[i];
							output[outOffsetSample + 1] = decoded[i + 1];
							outOffsetChannel += 2 * (Channels - 1);
						}

						blockOffset += 4;
					}

					outOffset += 16 * Channels;
				}
			}

			return output;
		}
		byte[] GetRawData()
		{
			stream.Seek(dataOffset, SeekOrigin.Begin);
			var rawData = stream.ReadBytes(dataLength);

			if (waveType == WaveType.ImaAdpcm)
			{
				rawData = DecodeImaAdpcmData(rawData);
				SampleBits = 16;
			}

			return rawData;
		}

		int Read(byte[] buffer, int offset, int count)
		{
			var bytesWritten = 0;
			var bytesLeft = Math.Min(count, buffer.Length - offset);
			while (bytesLeft > 0)
			{
				var len = stream.Read(buffer, offset, bytesLeft);
				if (len == 0)
					break;
				bytesLeft -= len;
				offset += len;
				bytesWritten += len;
			}

			return bytesWritten;
		}
	}
}