/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2020 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2.1+
 */

/* CHANGE LOG
 * [UPD] _read() renamed to read()
 * [ADD] Duration
 * [ADD] GetWaveBytes()
 * v1.2.1, 190802
 * [FIX] Type comparison in Decode was OR and could crash [#1]
 * v1.2, 160712
 * [ADD] _isModified edits. Not fully implemented here, as you can mess with the data still
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
	/// <remarks>The Blas resource is used for both the <see cref="Resource.ResourceType.Blas"/> and <see cref="Resource.ResourceType.Voic"/> types, as they are in fact the same resource. The only difference is the usage of the resources within the program. Blas resources tend to be flight engine sound effects such as engines, weapons, comms, etc. Voic resources are voices or sounds primarily for cutscene use. The resources themselves are wrappers for Creative Voice Files (*.voc). Although the format supports other methods, it is assumed that the audio data is always uncompressed 8-bit PCM.<hr/>
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
	/// The <i>Blocks</i> array typically consists of a single Sound Data block followed by the EOF block (<c>Type == 0x00</c>), although there are cases where a resource contains two Sound Data blocks.<br/>
	/// There are a handful of resources that also use the Repeat block; a NumRepeat value of <b>0xFFFF</b> (-1) is for an infinite loop, otherwise it's a zero-indexed count, so a value of <b>0</b> means 1 repeat in addition to the original, so it plays twice. These resources also contain the End Repeat block (<c>Type == 0x07</c>) after the Sound Data block before EOF. The EOF and End Repeat blocks do not contain any values aside from <i>Type</i>. There are other <i>Type</i> values for *.voc files, but TIE doesn't use them.<br/><br/>
	/// The sound block is simple. <i>FrequencyDivisor</i> has known values of <b>0xA1-0xA6</b>.  The sample rate of the sound is defined as <c>(1e6 / (256 - FrequencyDivisor))</c>, which gets us 10.526-11.111 KHz.<br/><br/>
	/// <i>Codec</i> simply tells us that the sound data is uncompressed 8-bit PCM, which is easy enough to extract and form a wave file, or if you have a VOC reader, you can just cut up the LFD and be done with it.<br/><br/>
	/// Now, the only trick to the block is the <i>Length</i> value. A <c>short</c> is 16-bit, <c>int</c> is 32-bit, well this stupid thing is 24-bit.  It is the length of the remaining values of the data block, however it's awkward to read/write to because of the stupid 3-byte length.</remarks>
	public class Blas : Resource
	{
		int _frequency = 12000;
		const int _vocHeaderLength = 0x1A;

		#region constructors
		/// <summary>Creates a blank resource</summary>
		public Blas()
		{
			_type = ResourceType.Blas;
			SoundBlocks[0].DoesRepeat = false;
			SoundBlocks[1].DoesRepeat = false;
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="LoadFileException">Typically due to file corruption</exception>
		public Blas(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="LoadFileException">Typically due to file corruption</exception>
		public Blas(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			read(fsLFD, filePosition);
			fsLFD.Close();
		}
		#endregion constructors

		void read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <paramref name="raw"/> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="Resource.ResourceType.Blas"/> or <see cref="Resource.ResourceType.Voic"/>.</exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = _vocHeaderLength;
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Blas && _type != ResourceType.Voic) throw new ArgumentException("Raw header is not for a Blas or Voic resource");
			SoundBlocks = new SoundDataBlock[2];	// maximum number of Sound blocks observed in TIE
			for (int i = 0; i < SoundBlocks.Length; i++)
			{
				if (_rawData[offset] == 6)
				{
					SoundBlocks[i].NumberOfRepeats = BitConverter.ToInt16(_rawData, offset + 1);
					offset += 6;
				}
				else SoundBlocks[i].NumberOfRepeats = -2;
				if (_rawData[offset] == 1)
				{
					int len = BitConverter.ToUInt16(_rawData, offset + 1) + _rawData[offset + 3] * 256 * 256; // stupid 3-byte value
					_frequency = 1000000 / (256 - _rawData[offset + 4]);
					SoundBlocks[i].Data = new byte[len - 2];
					ArrayFunctions.TrimArray(_rawData, offset + 6, SoundBlocks[i].Data);
					if (i != SoundBlocks.Length - 1) offset += len + 4 + (SoundBlocks[i].DoesRepeat ? 4 : 0);	// prep offset for next block
				}
				else if (_rawData[offset] == 0)	// i != 0
				{
					for (int j = i; j < SoundBlocks.Length; j++)
					{
						if (SoundBlocks[j].Data != null) SoundBlocks[j].Data = null;
					}
					break;
				}
			}
		}

		/// <summary>Prepares the resource for writing and updates <see cref="Resource.RawData"/></summary>
		public override void EncodeResource()
		{
			int len = _vocHeaderLength + 1;	// VocHeader + EofBlock
			foreach (SoundDataBlock sdb in SoundBlocks)
				if (sdb.Data != null) len += sdb.Data.Length + 6 + (sdb.DoesRepeat ? 0xA : 0);	// 6 = SoundBlockHeader + FreqDivisor + Codec
				else break;
			byte[] raw = new byte[len];
			ArrayFunctions.WriteToArray("Creative Voice File", raw, 0);
			raw[0x13] = 0x1A;	// char, EOF, prevents printing?
			raw[0x14] = 0x1A;	// short, HEADER_LENGTH
			ArrayFunctions.WriteToArray((short)0x10A, raw, 0x16);	// VOC_VERSION
			ArrayFunctions.WriteToArray((short)0x1129, raw, 0x18); // VERSION VERIFY
			int offset = _vocHeaderLength;
			foreach (SoundDataBlock sdb in SoundBlocks)
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

		/// <summary>Gets the raw audio data and reformats it to be ready for playback. </summary>
		/// <param name="withRepeats">Whether or not to include repeats in the audio data</param>
		/// <returns>The data in .WAV file format, with repeats if applicable.<br/>
		/// If <see cref="SoundDataBlock.NumberOfRepeats"/> is infinite and <paramref name="withRepeats"/> is <b>true</b>, will include <b>4</b> repeats.</returns>
		public byte[] GetWavBytes(bool withRepeats)
		{
			MemoryStream s = new MemoryStream();
			BinaryWriter bw = new BinaryWriter(s);
			// WAV_HEADER
			bw.Write("RIFF".ToCharArray());
			s.Position += 4;                // skip over, come back to later, P = 4  (file.length-8)
			bw.Write("WAVE".ToCharArray());
			// WAV_DATA_BLOCK
			// fmt_HEADER
			bw.Write("fmt ".ToCharArray());
			bw.Write((uint)16);             // fmt block length
											// fmt_DATA_BLOCK
			bw.Write((short)1);             // uncompressed PCM
			bw.Write((short)1);             // NumChannels (Mono)
			bw.Write((uint)Frequency);      // SampleRate
			bw.Write((uint)Frequency);      // ByteRate (SampleRate * NumChannels * BitsPerSample/8) [SR * 1 * 8/8]
			bw.Write((short)1);             // BlockAlign (NumChannels * BitsPerSample/8) [1 * 8/8]
			bw.Write((short)8);             // BitsPerSample
											// data_HEADER
			bw.Write("data".ToCharArray());
			s.Position += 4;                // skip over, come back to later, P = 40 (file.length-44);
			foreach (SoundDataBlock sdb in SoundBlocks)
				if (sdb.Data != null)
					if (sdb.DoesRepeat && withRepeats)
						for (int i = 0; i < (sdb.NumberOfRepeats != -1 ? sdb.NumberOfRepeats + 2 : 5); i++) // repeat 4 times for infinite repeats
							bw.Write(sdb.Data);
					else bw.Write(sdb.Data);
			s.SetLength(s.Position);
			s.Position = 4;
			bw.Write((uint)(s.Length - 8));
			s.Position = 40;
			bw.Write((uint)(s.Length - 44));

			byte[] data = new byte[s.Length];
			s.Position = 0;
			s.Read(data, 0, (int)s.Length);
			s.Close();

			return data;
		}
		/// <summary>Gets the raw audio data and reformats it to be ready for playback.</summary>
		/// <returns>The data in .WAV file format, with repeats if applicable. If <see cref="SoundDataBlock.NumberOfRepeats"/> is infinite, will include <b>4</b> repeats.</returns>
		public byte[] GetWavBytes()
		{
			return GetWavBytes(true);
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
                _isModifed = true;
			}
		}

		/// <summary>Gets the audio data array</summary>
		/// <remarks>Array length is always 2 as that's the maximum seen in TIE, although more should be possible.</remarks>
		public SoundDataBlock[] SoundBlocks { get; private set; } = new SoundDataBlock[2];

		/// <summary>Gets the length of time in seconds.</summary>
		/// <remarks>Value is rounded to .01 seconds.<br/>
		/// If sound data has a finite number of repeats, includes that duration</remarks>
		public decimal Duration
		{
			get
			{
				int len = 0;
				foreach (SoundDataBlock s in SoundBlocks)
					if (s.Data != null)
					{
						if (!s.DoesRepeat || s.NumberOfRepeats == -1) len += s.Data.Length;
						else len += s.Data.Length * (s.NumberOfRepeats + 1);
					}
				return Math.Round((decimal)len / Frequency, 2);
			}
		}
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
				get { return NumberOfRepeats > -2; }
				set { if (!value) NumberOfRepeats = -2; }
			}
		}
	}
}
