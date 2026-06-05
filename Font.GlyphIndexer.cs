/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2+
 */

/* CHANGE LOG
 * [UPD] Removed Indexer inheritance, implemented directly
 * v1.2, 160712
 * [ADD] _isModified edits
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using Idmr.Common;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Idmr.LfdReader
{
    public partial class Font : Resource
	{
		/// <summary>Object to provide array access to the character glyphs.</summary>
		public class GlyphIndexer
		{
			readonly Font _parent;
			readonly List<Bitmap> _items;
			
			/// <summary>Initializes the indexer.</summary>
			/// <param name="parent">The parent resource.</param>
			internal GlyphIndexer(Font parent)
			{
				_parent = parent;
				_items = parent._glyphs;
			}
			
			/// <summary>Gets or sets the individual images.</summary>
			/// <param name="index">Array index.</param>
			/// <returns><see cref="System.Drawing.Imaging.PixelFormat.Format1bppIndexed"/> Bitmap.</returns>
			/// <exception cref="ArgumentException">Image Height is incorrect.</exception>
			/// <exception cref="BoundaryException">Maximum character width is exceeded.</exception>
			/// <exception cref="IndexOutOfRangeException">Invalid <paramref name="index"/> value.</exception>
			public Bitmap this[int index]
			{
				get => _items[index];
				set
				{
					if (value.Height != _parent._height) throw new ArgumentException($"New image not required height ({_parent._height}px)", "value");
					if (value.Width > _parent._bitsPerScanLine) throw new BoundaryException("value.Width", _parent._bitsPerScanLine.ToString());
					_items[index] = GraphicsFunctions.ConvertTo1bpp(value);
					_parent._isModified = true;
				}
			}
		}
	}
}
