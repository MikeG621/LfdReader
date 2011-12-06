/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 1.0
 */

/* CHANGELOG
 * 110914 - housekeeping, ArgEx to Frequency
 * 110922 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110924 - implemented Decode/EncodeResource(), added SoundDataBlock/SoundBlocks, removed Data, added check to audio data length
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets BLAS and VOIC audio resources</summary>
	/// <remarks>This is under the assumption that BLAS/VOIC data is always uncompressed 8-bit PCM</remarks>
	public class Blas : Resource
	{
		SoundDataBlock[] _soundBlocks;
		int _frequency;
		const int _vocHeaderLength = 0x1A;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Blas(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Blas(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}

		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		//===================
		/// <summary>Processes raw data to create Blas/Voic information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = _vocHeaderLength;
			_decodeResource(raw, containsHeader);
			_soundBlocks = new SoundDataBlock[2];	// maximum number of Sound blocks observed in TIE
			for (int i = 0; i < _soundBlocks.Length; i++)
			{
				if (_rawData[offset] == 6)
				{
					//System.Diagnostics.Debug.WriteLine("repeat block");
					_soundBlocks[i].NumberOfRepeats = BitConverter.ToInt16(_rawData, offset + 1);	// DoesRepeat auto-set to true
					offset += 6;
				}
				else _soundBlocks[i].DoesRepeat = false;
				if (_rawData[offset] == 1)
				{
					//System.Diagnostics.Debug.WriteLine("data block");
					int len = BitConverter.ToUInt16(_rawData, offset + 1) + _rawData[offset + 3] * 256 * 256; // stupid 3-byte value
					_frequency = 1000000 / (256 - _rawData[offset + 4]);
					_soundBlocks[i].Data = new byte[len - 2];
					ArrayFunctions.TrimArray(_rawData, offset + 6, _soundBlocks[i].Data);
					//^ Buffer.BlockCopy(_rawData, offset + 6, _soundBlocks[i].Data, 0, _soundBlocks[i].Data.Length);
					if (i != _soundBlocks.Length - 1) offset += len + 4 + (_soundBlocks[i].DoesRepeat ? 4 : 0);	// prep offset for next block
				}
				else if (_rawData[offset] == 0)	// i != 0
				{
					//System.Diagnostics.Debug.WriteLine("end block");
					for (int j = i; j < _soundBlocks.Length; j++)
					{
						//System.Diagnostics.Debug.WriteLine("set null...");
						if (_soundBlocks[j].Data != null) _soundBlocks[j].Data = null;
						//System.Diagnostics.Debug.WriteLine("complete");
					}
					break;
				}
			}
			//System.Diagnostics.Debug.WriteLine("blocks complete");
		}

		/// <summary>Prepare Blas/Voic information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			int len = _vocHeaderLength + 1;	// VocHeader + EofBlock
			foreach (SoundDataBlock sdb in _soundBlocks)
				if (sdb.Data != null) len += sdb.Data.Length + 6 + (sdb.DoesRepeat ? 0xA : 0);	// 6 = SoundBlockHeader + FreqDivisor + Codec
				else break;
			byte[] raw = new byte[len];
			ArrayFunctions.WriteToArray("Creative Voice File", raw, 0);
			raw[0x13] = 0x1A;	// char, EOF, prevents printing?
			raw[0x14] = 0x1A;	// short, HEADER_LENGTH
			ArrayFunctions.WriteToArray((short)0x10A, raw, 0x16);	// VOC_VERSION
			ArrayFunctions.WriteToArray((short)0x1129, raw, 0x18); // VERSION VERIFY
			int offset = _vocHeaderLength;
			foreach (SoundDataBlock sdb in _soundBlocks)
			{
				if (sdb.Data == null) break;
				if (sdb.DoesRepeat)
				{
					ArrayFunctions.WriteToArray((int)0x206, raw, ref offset);	// Type=0x06, Length=0x000002
					ArrayFunctions.WriteToArray(sdb.NumberOfRepeats, raw, ref offset);
				}
				ArrayFunctions.WriteToArray((int)((sdb.Data.Length + 2) << 1 + 1), raw, offset);	// Type=0x01, Length=(_data.Length+2)
				raw[offset + 4] = (byte)Math.Round((decimal)(256 - (1000000 / _frequency)));
				// raw[offset+5] = 0, Codec
				ArrayFunctions.WriteToArray(sdb.Data, raw, offset + 6);
				if (sdb.DoesRepeat) raw[offset + 6 + sdb.Data.Length] = 7;	// EndBlock.Type
				offset += len + 4 + (sdb.DoesRepeat ? 4 : 0);	// prep offset for next block
			}
			// last byte is EofBlock
			_rawData = raw;
		}

		/// <value>Frequency in Hertz of audio data.</value>
		/// <remarks>Value must be 10-12 kHz</remarks>
		/// <exception cref="ArgumentException">value is outside the required range</exception>
		public int Frequency
		{
			get { return _frequency; }
			set
			{
				if (value < 10000 || value > 12000) throw new ArgumentException("value must be 10-12 kHz", "value");
				_frequency = value;
			}
		}

		/// <summary>Gets the audio data array</summary>
		/// <remarks>Array length is always 2 as that's the maximum seen in TIE, although more should be possible</remarks>
		public SoundDataBlock[] SoundBlocks { get { return _soundBlocks; } }

		/// <summary>Container for audio data and repeat information</summary>
		public struct SoundDataBlock
		{
			// FrequencyDivisor is left global, Codec is ignored and always 00
			short _numberOfRepeats;
			bool _doesRepeat;
			byte[] _data;

			/// <summary>Raw 8-bit PCM audio data</summary>
			/// <remarks>Array is restricted to 0xFFFFFD maximum length</remarks>
			/// <exception cref="ArgumentException">Array length exceeds limits</exception>
			public byte[] Data
			{
				get { return _data; }
				set
				{
					if (value.Length > 0xFFFFFD) throw new ArgumentException("Audio array is too long");
					_data = value;
				}
			}

			/// <summary>Gets or sets the number of repeats</summary>
			/// <remarks>Values -1 (infinite repeat) and higher activate repeat flag, -2 and lower deactivate</remarks>
			public short NumberOfRepeats
			{
				get { return _numberOfRepeats; }
				set
				{
					_numberOfRepeats = value;
					if (_numberOfRepeats <= -2) _doesRepeat = false;
					else _doesRepeat = true;
				}
			}

			/// <summary>Gets or sets the repeat flag</summary>
			public bool DoesRepeat
			{
				get { return _doesRepeat; }
				set
				{
					_doesRepeat = value;
					if (!_doesRepeat) _numberOfRepeats = -2;
				}
			}
		}
	}
}
