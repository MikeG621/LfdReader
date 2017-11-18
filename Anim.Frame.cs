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
using System.Drawing.Imaging;
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Anim : Resource
	{
		/// <summary>Object for the individual images in the resource</summary>
		/// <remarks>Each Frame is practically an individual <see cref="Delt"/> resource with the added consideration that is must lie within the boundaries set by the parent Anim resource</remarks>
		public class Frame
		{
			internal Anim _parent;
			internal Delt _delt = new Delt();
			
			/// <summary>Blank constructor</summary>
			/// <param name="parent">Parent Anim resource</param>
			internal Frame(Anim parent)
			{
				_parent = parent;
			}
			
			/// <summary>Gets or sets the global left position of the frame</summary>
			/// <exception cref="BoundaryException"><i>Left</i> results in portions of the image being outside the acceptable boundaries</exception>
			/// <remarks>If <see cref="HasFixedDimensions"/> is <b>false</b>, the parent dimensions will adjust as necessary.<br/>
			/// Otherwise, out-of-bounds content results in an exception</remarks>
			public short Left
			{
				get { return _delt.Left; }
				set
				{
					if (_parent.HasFixedDimensions && (value < _parent._left || value + Width > _parent._left + _parent._width))
						throw new BoundaryException("Left", _parent._left + "-" + (_parent._left + _parent._width - Width));
					_delt.Left = value;
					_parent._recalculateDimensions();
                    _parent._isModifed = true;
				}
			}
			
			/// <summary>Gets or sets the global top position of the frame</summary>
			/// <exception cref="BoundaryException"><i>Top</i> results in portions of the image being outside the acceptable boundaries</exception>
			/// <remarks>If <see cref="HasFixedDimensions"/> is <b>false</b>, the parent dimensions will adjust as necessary.<br/>
			/// Otherwise, out-of-bounds content results in an exception</remarks>
			public short Top
			{
				get { return _delt.Top; }
				set
				{
					if (_parent.HasFixedDimensions && (value < _parent._top || value + Height > _parent._top + _parent._height))
						throw new BoundaryException("Top", _parent._top + "-" + (_parent._top + _parent._height - Height));
					_delt.Top = value;
					_parent._recalculateDimensions();
                    _parent._isModifed = true;
				}
			}
			
			/// <summary>Gets the width of the frame</summary>
			public short Width { get { return _delt.Width; } }
			
			/// <summary>Gets the height of the frame</summary>
			public short Height { get { return _delt.Height; } }
			
			/// <summary>Gets or sets the frame image</summary>
			/// <remarks><see cref="Delt.ErrorImage"/> is returned if there's an error retrieving <i>Image</i>.<br/><br/>
			/// When setting the image, if <see cref="HasFixedDimensions"/> is <b>false</b>, the parent dimensions will adjust as necessary. Otherwise, out-of-bounds content results in an exception.<br/><br/>
			/// <i>Image</i> is converted to <see cref="PixelFormat.Format8bppIndexed"/> using the current <see cref="Anim"/> palette if it exists. If the palette is undefined, loading a <see cref="PixelFormat.Format8bppIndexed"/> image will set the palette for the <see cref="Anim"/>.</remarks>
			/// <exception cref="InvalidOperationException"><i>Image</i> is not <see cref="PixelFormat.Format8bppIndexed"/> and the parent <see cref="Anim"/> does not have a defined palette</exception>
			/// <exception cref="BoundaryException"><i>Image.Size</i> results in portions of the image being outside the acceptable boundaries</exception>
			public Bitmap Image
			{
				get
				{
					try
					{
						if (!_parent.RelativePosition) return _delt.Image;
						else
						{
							int localLeft = Left - _parent.Left;
							int localTop = Top - _parent.Top;
							Bitmap fullImage = new Bitmap(_parent.Width, _parent.Height, PixelFormat.Format8bppIndexed);
							Graphics g = Graphics.FromImage(fullImage);
							g.DrawImage(_delt.Image, new Point(localLeft, localTop));
							g.Dispose();
							if (_parent.HasDefinedPalette) fullImage.Palette = _parent._palette;
							return fullImage;
						}
					}
					catch { return Delt.ErrorImage; }
				}
				set
				{
					if (!_parent.HasDefinedPalette)
					{
						if (value.PixelFormat == PixelFormat.Format8bppIndexed) _parent._palette = value.Palette;
						else throw new InvalidOperationException("Image is not Format8bppIndexed and parent Anim does not contain a defined Palette");
					}
					if (_parent.HasFixedDimensions)
					{
						if (Left + value.Width > _parent.Left + _parent.Width || Top + value.Height > _parent.Top + _parent.Height)
							throw new BoundaryException("Image.Size", (_parent.Left + _parent.Width - Left) + "x" + (_parent.Top + _parent.Height - Top) + " max");
					}
					_delt.Image = GraphicsFunctions.ConvertTo8bpp(value, _parent._palette);
					_parent._recalculateDimensions();	// even if HasFixedDimensions, Anim can shrink
				}
			}
			
			/// <summary>Gets or sets the global position of the frame</summary>
			/// <exception cref="BoundaryException"><i>Position</i> results in portions of the image being outside the acceptable boundaries</exception>
			/// <remarks>If <see cref="HasFixedDimensions"/> is <b>false</b>, the parent dimensions will adjust as necessary.<br/>
			/// Otherwise, out-of-bounds content results in an exception.</remarks>
			public Point Position
			{
				get { return new Point(Left, Top); }
				set { Left = (short)value.X; Top = (short)value.Y; }
			}
		}
	}
}
