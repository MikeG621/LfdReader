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
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Panl : Resource
	{
		/// <summary>Object to provide array access to individual images in the Panl</summary>
		public class ImageIndexer : Indexer<Bitmap>
		{
			Panl _parent;
			
			/// <summary>Initializes the indexer</summary>
			/// <param name="parent">The parent resource</param>
			internal ImageIndexer(Panl parent)
			{
				_parent = parent;
				_items = _parent._images;
			}
			
			/// <summary>Gets or sets the individual images</summary>
			/// <param name="index">Array index</param>
			/// <returns><see cref="PixelFormat.Format8bppIndexed"/> Bitmap of the indicated image</returns>
			/// <exception cref="IndexOutOfRangeException">Invalid <i>index</i> value</exception>
			/// <exception cref="Idmr.Common.BoundaryException">Image exceeds allowable dimensions</exception>
			/// <exception cref="NullReferenceException">Images have not been initialized</exception>
			/// <remarks>If the resource was created from an LFD file, <i>index</i> is ignored.<br/>
			/// Image is converted to <see cref="PixelFormat.Format8bppIndexed"/>, must be <b>640x480</b> or smaller.</remarks>
			public override Bitmap this[int index]
			{
				get
				{
					if (!_parent._isPnl) index = 0;
					return _items[index];
				}
				set
				{
					if (!_parent._isPnl) index = 0;
					if (value.Width > Panl.MaximumWidth) throw new BoundaryException("image.Width", Panl.MaximumWidth + "px max");
					if (value.Height > Panl.MaximumHeight) throw new BoundaryException("image.Height", Panl.MaximumHeight + "px max");
					Bitmap temp = _items[index];
					try { _items[index] = GraphicsFunctions.ConvertTo8bpp(value, _parent._palette); }
					catch (Exception x) { _items[index] = temp; throw x; }
				}
			}
			
			/// <summary>Sets the image Palette</summary>
			/// <param name="palette">The Palette to be used</param>
			public void SetPalette(ColorPalette palette)
			{
				_parent._palette = palette;
				for (int i = 0; i < _items.Length; i++) _items[i].Palette = palette;
			}
		}
	}
}
