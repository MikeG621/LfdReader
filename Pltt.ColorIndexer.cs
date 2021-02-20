/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2021 Michael Gaisser (mjgaisser@gmail.com)
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
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Pltt : Resource
	{
		/// <summary>Object to provide array access to individual colors in the Pltt.</summary>
		public class ColorIndexer : Indexer<Color>
		{
			readonly Pltt _parent;
			
			/// <summary>Initializes the indexer.</summary>
			/// <param name="parent">The parent resource.</param>
			internal ColorIndexer(Pltt parent)
			{
				_parent = parent;
				_items = _parent._entries;
			}

			/// <summary>Gets or sets the individual colors.</summary>
			/// <param name="index">Array index.</param>
			/// <returns>Indicated color.</returns>
			/// <exception cref="ArgumentOutOfRangeException">Invalid <paramref name="index"/> value.</exception>
			/// <remarks>Valid <paramref name="index"/> values are determined by the parent <see cref="StartIndex"/> and <see cref="EndIndex"/> properties.</remarks>
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
