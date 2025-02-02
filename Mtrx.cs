/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2025 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.4
 */

/* CHANGE LOG
 * v2.4, 250202
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
	///   /* 0x00 */ short NumFrames;	// 384 for Training, otherwise 256
	///   /* 0x02 */ short Unk2;	// Appears to be always 1
	///   /* 0x04 */ short NumObjects;	// 1 for training, otherwise 3
	///   /* 0x06 */ Frame[NumFrames]; Frames
	/// }
	/// 
	/// struct Frame
	/// {
	///   /* 0x00 */ Vector CameraPosition?;	// x/y/z coords
	///   /* 0x06 */ Vector	CameraRotation?;	// Yaw/Pitch/Roll
	///   /* 0x0C */ Vector[Unk2]	Unk;	// doesn't appear to actually be used
	///   /* 0x12 */ Transform[NumObjects]	TransformData;
	/// }
	///
	/// struct Transform (size 0x18)
	/// {
	///   /* 0x00 */ short[9]	RotationMatrix;
	///   /* 0x12 */ Vector	Position;	// x/y/z
	/// }
	///
	/// struct Vector (size 0x06)
	/// {
	///   /* 0x00 */ short XorYaw;
	///   /* 0x02 */ short YorPitch;
	///   /* 0x04 */ short ZorRoll;
	/// }</code>
	/// <para>Matrix animates at 12 frames per second, so "trnfly1" is approximately 32 seconds, and the "cmbtfly*" resources are about 21 seconds. <see cref="Unk2"/> appears to always be <b>1</b>. <see cref="NumObjects"/> is the number of items being tracked by the matrix. In Training, it's a single track segment. For the combat chamber, it's a flight of 3 craft.</para>
	/// <para>The first vectors in the Frame I believe are the camera positioning. The <see cref="Frame.Unk"/> vector doesn't appear to ever be used, and if for some reason <i>Unk2</i> isn't <b>1</b>, TIE only keeps the last entry of the array anyway.</para>
	/// <para>The <i>RotationMatrix</i> is a 3x3 transform matrix that's applied to the <i>Position</i> of the object.  Still sorting that out, as the the engine is using multiple coordinate systems which affects how the transform is applied.</para></example>
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
			NumFrames = BitConverter.ToInt16(_rawData, 0);
			Unk2 = BitConverter.ToInt16(_rawData, 2);
			NumObjects = BitConverter.ToInt16(_rawData, 4);
			Data = new byte[Length - 6];
			ArrayFunctions.TrimArray(_rawData, 6, Data);
		}
		#endregion

		/// <summary>Gets the number of frames stored in the resource.</summary>
		public short NumFrames { get; internal set; }
		/// <summary>Unknown</summary>
		public short Unk2 { get; internal set; }
		/// <summary>Gets the number of craft the frame data is written for.</summary>
		public short NumObjects { get; internal set; }
		/// <summary>Gets the raw frame data.</summary>
		/// <remarks>Not fully broken out yet, see source for full details.<br/>
		/// **WILL BE DEPRECATED LATER**</remarks>
		public byte[] Data { get; internal set; }

		/// <summary>Gets the number of seconds stored in the resource.</summary>
		/// <remarks>Value is <see cref="NumFrames"/> / 12.</remarks>
		public float NumSeconds => (float)Math.Round((double)NumFrames / 12, 2);
	}
}