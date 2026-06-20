/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.1+
 */

/* CHANGE LOG
 * [NEW] Created
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Idmr.LfdReader
{
	public partial class Pltt
	{
		/// <summary>Object to provide array access to the Pltt's Rotators.</summary>
		public class RotatorIndexer : IEnumerable<Rotator>
		{
			readonly Pltt _parent;
			readonly List<Rotator> _items;

			/// <summary>Initializes the indexer.</summary>
			/// <param name="parent">The parent resource.</param>
			internal RotatorIndexer(Pltt parent)
			{
				_parent = parent;
				_items = _parent._rotators;
			}

			/// <summary>Gets or sets the individual Rotator.</summary>
			/// <param name="index">Array index.</param>
			/// <returns>Indicated Rotator.</returns>
			/// <exception cref="ArgumentOutOfRangeException">Invalid <paramref name="index"/> value.</exception>
			public Rotator this[int index]
			{
				get => _items[index];
				set
				{
					_items[index] = value;
					_parent.Dirty();
				}
			}

			#region IEnumerable members
			public IEnumerator<Rotator> GetEnumerator()
			{
				return ((IEnumerable<Rotator>)_items).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return ((IEnumerable)_items).GetEnumerator();
			}
			#endregion
		}
	}
}
