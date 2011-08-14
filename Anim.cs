/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * Full notice in Resource.cs
 */

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace Idmr.LfdReader
{
	/// <remarks>Reads LFD files and interprets ANIM resource types.</remarks>
	public class Anim : Resource
	{
		// TODO: public settings;
		//	bool Autosize: if true, increase ANIM sizes if oversized frame imported
		//	bool FixFrameLocations: if true, update frame.Locations when ANIM.Location changed
		private ColorPalette _palette;
		private short _left = -1;
		private short _top = -1;
		private short _width = -1;
		private short _height = -1;
		private Frame[] _frames;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palette">The ColorPalette used for the ANIM resource</param>
		public Anim(FileStream stream, long filePosition, ColorPalette palette)
		{
			Read(stream, filePosition, palette);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palette">The ColorPalette used for the ANIM resource</param>
		public Anim(string path, long filePosition, ColorPalette palette)
		{
			FileStream stream = File.OpenRead(path);
			Read(stream, filePosition, palette);
			stream.Close();
		}

		private void Read(FileStream stream, long filePosition, ColorPalette animPalette)
		{
			BinaryReader br = new BinaryReader(stream);
			_palette = animPalette;
			_fileName = stream.Name;	// Resource.filename
			_offset = filePosition;	// Resource.offset
			stream.Position = _offset + NameOffset;
			_name = new string(br.ReadChars(8)).Trim('\0');	// Resource.name
			_length = br.ReadInt32();	// Resource.length
			short numFrames = br.ReadInt16();
			short tx, ty, tr, tb, r=-1, b=-1;
			int tl;
			for (int i=0;i<numFrames;i++)
			{
				tl = br.ReadInt32();	// FRAME_LENGTH
				tx = br.ReadInt16();	// FRAME_LEFT
				ty = br.ReadInt16();	// FRAME_TOP
				tr = br.ReadInt16();	// FRAME_RIGHT
				tb = br.ReadInt16();	// FRAME_BOTTOM
				stream.Position += tl-8;
				if (i==0)
				{
					_left = tx;
					_top = ty;
					r = tr;
					b = tb;
					if (r==-1 && _left==-1) r=0;
					if (b==-1 && _top==-1) b=0;
				}
				// determine maximum size used
				_left = (tx<_left && (r-_left)<800 ? tx : _left);
				_top = (ty<_top && (b-_top)<600 ? ty : _top);
				r = (tr>r && (tr-_left)<=800 ? tr : r);
				b = (tb>b && (tb-_top)<=600 ? tb : b);
			}
			_width = (short)(r-_left+1);
			_height = (short)(b-_top+1);
			stream.Position = _offset + Resource.HeaderLength + 2;
			_frames = new Frame[numFrames];
			//Delt d;
			for (int i=0;i<numFrames;i++)
			{
				long p = stream.Position + 4;
				tl = br.ReadInt32();
				//_images[i] = new Delt(stream, p - Resource.HeaderLength, _palette, _left, _top).Image;
				/*d = new Delt(stream, p - Resource.HeaderLength, _palette);	// go back to normal Delt, control size here in Anim
				_frames[i].Left = d.Left;
				_frames[i].Top = d.Top;
				_frames[i].Image = d.Image;*/
				_frames[i].Left = br.ReadInt16();
				_frames[i].Top = br.ReadInt16();
				_frames[i].Image = Delt.DecodeImage(_frames[i].Left, _frames[i].Top, (short)(br.ReadInt16() - _frames[i].Left + 1), (short)(br.ReadInt16() - _frames[i].Top + 1), br.ReadBytes(tl - 8));
				_frames[i].Image.Palette = _palette;
				stream.Position = p + tl;
			}
		}

		//===================
		/// <returns>A bitmap of the defined frame index</returns>
		/// <param name="index">Zero-indexed frame number</param>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public Bitmap GetFrame(int index)
		{
			return GetFrame(index, false);
		}
		/// <returns>A bitmap of the defined frame index</returns>
		/// <param name="index">Zero-indexed frame number</param>
		/// <param name="globalPosition">(Unimplemented) Determines if the image returned should be relative to 0,0</param>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public Bitmap GetFrame(int index, bool globalPosition)
		{
			// TODO: re-implement globalPosition
			try
			{
				if (!globalPosition) return _frames[index].Image;
				else return _frames[index].Image;
			}
			catch { return Delt.ErrorImage; }
		}
		
		/// <returns>Global X/Y location of the frame</returns>
		/// <param name="index">Zero-indexed frame number</param>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public Point GetFramePosition(int index)
		{
			return new Point(_frames[index].Left, _frames[index].Top);
		}
		
		/// <remarks>Sets the indicated frame number to a new image</remarks>
		/// <param name="index">Zero-indexed frame to be updated</param>
		/// <param name="image">Image to replace the indicated frame</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i>.Size results in portions of the image being outside the animation boundary</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public void SetFrame(int index, Bitmap image)
		{
			if (image.Width > _width + _left - _frames[index].Left) throw new Idmr.Common.BoundaryException("image.Width", (_width + _left - _frames[index].Left) + "px max");
			if (image.Height > _height + _top - _frames[index].Top) throw new Idmr.Common.BoundaryException("image.Height", (_height + _top - _frames[index].Top) + "px max");
			_frames[index].Image = Idmr.Common.Graphics.ConvertTo8bpp(image, _palette);
		}
		
		/// <remarks>Sets the indicated frame's global position</remarks>
		/// <param name="index">Frame to be updated</param>
		/// <param name="left">Left position</param>
		/// <param name="top">Top position</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>left</i> or <i>top</i> result in portions of the image being outside the animation boundary</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public void SetFramePosition(int index, short left, short top)
		{
			if (left < _left || left > _left + _width - _frames[index].Image.Width) throw new Idmr.Common.BoundaryException("left", _left.ToString() + " to " + (_left + _width - _frames[index].Image.Width));
			_frames[index].Left = left;
			if (top < _top || top > _top + _height - _frames[index].Image.Height) throw new Idmr.Common.BoundaryException("top", _top.ToString() + " to " + (_top + _height - _frames[index].Image.Height));
			_frames[index].Top = top;
		}
		/// <remarks>Sets the indicated frame's global position</remarks>
		/// <param name="index">Frame to be updated</param>
		/// <param name="position">Frame position</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>position.X</i> or <i>position.Y</i> result in portions of the image being outside the animation boundary</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid frame index</exception>
		public void SetFramePosition(int index, Point position)
		{
			SetFramePosition(index, (short)position.X, (short)position.Y);
		}

		//===================
		/// <value>Gets total number of frames within the ANIM resource</value>
		public short NumberOfFrames { get { return _frames.Length; } }
		/// <value>Gets or sets Left screen location of Anim</value>
		/// <remarks>Will only update if value is 0-639</remarks>
		public short Left
		{
			get { return _left; }
			set { if (value < 640 && value >= 0) _left = value; }
		}
		/// <value>Gets or sets Top screen location of Anim</value>
		/// <remarks>Will only update if value is 0-479</remarks>
		public short Top
		{
			get { return _top; }
			set { if (value < 480 && value >= 0) _top = value; }
		}
		/// <value>Gets maximum width occupied by image</value>
		public short Width { get { return _width; } }
		/// <value>Gets maximum height occupied by image</value>
		public short Height { get { return _height; } }
		/// <value>Gets or sets Anim screen location</value>
		/// <remarks>Left will only update if X is 0-639<para>
		/// Top will only update is Y is 0-479</remarks>
		public Point Location
		{
			get { return new Point(_left, _top); }
			set
			{
				if (value.X < 640 && value.X >= 0) _left = (short)value.X;
				if (value.Y < 480 && value.Y >= 0) _top = (short)value.Y;
			}
		}
		
		/// <remarks>Represents the individual images contained in the resource</remarks>
		public struct Frame
		{
			public short Left;
			public short Top;
			// Right = Width + Left - 1
			// Bottom = Top + Height - 1
			public Bitmap Image;
			
			public Point Position
			{
				get { return new Point(Left, Top); }
				set { Left = value.X; Top = value.Y; }
			}
		}
	}
}
