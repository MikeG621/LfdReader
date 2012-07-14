/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.0
 */

/* CHANGELOG
 * 120423 - split out from Font.cs, add Boundary/IndexOutOfRange exceptions
 * 120426 - rem _updateWidth(), replaced fields with _parent
 * 120524 - ctor to internal
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Font : Resource
	{
		/// <summary>Object to provide array access to the character glyphs</summary>
		public class GlyphIndexer : Indexer<Bitmap>
		{
			Font _parent;
			
			/// <summary>Initializes the indexer</summary>
			/// <param name="parent">The parent resource</param>
			internal GlyphIndexer(Font parent)
			{
				_parent = parent;
				_items = parent._glyphs;
			}
			
			/// <summary>Gets or sets the individual images</summary>
			/// <param name="index">Array index</param>
			/// <returns><see cref="PixelFormat.Format1bppIndexed"/> Bitmap</returns>
			/// <exception cref="ArgumentException">Image Height is incorrect</exception>
			/// <exception cref="Idmr.Common.BoundaryException">Maximum character width is exceeded</exception>
			/// <exception cref="IndexOutOfRangeExecption">Invalid <i>index</i> value</exception>
			public override Bitmap this[int index]
			{
				get { return _items[index]; }
				set
				{
					if (value.Height != _parent._height) throw new ArgumentException("New image not required height (" + _parent._height + "px)", "value");
					if (value.Width > _parent._bitsPerScanLine) throw new BoundaryException("value.Width", _parent._bitsPerScanLine.ToString());
					_items[index] = GraphicsFunctions.ConvertTo1bpp(value);
				}
			}
		}
	}
}
