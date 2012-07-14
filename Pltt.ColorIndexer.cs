/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.0
 */

/* CHANGELOG
 * 120426 - create
 * 120524 - ctor to internal
 */

using System;
using System.Drawing;
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Pltt : Resource
	{
		/// <summary>Object to provide array access to individual colors in the Pltt</summary>
		public class ColorIndexer : Indexer<Color>
		{
			Pltt _parent;
			
			/// <summary>Initializes the indexer</summary>
			/// <param name="parent">The parent resource</param>
			internal ColorIndexer(Pltt parent)
			{
				_parent = parent;
				_items = _parent._entries;
			}
			
			/// <summary>Gets or sets the individual colors</summary>
			/// <param name="index">Array index</param>
			/// <returns>Indicated color</returns>
			/// <exception cref="ArgumentOutOfRangeException">Invalid <i>index</i> value</exception>
			/// <remarks>Valid <i>index</i> values are determined by the parent <see cref="Pltt.StartIndex"/> and <see cref="Pltt.EndIndex"/> properties.</remarks>
			public override Color this[int index]
			{
				get
				{
					if (index > _parent.EndIndex || index < _parent.StartIndex)
						throw new ArgumentOutOfRangeException("Index must be " + _parent.StartIndex + "-" + _parent.EndIndex);
					return _items[index];
				}
				set
				{
					if (index > _parent.EndIndex || index < _parent.StartIndex)
						throw new ArgumentOutOfRangeException("Index must be " + _parent.StartIndex + "-" + _parent.EndIndex);
					_items[index] = value;
				}
			}
		}
	}
}
