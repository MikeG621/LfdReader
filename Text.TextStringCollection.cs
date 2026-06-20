/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2+
 */

/* CHANGE LOG
 * [NEW] created
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Idmr.LfdReader
{
	public partial class Text
	{
		/// <summary>Object to maintain Text strings.</summary>
		public class TextStringCollection : IEnumerable<TextString>
		{
			readonly Text _parent;
			readonly List<TextString> _items = new List<TextString>();

			/// <summary>Creates an empty collection.</summary>
			/// <param name="parent">The parent Text resource.</param>
			internal TextStringCollection(Text parent) => _parent = parent;
			/// <summary>Create a collection with a starting number of strings.</summary>
			/// <param name="parent">The parent Text resource.</param>
			/// <param name="count">The number of strings.</param>
			/// <remarks>All strings initialize empty.</remarks>
			internal TextStringCollection(Text parent, int count) : this(parent)
			{
				for (int i = 0; i < count; i++) _items.Add(new TextString(_parent));
			}

			/// <summary>Gets the accessor for the string at the specified index.</summary>
			/// <param name="index">Array index.</param>
			/// <returns>The string at the specified index.</returns>
			/// <exception cref="ArgumentOutOfRangeException">Invalid value of <paramref name="index"/>.</exception>
			public TextString this[int index] => _items[index];

			/// <summary>Sets the number strings in the resource.</summary>
			/// <remarks><see cref="Strings"/> expands and contracts as needed. If <paramref name="count"/> is less than <see cref="NumberOfStrings"/>, <see cref="Strings"/> will truncate with data loss.</remarks>
			/// <param name="count"></param>
			internal void setCount(int count)
			{
				if (count > _items.Count)
					for (int i = _items.Count; i < count; i++) _items.Add(new TextString(_parent));
				else if (count < _items.Count)
					for (int i = _items.Count; i > count; i--) _items.RemoveAt(_items.Count - 1);
			}

			/// <summary>Removes all strings from the collection.</summary>
			public void Clear() => _items.Clear();

			/// <summary>Gets the number of strings in the collection.</summary>
			public int Count => _items.Count;

			#region IEnumerable members
			public IEnumerator<TextString> GetEnumerator()
			{
				return ((IEnumerable<TextString>)_items).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return ((IEnumerable)_items).GetEnumerator();
			}
			#endregion
		}
	}
}
