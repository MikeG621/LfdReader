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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "ANIM" animation resources</summary>
	/// <remarks>The Anim resource is simply a collection of <see cref="Delt"/> resources and the box that encompasses all of them. Like the <see cref="Delt"/>, the palette is controlled by the <see cref="Film"/> that is defining the current view. The <see cref="Film"/> also controls the Anim's animation speed, direction and looping.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ short NumberOfFrames;
	///   /* 0x02 */ Frame[NumberOfFrames] Frames;
	/// }
	/// 
	/// struct Frame
	/// {
	///   /* 0x00 */ int Length;
	///   /* 0x02 */ Delt.RawData Image;
	/// }</code>
	/// The only real unique value in the Anim is the number of frames that are stored within the resource. The Frame struct is nothing more than a wrapper for the <see cref="Delt"/> resource with only the <i>Frame.Length</i> value which is just the size of <i>Frame.Image</i>.  The <see cref="Location"/> and <see cref="Size"/> properties are derived from the dimensions of the individual Frames.<br/><br/>
	/// All Frames share the same palette.</remarks>
	public partial class Anim : Resource
	{
		ColorPalette _palette = null;
		short _left = -1;
		short _top = -1;
		short _width = -1;
		short _height = -1;
		FrameCollection _frames;

		#region constructors
		/// <summary>Creates a blank resource</summary>
		public Anim()
		{
			_type = ResourceType.Anim;
			_frames = new FrameCollection(this);
		}
		/// <summary>Creates a new instance from an existing opened file with default 8bpp Palette</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing opened file with the supplied Palette</summary>
		/// <param name="stream">This opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palette">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(FileStream stream, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing opened file with the supplied Palette array</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palettes">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(FileStream stream, long filePosition, Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			_read(stream, filePosition);
			SetPalette(palettes);
		}
		/// <summary>Creates a new instance from an existing file with default 8bpp Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Creates a new instance from an existing file with the supplied Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palette">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(string path, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Creates a new instance from an exsiting file with the supplied Palette array</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palettes">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(string path, long filePosition, Pltt[] palettes)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
			SetPalette(palettes);
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
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Anim"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Anim) throw new ArgumentException("Raw header is not for an Anim resource");
			//System.Diagnostics.Debug.WriteLine("decoding...");
			short numberOfFrames = BitConverter.ToInt16(_rawData, 0);
			_frames = new FrameCollection(this);
			for (int i = 0; i < numberOfFrames; i++) _frames.Add(new Frame(this));
			int frameLength;
			int offset = 2;
			for (int i = 0; i < NumberOfFrames; i++)
			{
				frameLength = BitConverter.ToInt32(_rawData, offset);
				byte[] delt = new byte[frameLength];
				ArrayFunctions.TrimArray(_rawData, offset + 4, delt);
				//System.Diagnostics.Debug.WriteLine("Frame offset: " + offset);
				_frames[i]._delt.DecodeResource(delt, false);
				/*_frames[i]._left = BitConverter.ToInt16(_rawData, offset + 4);
				_frames[i]._top = BitConverter.ToInt16(_rawData, offset + 6);
				_frames[i]._right = BitConverter.ToInt16(_rawData, offset + 8);
				_frames[i]._bottom = BitConverter.ToInt16(_rawData, offset + 10);
				byte[] encoded = new byte[frameLength - 8];
				ArrayFunctions.TrimArray(_rawData, offset + 12, encoded);
				//System.Diagnostics.Debug.WriteLine("frame offset: " + offset);
				//System.Diagnostics.Debug.WriteLine("frame length: " + frameLength);
				_frames[i]._image = Delt.DecodeImage( _frames[i].Left, _frames[i].Top, _frames[i].Width, _frames[i].Height, encoded);*/
				if (HasDefinedPalette) _frames[i]._delt.Palette = _palette;
				offset += frameLength + 4;
			}
			_recalculateDimensions();
			//System.Diagnostics.Debug.WriteLine("Anim LTWH: " + _left + ", " + _top + ", " + _width + ", " + _height);
			//System.Diagnostics.Debug.WriteLine("... complete");
		}

		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
		public override void EncodeResource()
		{
			//byte[][] images = new byte[NumberOfFrames][];
			int len = 2;
			for (int i = 0; i < NumberOfFrames; i++)
			{
				_frames[i]._delt.EncodeResource();
				len += _frames[i]._delt.Length + 4;
				//images[i] = Delt.EncodeImage(_frames[i].Image, _frames[i].Left, _frames[i].Top);
				//len += images[i].Length + 12;
			}
			byte[] raw = new byte[len];
			int offset = 0;
			ArrayFunctions.WriteToArray(NumberOfFrames, raw, ref offset);
			for (int i = 0; i < NumberOfFrames; i++)
			{
				ArrayFunctions.WriteToArray(_frames[i]._delt.Length + 4, raw, ref offset);
				ArrayFunctions.WriteToArray(_frames[i]._delt.RawData, raw, ref offset);
				/*ArrayFunctions.WriteToArray(images[i].Length + 8, raw, ref offset);
				ArrayFunctions.WriteToArray(_frames[i].Left, raw, ref offset);
				ArrayFunctions.WriteToArray(_frames[i].Top, raw, ref offset);
				ArrayFunctions.WriteToArray(_frames[i]._delt._right, raw, ref offset);
				ArrayFunctions.WriteToArray(_frames[i]._delt._bottom, raw, ref offset);
				ArrayFunctions.WriteToArray(images[i], raw, ref offset);*/
			}
			_rawData = raw;

		}
		
		/// <summary>Sets the colors used for the Anim</summary>
		/// <param name="palette">The colors to be used</param>
		/// <remarks>All <see cref="Frame.Image">Images</see> are updated</remarks>
		public void SetPalette(ColorPalette palette)
		{
			_palette = palette;
			for (int i = 0; i < _frames.Count; i++) _frames[i]._delt.Palette = _palette;
		}
		/// <summary>Sets the colors used for the Anim</summary>
		/// <param name="palettes">The colors to be used</param>
		/// <remarks>All <see cref="Frame.Image">Images</see> are updated</remarks>
		public void SetPalette(Pltt[] palettes) { SetPalette(Pltt.ConvertToPalette(palettes)); }
		#endregion public methods
		
		#region public properties
		/// <summary>Determines if <see cref="Frame.Image">Images</see> should be returned sized relative to <see cref="Location"/></summary>
		public bool RelativePosition { get; set; }
		
		/// <summary>Gets total number of frames within the resource</summary>
		public short NumberOfFrames { get { return (short)_frames.Count; } }
		
		/// <summary>Gets or sets the Left screen location of the resource</summary>
		/// <remarks>Each <see cref="Frame"/> will update its <see cref="Frame.Position"/> to maintain it's relative distance to <see cref="Location"/>.</remarks>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> causes portion of image to be off-screen</exception>
		public short Left
		{
			get { return _left; }
			set
			{
				if (value >= Delt.MaximumWidth - _width || value < 0)
					throw new BoundaryException("value", "0-" + (Delt.MaximumWidth - _width));
				short diff = (short)(value - _left);
				for (int f = 0; f < NumberOfFrames; f++) _frames[f].Left += diff;
				_left = value;
			}
		}
		/// <summary>Gets or sets the Top screen location of the resource</summary>
		/// <remarks>Each <see cref="Frame"/> will update its <see cref="Frame.Position"/> to maintain it's relative distance to <see cref="Location"/>.</remarks>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> causes portion of image to be off-screen</exception>
		public short Top
		{
			get { return _top; }
			set
			{
				if (value >= Delt.MaximumHeight - _height || value < 0)
					throw new BoundaryException("value", "0-" + (Delt.MaximumHeight - _height));
				short diff = (short)(value - _top);
				for (int f = 0; f < NumberOfFrames; f++) _frames[f].Top += diff;
				_top = value;
			}
		}
		/// <summary>Gets the maximum width occupied by the resource</summary>
		public short Width { get { return _width; } }
		/// <summary>Gets the maximum height occupied by the resource</summary>
		public short Height { get { return _height; } }
		/// <summary>Gets or sets the resource screen location</summary>
		/// <remarks>Each <see cref="Frame"/> will update its <see cref="Frame.Position"/> to maintain it's relative distance to <see cref="Location"/>.</remarks>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> causes portion of image to be off-screen</exception>
		public Point Location
		{
			get { return new Point(_left, _top); }
			set
			{
				try
				{
					Left = (short)value.X;
					Top = (short)value.Y;
				}
				catch (BoundaryException x)
				{ throw new BoundaryException("value", "0,0 - " + (Delt.MaximumWidth - _width) + "," + (Delt.MaximumHeight - _height), x); }
			}
		}
		/// <summary>Gets the maximum size occupied by the image</summary>
		public Size Size { get { return new Size(_width, _height); } }

		/// <summary>Gets if the Anim palette has been defined</summary>
		public bool HasDefinedPalette { get { return _palette != null; } }
		
		/// <summary>When <b>true</b>, locks the overall Anim boundaries</summary>
		/// <remarks>When fixed, <see cref="Frame.Image"/> and <see cref="Frame.Location"/> cannot be edited in a manner that would result in portions of the <see cref="Frame"/> residing outside the original boundaries of the Anim.<br/>
		/// Defaults to <b>false</b>.</remarks>
		public bool HasFixedDimensions { get; set; }

		/// <summary>Gets the collection of images</summary>
		public FrameCollection Frames { get { return _frames; } }
		#endregion public properties
		
		internal void _recalculateDimensions()
		{
			short left = Delt.MaximumWidth, right = -1, top = Delt.MaximumHeight, bottom = -1;
			for (int f = 0; f < NumberOfFrames; f++)
			{
				left = (_frames[f].Left < left ? _frames[f].Left : left);
				top = (_frames[f].Top < top ? _frames[f].Top : top);
				short frameRight = (short)(_frames[f].Left + _frames[f].Width - 1);
				right = (frameRight > right ? frameRight : right);
				short frameBottom = (short)(_frames[f].Top + _frames[f].Height - 1);
				bottom = (frameBottom > bottom ? frameBottom : bottom);
			}
			_left = left;
			_top = top;
			_width = (short)(right - left + 1);
			_height = (short)(bottom - top + 1);
		}
	}
}
