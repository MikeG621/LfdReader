/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2014 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.1
 */

/* CHANGE LOG
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "BLAS" and "VOIC" audio resources</summary>
	/// <remarks>The Blas resource is used for both the <see cref="Resource.ResourceType.Blas"/> and <see cref="Resource.ResourceType.Voic"/> types, as they are in fact the same resource. The only difference is the usage of the resources within the program. Blas resources tend to be sound effects such as doors, weapons, etc. Voic resources as one might guess are voice audio, primarily for cutscene use. The resources themselves are wrappers for Creative Voice Files (*.voc). Although the format supports other methods, it is assumed that the audio data is always uncompressed 8-bit PCM.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ VocHeader Header;
	///   /* 0x1A */ VocDataBlock[] Blocks;
	/// }
	/// 
	/// struct VocHeader
	/// {
	///   /* 0x00 */ char[19] Reserved = "Creative Voice File";
	///   /* 0x13 */ byte Reserved = 0x1A;
	///   /* 0x14 */ short HeaderLength = 0x1A;
	///   /* 0x16 */ short Version = 0x010A;
	///   /* 0x18 */ short VersionVerify = 0x1129;
	/// }
	///
	/// struct VocDataBlock
	/// {
	///   /* 0x00 */ byte Type;
	///   #if (Type != 0)	// 0 is EOF block
	///     /* 0x01 */ byte[3] Length;
	///     #if (Type == 1) // Sound Data block
	///       /* 0x04 */ byte FrequencyDivisor;
	///       /* 0x05 */ byte Codec = 0x00;
	///       /* 0x06 */ byte[Length-2] AudioData;
	///     #elseif (Type == 6) // Repeat block
	///       /* 0x04 */ short NumRepeat;
	///     #endif
	///   #endif
	/// }</code>
	/// For the most part, <i>Header</i> is a formality, the only way to sync it into the LFD file. Since this is simply a pre-existing file format that has been placed into the LFD, the format is very reliable and is safe to use. <i>Header</i> is also fixed. There is technically a possiblity of the <i>Version</i> and <i>Verify</i> values being different, but TIE sticks with the older version, so we don't have to worry about it.<br/><br/>
	/// -- VocDataBlock --<br/><br/>
	/// The <i>Blocks</i> array typically consists of a single Sound Data block followed by the EOF block (<c>Type == 0x00</c>), although there are cases where a resource contains two Sound Data blocks. There are a handful of resources that also use the Repeat block; they all have a NumRepeat value of <b>0xFFFF</b> (-1, infinite loop). These resources also contain the End Repeat block (<c>Type == 0x07</c>) after the Sound Data block before EOF. The EOF and End Repeat blocks do not contain any values aside from <i>Type</i>. There are other <i>Type</i> values for *.voc files, but TIE doesn't use them.<br/><br/>
	/// The sound block is simple. <i>FrequencyDivisor</i> has known values of <b>0xA1-0xA6</b>.  The sample rate of the sound is defined as <c>(1e6 / (256 - FrequencyDivisor))</c>, which gets us 10.526-11.111 KHz.<br/><br/>
	/// <i>Codec</i> simply tells us that the sound data is uncompressed 8-bit PCM, which is easy enough to extract and form a wave file, or if you have a VOC reader, you can just cut up the LFD and be done with it.<br/><br/>
	/// Now, the only trick to the block is the <i>Length</i> value. A <c>short</c> is 16-bit, <c>int</c> is 32-bit, well this stupid thing is 24-bit.  It is the length of the remaining values of the data block, however it's awkward to read/write to because of the stupid 3-byte length.</remarks>
	public class Blas : Resource
	{
		SoundDataBlock[] _soundBlocks = new SoundDataBlock[2];
		int _frequency = 12000;
		const int _vocHeaderLength = 0x1A;

		#region constructors
		/// <summary>Creates a blank resource</summary>
		public Blas()
		{
			_type = ResourceType.Blas;
			_soundBlocks[0].DoesRepeat = false;
			_soundBlocks[1].DoesRepeat = false;
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Blas(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Blas(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}
		#endregion constructors
		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <i>raw</i> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Blas"/> or <see cref="ResourceType.Voic"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = _vocHeaderLength;
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Blas || _type != ResourceType.Voic) throw new ArgumentException("Raw header is not for a Blas or Voic resource");
			_soundBlocks = new SoundDataBlock[2];	// maximum number of Sound blocks observed in TIE
			for (int i = 0; i < _soundBlocks.Length; i++)
			{
				if (_rawData[offset] == 6)
				{
					//System.Diagnostics.Debug.WriteLine("repeat block");
					_soundBlocks[i].NumberOfRepeats = BitConverter.ToInt16(_rawData, offset + 1);
					offset += 6;
				}
				else _soundBlocks[i].NumberOfRepeats = -2;
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

		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
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
		#endregion public methods
		
		#region public properties
		/// <summary>Frequency in Hertz of the audio data.</summary>
		/// <remarks><i>value</i> must be <b>10-12 kHz</b> (10000-12000). Defaults to <b>12000</b></remarks>
		/// <exception cref="ArgumentOutOfRangeException"><i>value</i> is outside the required range</exception>
		public int Frequency
		{
			get { return _frequency; }
			set
			{
				if (value < 10000 || value > 12000) throw new ArgumentOutOfRangeException("value must be 10-12 kHz");
				_frequency = value;
			}
		}

		/// <summary>Gets the audio data array</summary>
		/// <remarks>Array length is always 2 as that's the maximum seen in TIE, although more should be possible.</remarks>
		public SoundDataBlock[] SoundBlocks { get { return _soundBlocks; } }
		#endregion public properties
		/// <summary>Container for audio data and repeat information</summary>
		public struct SoundDataBlock
		{
			// FrequencyDivisor is left global, Codec is ignored and always 00
			byte[] _data;

			/// <summary>Gets or sets the raw 8-bit PCM audio data</summary>
			/// <remarks><i>value</i>.Length is restricted to <b>0xFFFFFD</b></remarks>
			/// <exception cref="ArgumentException"><i>value</i> length exceeds limits</exception>
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
			/// <remarks>Values <b>-1</b> (infinite repeat) and higher activate repeat flag, <b>-2</b> and lower deactivate</remarks>
			public short NumberOfRepeats { get; set; }

			/// <summary>Gets or sets the repeat flag</summary>
			public bool DoesRepeat
			{
				get { return (NumberOfRepeats > -2); }
				set { if (!value) NumberOfRepeats = -2; }
			}
		}
	}
}
