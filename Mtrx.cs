/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2023 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.3+
 */

/* CHANGE LOG
 * [NEW] Created
 */

using Idmr.Common;
using System;
using System.IO;

namespace Idmr.LfdReader
{
	/// <summary>Object for "MTRX" OPT transforms.</summary>
	/// <remarks>The Matrix resource contains the replay information for the orientation of a craft OPT when not in-flight, specifically the main screen demo in the Training and Combat chambers.</remarks>
	/// <example><h4>Raw Data definition</h4>
	/// <code>
	/// RawData
	/// {
	///   /* 0x00 */ short Unk1;	// 384 for Training, otherwise 256
	///   /* 0x02 */ short Unk2;	// Appears to be always 1
	///   /* 0x04 */ short Unk3;	// 1 for training, otherwise 3
	///   /* 0x06 */ byte[] data;	// don't know how this breaks down yet
	/// }</code>
	/// <para>The length of data = Unk1 * (Unk2 + 2) * 6 + Unk3 * 24, or = 6 * Unk1 * (2 + Unk2 + Unk3 * 4)).</para>
	/// <para>Not much to speak of as of yet.</para></example>
	public class Mtrx : Resource
	{
		#region constructors
		/// <summary>Blank constructor.</summary>
		public Mtrx()
		{
			_type = ResourceType.Mtrx;
		}
		/// <summary>Creates a new instance from an existing opened file.</summary>
		/// <param name="stream">The opened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Mtrx(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file.</summary>
		/// <param name="path">The full path to the unopened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Mtrx(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			read(stream, filePosition);
			stream.Close();
		}
		#endregion

		void read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource.</summary>
		/// <param name="raw">Raw byte data.</param>
		/// <param name="containsHeader">Whether or not <paramref name="raw"/> contains the resource Header information.</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="Resource.ResourceType.Mtrx"/>.</exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Mtrx) throw new ArgumentException("Raw header is not for a Mtrx resource");
			Unk1 = BitConverter.ToInt16(_rawData, 0);
			Unk2 = BitConverter.ToInt16(_rawData, 2);
			Unk3 = BitConverter.ToInt16(_rawData, 4);
			Data = new byte[Length - 6];
			ArrayFunctions.TrimArray(_rawData, 6, Data);
		}
		#endregion

		public short Unk1 { get; internal set; }
		public short Unk2 { get; internal set; }
		public short Unk3 { get; internal set; }
		public byte[] Data { get; internal set; }
	}
}
