/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 0.9
 */

/* CHANGELOG
 * 110912 - add AutoSize, AutoLocate, Size
 * 110914 - Left/Top/Position exceptions
 * 110920 - SetFrame()/SetFramePosition() AutoSize/AutoLocate implementation
 * 110922 - Write(), added LoadFileException and SaveFileException throws, relativePosition re-implemented
 * 110925 - implemented Decode/EncodeResource
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets ANIM resource types.</summary>
	public class Anim : Resource
	{
		ColorPalette _palette = null;
		short _left = -1;
		short _top = -1;
		short _width = -1;
		short _height = -1;
		Frame[] _frames;

		#region constructors
		/// <summary>Create a new Anim instance from an existing opened file with default 8bpp Palette</summary>
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Create a new Anim instance from an existing opened file with the supplied Palette</summary>
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palette">The ColorPalette used for the ANIM resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(FileStream stream, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			_read(stream, filePosition);
		}
		/// <summary>Create a new Anim instance from an existing opened file with the supplied Palette array</summary>
		/// <param name="stream">The FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(FileStream stream, long filePosition, Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			_read(stream, filePosition);
			SetPalette(palettes);
		}
		/// <summary>Create a new Anim instance from an existing file with default 8bpp Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Create a new Anim instance from an existing file with the supplied Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palette">The ColorPalette used for the ANIM resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Anim(string path, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Create a new Anim instance from an exsiting file with the supplied Palette array</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
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
			try
			{
				AutoSize = false;
				AutoLocate = false;
				_process(stream, filePosition);
			}
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to create Anim information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			//System.Diagnostics.Debug.WriteLine("decoding...");
			short numberOfFrames = BitConverter.ToInt16(_rawData, 0);
			short frameLeft, frameTop, frameRight, frameBottom, animRight = -1, animBottom = -1;
			int frameLength;
			int offset = 2;
			//System.Diagnostics.Debug.WriteLine("getting sizes...");
			for (int i = 0; i < numberOfFrames; i++)
			{
				frameLength = BitConverter.ToInt32(_rawData, offset);
				frameLeft = BitConverter.ToInt16(_rawData, offset + 4);
				frameTop = BitConverter.ToInt16(_rawData, offset + 6);
				frameRight = BitConverter.ToInt16(_rawData, offset + 8);
				frameBottom = BitConverter.ToInt16(_rawData, offset + 10);
				offset += frameLength + 4;
				// done reading, process sizes
				if (i == 0)
				{
					_left = frameLeft;
					_top = frameTop;
					animRight = frameRight;
					animBottom = frameBottom;
					if (animRight == -1 && _left == -1) animRight = 0;
					if (animBottom == -1 && _top == -1) animBottom = 0;
				}
				_left = (frameLeft < _left && (animRight - _left) < Delt.MaximumWidth ? frameLeft : _left);
				_top = (frameTop < _top && (animBottom - _top) < Delt.MaximumHeight ? frameTop : _top);
				animRight = (frameRight > animRight && (frameRight - _left) <= Delt.MaximumWidth ? frameRight : animRight);
				animBottom = (frameBottom > animBottom && (frameBottom - _top) <= Delt.MaximumHeight ? frameBottom : animBottom);
			}
			_width = (short)(animRight - _left + 1);
			_height = (short)(animBottom - _top + 1);
			//System.Diagnostics.Debug.WriteLine("Anim LTWH: " + _left + ", " + _top + ", " + _width + ", " + _height);
			_frames = new Frame[numberOfFrames];
			offset = 2;
			ColorPalette defaultPalette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			for (int i = 0; i < numberOfFrames; i++)
			{
				frameLength = BitConverter.ToInt32(_rawData, offset);
				_frames[i].Left = BitConverter.ToInt16(_rawData, offset + 4);
				_frames[i].Top = BitConverter.ToInt16(_rawData, offset + 6);
				byte[] image = new byte[frameLength - 8];
				ArrayFunctions.TrimArray(_rawData, offset + 12, image);
				//System.Diagnostics.Debug.WriteLine("frame offset: " + offset);
				//System.Diagnostics.Debug.WriteLine("frame length: " + frameLength);
				_frames[i].Image =
					Delt.DecodeImage( _frames[i].Left, _frames[i].Top,
					(short)(BitConverter.ToInt16(_rawData, offset + 8) - _frames[i].Left + 1),
					(short)(BitConverter.ToInt16(_rawData, offset + 10) - _frames[i].Top + 1),
					image);
				if (_palette != null) _frames[i].Image.Palette = _palette;
				else _frames[i].Image.Palette = defaultPalette;
				offset += frameLength + 4;
			}
			//System.Diagnostics.Debug.WriteLine("... complete");
		}

		/// <summary>Prepare Anim information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			byte[][] images = new byte[NumberOfFrames][];
			int len = 2;
			for (int i = 0; i < NumberOfFrames; i++)
			{
				images[i] = Delt.EncodeImage(_frames[i].Image, _frames[i].Left, _frames[i].Top);
				len += images[i].Length + 12;
			}
			byte[] raw = new byte[len];
			int offset = 0;
			ArrayFunctions.WriteToArray(NumberOfFrames, raw, ref offset);
			for (int i = 0; i < NumberOfFrames; i++)
			{
				ArrayFunctions.WriteToArray(images[i].Length + 8, raw, ref offset);
				ArrayFunctions.WriteToArray(_frames[i].Left, raw, ref offset);
				ArrayFunctions.WriteToArray(_frames[i].Top, raw, ref offset);
				ArrayFunctions.WriteToArray((short)(_frames[i].Left + _frames[i].Image.Width - 1), raw, ref offset);
				ArrayFunctions.WriteToArray((short)(_frames[i].Top + _frames[i].Image.Height - 1), raw, ref offset);
				ArrayFunctions.WriteToArray(images[i], raw, ref offset);
			}
			_rawData = raw;

		}
		
		/// <summary>Gets the specified frame image</summary>
		/// <returns>A bitmap of the defined frame index, <i>Delt.ErrorImage</i> on error</returns>
		/// <param name="index">Zero-indexed frame number</param>
		public Bitmap GetFrame(int index)
		{
			return GetFrame(index, false);
		}
		/// <summary>Gets the specified frame image</summary>
		/// <returns>A bitmap of the defined frame index, <i>Delt.ErrorImage</i> on error</returns>
		/// <param name="index">Zero-indexed frame number</param>
		/// <param name="relativePosition">Determines if the image returned should be relative to Anim.Location</param>
		public Bitmap GetFrame(int index, bool relativePosition)
		{
			try
			{
				if (!relativePosition) return _frames[index].Image;
				else
				{
					int localLeft = _frames[index].Left - _left;
					int totalWidth = localLeft + _frames[index].Image.Width;
					int localTop = _frames[index].Top - _top;
					int totalHeight = localTop + _frames[index].Image.Height;
					Bitmap fullImage = new Bitmap(totalWidth, totalHeight);
					Graphics g = Graphics.FromImage(fullImage);
					g.DrawImage(_frames[index].Image, new Point(localLeft, localTop));
					g.Dispose();
					if (_palette != null) fullImage.Palette = _palette;
					return fullImage;
				}
			}
			catch { return Delt.ErrorImage; }
		}
		
		/// <summary>Gets the location of the frame relative to screen origin</summary>
		/// <returns>Global X/Y location of the frame</returns>
		/// <param name="index">Zero-indexed frame number</param>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public Point GetFramePosition(int index)
		{
			return _frames[index].Position;
		}
		
		/// <summary>Sets the indicated frame number to a new image</summary>
		/// <param name="index">Zero-indexed frame to be updated</param>
		/// <param name="image">Image to replace the indicated frame</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i>.Size results in portions of the image being outside the acceptable boundaries</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		/// <remarks>If AutoSize is <i>true</i>, frame will shift to prevent off-screen content and Anim dimensions will enlarge as necessary.<br>
		/// If AutoSize and AutoLocate are <i>true</i>, Anim location will also shift to prevent off-screen content.<br>
		/// If AutoLocate is <i>true</i> and AutoSize is <i>false</i>, frame will shift to prevent content outside of Anim boundaries.<br>
		/// All other scenarios result in a BoundaryException.<br><br>
		/// <i>image</i> is converted to 8bpp using the current Anim palette.</remarks>
		public void SetFrame(int index, Bitmap image)
		{
			if (AutoSize)
			{	// AutoLocate applies to frames and Anim
				if (image.Width > Delt.MaximumWidth || image.Height > Delt.MaximumHeight)
					throw new BoundaryException("image.Size", Delt.MaximumWidth + "x" + Delt.MaximumHeight);
				if (!AutoLocate && (_left + image.Width > Delt.MaximumWidth || _top + image.Height > Delt.MaximumHeight))
					throw new BoundaryException("image.Size", (Delt.MaximumWidth - _left) + "x" + (Delt.MaximumHeight - _top));
				if (_frames[index].Left + image.Width > Delt.MaximumWidth)
					_frames[index].Left = (short)(Delt.MaximumWidth - image.Width); // shift frame
				if (_frames[index].Left < _left)
					_left = _frames[index].Left;	// shift ANIM location, cannot be TRUE if !AutoLocate
				if (_frames[index].Left + image.Width > _left + _width)
					_width = (short)(_frames[index].Left + image.Width - _left);	// extend ANIM size
				if (_frames[index].Top + image.Height > Delt.MaximumHeight)
					_frames[index].Top = (short)(Delt.MaximumHeight - image.Height);	// shift frame
				if (_frames[index].Top < _top)
					_top = _frames[index].Top;	// adjust ANIM location, cannot be TRUE if !AutoLocate
				if (_frames[index].Top + image.Height > _top + _height)
					_height = (short)(_frames[index].Top + image.Height - _top);	// extend ANIM size
			}
			else if (AutoLocate)
			{	// AutoLocate only applies to frames, as moving Anim would require changing Size
				if (image.Width > _width || image.Height > _height)
					throw new BoundaryException("image.Size", _width + "x" + _height);
				if (_frames[index].Left + image.Width > _left + _width)
					_frames[index].Left = (short)(_left + _width - image.Width); // shift frame
				if (_frames[index].Top + image.Height > _top + _height)
					_frames[index].Top = (short)(_top + _height - image.Height);	// shift frame
			}
			else if (image.Width + _frames[index].Left > _left + _width || image.Height + _frames[index].Top > _top + _height)
				throw new BoundaryException("image.Size", (_left +_width - _frames[index].Left) + "x" + (_top + _height - _frames[index].Top));
			_frames[index].Image = GraphicsFunctions.ConvertTo8bpp(image, _palette);
		}
		
		/// <summary>Sets the indicated frame's global position</summary>
		/// <param name="index">Zero-indexed frame to be updated</param>
		/// <param name="left">Left position</param>
		/// <param name="top">Top position</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>left</i> or <i>top</i> result in portions of the image being outside the acceptable boundaries</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		/// <remarks>If AutoSize is <i>true</i>, Anim will englarge as necessary.<br>
		/// If AutoSize and AutoLocate are <i>true</i>, Anim location will also shift as necessary.<br>
		/// All other scenarios, including positional values that result in off-screen content results in a BoundaryException</remarks>
		public void SetFramePosition(int index, short left, short top)
		{
			if (left < 0 || top < 0) throw new BoundaryException("Values must not be negative");
			if (AutoSize)
			{
				if (_frames[index].Image.Width + left > Delt.MaximumWidth)
					throw new BoundaryException("left", (Delt.MaximumWidth - _frames[index].Image.Width) + " max");
				if (_frames[index].Image.Height + top > Delt.MaximumHeight)
					throw new BoundaryException("top", (Delt.MaximumHeight - _frames[index].Image.Height) + " max");
				if (left + _frames[index].Image.Width > _left + _width) _width = (short)(left + _frames[index].Image.Width - _left);
				if (top + _frames[index].Image.Height > _top + _height) _height = (short)(top + _frames[index].Image.Height - _top);
				if (AutoLocate)
				{
					if (left < _left)
					{
						_width = (short)(_width + _left - left);
						_left = left;
					}
					if (top < _top)
					{
						_height = (short)(_height + _top - top);
						_top = top;
					}
				}
			}
			if (left < _left || left + _frames[index].Image.Width > _left + _width)
				throw new BoundaryException("left", _left + "-" + (_left + _width - _frames[index].Image.Width));
			if (top < _top || top + _frames[index].Image.Height > _top + _height)
				throw new BoundaryException("top", _top + "-" + (_top + _height - _frames[index].Image.Height));
			_frames[index].Left = left;
			_frames[index].Top = top;
		}
		/// <summary>Sets the indicated frame's global position</summary>
		/// <param name="index">Zero-indexed frame to be updated</param>
		/// <param name="position">Frame position</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>position.X</i> or <i>position.Y</i> result in portions of the image being outside the acceptable boundaries</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public void SetFramePosition(int index, Point position)
		{
			SetFramePosition(index, (short)position.X, (short)position.Y);
		}

		/// <summary>Sets the colors used for the Anim</summary>
		/// <param name="palette">The colors to be used</param>
		public void SetPalette(ColorPalette palette)
		{
			_palette = palette;
			for (int i = 0; i < _frames.Length; i++) _frames[i].Image.Palette = _palette;
		}
		/// <summary>Sets the colors used for the Anim</summary>
		/// <param name="palettes">The colors to be used</param>
		public void SetPalette(Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			foreach (Pltt p in palettes)
				for (int i = p.StartIndex; i <= p.EndIndex; i++)
					_palette.Entries[i] = p.Entries[i - p.StartIndex].Color;
			for (int i = 0; i < _frames.Length; i++) _frames[i].Image.Palette = _palette;
		}
		#endregion public methods
		
		#region public properties
		/// <summary>Gets total number of frames within the ANIM resource</summary>
		public short NumberOfFrames { get { return (short)_frames.Length; } }
		/// <summary>Gets or sets Left screen location of Anim</summary>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> causes portion of image to be off-screen</exception>
		public short Left
		{
			get { return _left; }
			set
			{
				if (value >= Delt.MaximumWidth - _width || value < 0)
					throw new BoundaryException("value", "0-" + (Delt.MaximumWidth - _width).ToString());
				_left = value;
			}
		}
		/// <summary>Gets or sets Top screen location of Anim</summary>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> causes portion of image to be off-screen</exception>
		public short Top
		{
			get { return _top; }
			set
			{
				if (value >= Delt.MaximumHeight - _height || value < 0)
					throw new BoundaryException("value", "0-" + (Delt.MaximumHeight - _height).ToString());
				_top = value;
			}
		}
		/// <summary>Gets maximum width occupied by the image</summary>
		public short Width { get { return _width; } }
		/// <summary>Gets maximum height occupied by the image</summary>
		public short Height { get { return _height; } }
		/// <summary>Gets or sets Anim screen location</summary>
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

		/// <summary>Returns <i>true</i> if the Anim palette has been defined</summary>
		public bool HasDefinedPalette { get { return _palette != null; } }
		
		/// <summary>Redefine maximum allowable size if larger image is imported</summary>
		/// <remarks>Defaults to <i>false</i></remarks>
		public bool AutoSize { get; set; }
		
		/// <summary>Redefine Anim or Frame Positions if larger image is imported to prevent off-screen content</summary>
		/// <remarks>Defaults to <i>false</i></remarks>
		public bool AutoLocate { get; set; }
		#endregion public properties
		
		/// <summary>Represents the individual images contained in the resource</summary>
		public struct Frame
		{
			/// <summary>Gets or set the global left position of the frame</summary>
			public short Left { get; set; }
			
			/// <summary>Gets or set the global top position of the frame</summary>
			public short Top { get; set; }
			
			// Right = Width + Left - 1
			// Bottom = Top + Height - 1
			
			/// <summary>Gets or set the frame image</summary>
			public Bitmap Image { get; set; }
			
			/// <summary>Gets or set the global position of the frame</summary>
			public Point Position
			{
				get { return new Point(Left, Top); }
				set { Left = (short)value.X; Top = (short)value.Y; }
			}
		}
	}
}
