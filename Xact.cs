/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2016 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2
 */

/* CHANGE LOG
 * v1.2, 160712
 * [ADD] _isModified edits
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.Drawing;
using System.IO;
using Idmr.Common;
using Idmr.ImageFormat.Act;

namespace Idmr.LfdReader
{
	/// <summary>Object for "XACT" backdrop resources</summary>
	/// <remarks>The Xact resource is deprecated in favor of separate *.ACT files.<br/>
	/// For detailed ACT information refer to Idmr.ImageFormat.Act.chm.<br/><br/>
	/// <b>*** Requires Idmr.ImageFormat.Act.dll v2.0 or later. ***</b></remarks>
	public class Xact : Resource
	{
		ActImage _act;
		
		#region constructors
		/// <summary>Blank constructor</summary>
		public Xact()
		{
			_type = ResourceType.Xact;
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Xact(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Xact(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		#endregion

		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <i>raw</i> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Xact"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Xact) throw new ArgumentException("Raw header is not for a Xact resource");
			_act = new ActImage(_rawData);
		}

		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
		public override void EncodeResource()
		{
			string tempActFile = _fileName + ".act";
			_act.Save(tempActFile);
			FileStream stream = File.OpenRead(tempActFile);
			_rawData = new BinaryReader(stream).ReadBytes((int)stream.Length);
			stream.Close();
			File.Delete(tempActFile);
		}
		#endregion public methods
		
		#region public properties
		// Provides pass-through access to necessary properties
		
		/// <summary>Gets or sets the collection of <see cref="Frames">Frames</see> contained within the Act</summary>
		public FrameCollection Frames
		{
			get { return _act.Frames; }
			set
            {
                _act.Frames = value;
                _isModifed = true;
            }
		}
		
		/// <summary>Gets or sets the pixel location used to "pin" the object in-game</summary>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> does not fall within <see cref="Size"/></exception>
		/// <remarks><see cref="Frame.Location"/> values will update as necessary</remarks>
		public Point Center
		{
			get { return _act.Center; }
			set
            {
                _act.Center = value;
                _isModifed = true;
            }
		}
		
		/// <summary>Gets the overall height of the object</summary>
		public int Height { get { return _act.Height; } }
		
		/// <summary>Gets the number of images contained within the resource</summary>
		public int NumberOfFrames { get { return _act.NumberOfFrames; } }
		
		/// <summary>Gets the overall size of the object</summary>
		public Size Size { get { return _act.Size; } }
		
		/// <summary>Gets the overall width of the object</summary>
		public int Width { get { return _act.Width; } }
		#endregion public properties
	}
}
