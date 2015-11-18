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
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace OpenRA.FileFormats
{
	public class VocLoader
	{
		public int BitsPerSample { get { return 8; } }
		public int Channels { get { return 1; } }
		public int SampleRate { get; private set; }

		int totalSamples = 0;

		Stream stream;
		VocFileHeader header;
		List<VocBlock> blocks = new List<VocBlock>();
		int blockIndex;
		IEnumerator<VocBlock> currentBlock;
		int samplesLeftInBlock = 0;
		byte[] buffer = new byte[1024];

		struct VocFileHeader
		{
			public string Description;
			public int DatablockOffset;
			public int Version;
			public int ID;

			public static VocFileHeader Read(Stream s)
			{
				VocFileHeader vfh;
				vfh.Description = s.ReadASCII(20);
				vfh.DatablockOffset = s.ReadUInt16();
				vfh.Version = s.ReadUInt16();
				vfh.ID = s.ReadUInt16();
				return vfh;
			}
		}

		struct VocBlock
		{
			public int Code;
			public int Length;
			public VocSampleBlock SampleBlock;
			public VocLoopBlock LoopBlock;
		}

		struct VocSampleBlock
		{
			public int Rate;
			public int Samples;
			public long Offset;
		}

		struct VocLoopBlock
		{
			public int count;
		}

		public VocLoader(Stream stream)
		{
			this.stream = stream;
			CheckVocHeader();
			Preload();
		}

		private void CheckVocHeader()
		{
			var vfh = VocFileHeader.Read(stream);

			if (!vfh.Description.StartsWith("Creative Voice File"))
				throw new InvalidDataException("Voc header description not recognized");
			if (vfh.DatablockOffset != 26)
				throw new InvalidDataException("Voc header offset is wrong");
			if (vfh.Version != 0x010A)
				throw new InvalidDataException("Voc header version not recognized");
			if (vfh.ID != ~vfh.Version + 0x1234)
				throw new InvalidDataException("Voc header id is bogus");
		}

		private int GetSampleRateFromVocRate(int vocSampleRate)
		{
			if(vocSampleRate == 256)
				throw new InvalidDataException("Invalid frequency divisor 256 in voc file");
			if (vocSampleRate == 0xa5 || vocSampleRate == 0xa6)
				return 11025;
			else if (vocSampleRate == 0xd2 || vocSampleRate == 0xd3)
				return 22050;
			else
				return (int)(1000000L / (256L - vocSampleRate));
		}

		private void Preload()
		{
			while (true)
			{
				VocBlock block = new VocBlock();
				try
				{
					block.Code = stream.ReadByte();
					block.Length = 0;
				}
				catch(EndOfStreamException)
				{
					break;
				}

				if (block.Code == 0 || block.Code > 9)
					break;

				block.Length = stream.ReadByte();
				block.Length |= stream.ReadByte() << 8;
				block.Length |= stream.ReadByte() << 16;

				var skip = 0;
				switch (block.Code)
				{
					// Sound data
					case 1:
						{
							if (block.Length < 2)
								throw new InvalidDataException("Invalid sound data block length in voc file");
							var freqDiv = stream.ReadByte();
							block.SampleBlock.Rate = GetSampleRateFromVocRate(freqDiv);
							var codec = stream.ReadByte();
							if (codec != 0)
								throw new InvalidDataException("Unhandled codec used in voc file");
							skip = block.Length - 2;
							block.SampleBlock.Samples = skip;
							block.SampleBlock.Offset = stream.Position;
							if (blocks.Count > 0)
							{
								var b = blocks.Last();
								if (b.Code == 8)
								{
									block.SampleBlock.Rate = b.SampleBlock.Rate;
									blocks.Remove(b);
								}
							}

							if (SampleRate < block.SampleBlock.Rate)
								SampleRate = block.SampleBlock.Rate;
							break;
						}
						// Silence
					case 3:
						{
							if (block.Length != 3)
								throw new InvalidDataException("Invalid silence block length in voc file");
							block.SampleBlock.Offset = 0;
							block.SampleBlock.Samples = stream.ReadUInt16() + 1;
							var freqDiv = stream.ReadByte();
							block.SampleBlock.Rate = GetSampleRateFromVocRate(freqDiv);
							break;
						}
						// Repeat start
					case 6:
						{
							if (block.Length != 2)
								throw new InvalidDataException("Invalid repeat start block length in voc file");
							block.LoopBlock.count = stream.ReadUInt16() + 1;
							break;
						}
						// Repeat end
					case 7:
						break;
						// Extra info
					case 8:
						{
							if(block.Length != 4)
								throw new InvalidDataException("Invalid info block length in voc file");
							int freqDiv = stream.ReadUInt16();
							if (freqDiv == 65536)
								throw new InvalidDataException("Invalid frequency divisor 65536 in voc file");
							var codec = stream.ReadByte();
							if (codec != 0)
								throw new InvalidDataException("Unhandled codec used in voc file");
							var channels = stream.ReadByte() + 1;
							if (channels != 1)
								throw new InvalidDataException("Unhandled number of channels in voc file");
							block.SampleBlock.Offset = 0;
							block.SampleBlock.Samples = 0;
							block.SampleBlock.Rate = (int)(256000000L / (65536L - freqDiv));
							break;
						}
					// Sound data (New format)
					case 9:
					default:
						throw new InvalidDataException("Unhandled code in voc file");
				}
				if (skip > 0)
					stream.Seek(skip, SeekOrigin.Begin);
				blocks.Add(block);
			}
			foreach(var b in blocks)
			{
				if (b.Code == 8)
					throw new InvalidDataException("Unused block 8 in voc file");
				if (b.Code != 1 && b.Code != 9)
					continue;
				if (b.SampleBlock.Rate != SampleRate)
					throw new InvalidDataException("Voc file contains chunks with different sample rate");
				totalSamples += b.SampleBlock.Samples;
			}

			Rewind();
		}

		void Rewind()
		{
			currentBlock = blocks.GetEnumerator();

			while (currentBlock.MoveNext() && currentBlock.Current.Code != 1);

			stream.Seek(currentBlock.Current.SampleBlock.Offset, SeekOrigin.Begin);
			samplesLeftInBlock = currentBlock.Current.SampleBlock.Samples;
		}

		bool EndOfData { get { return currentBlock.Current.Equals(blocks.Last()) && samplesLeftInBlock == 0; } }

		int FillBuffer(int maxSamples)
		{
			int bufferedSamples = 0;
			int offset = 0;

			maxSamples = Math.Min(buffer.Length, maxSamples);

			while (maxSamples > 0 && !EndOfData)
			{
				int len = Math.Min(maxSamples, samplesLeftInBlock);
				stream.ReadBytes(buffer, offset, len);
				offset += len;
				int samplesRead = len;
				bufferedSamples += samplesRead;
				maxSamples -= samplesRead;
				samplesLeftInBlock -= samplesRead;

				UpdateBlockIfNeeded();
			}
			return bufferedSamples;
		}

		void UpdateBlockIfNeeded()
		{
			if (samplesLeftInBlock == 0)
			{
				while(currentBlock.MoveNext())
				{
					if (currentBlock.Current.Code != 1 && currentBlock.Current.Code != 9)
						continue;
					stream.Seek(currentBlock.Current.SampleBlock.Offset, SeekOrigin.Begin);
					samplesLeftInBlock = currentBlock.Current.SampleBlock.Samples;
					return;
				}
			}
		}
		
		public byte[] readBuffer(int numSamples)
		{
			byte[] buf = new byte[numSamples];
			int offset = 0;
			var samplesLeft = numSamples;
			while(samplesLeft > 0)
			{
				int len = FillBuffer(samplesLeft);
				if (len == 0)
					break;
				samplesLeft -= len;
				Array.Copy(buffer, 0, buf, offset, len);
				offset += len;
			}
			return buf;
		}

		public byte[] ReadAllBytes()
		{
			return readBuffer(totalSamples);
		}
	}
}
