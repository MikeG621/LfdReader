/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.5+
 */

/* CHANGE LOG
 * [UPD] Collection and IEnumerable implemented directly
 * [UPD] Remove marked OBS in favor of new RemoveAt
 * v2.5, 260214
 * [UPD] ctor now takes numFrames, allowing high-frames like PLAYER:ANIMicons*
 * v1.1, 141215
 * [UPD] changed license to MPL
 * [NEW] implemented SetCount
 * v1.0
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Anim : Resource
	{
		/// <summary>Object to maintain Anim image <see cref="Frame">Frames</see>.</summary>
		public class FrameCollection : IEnumerable<Frame>
		{
			internal Anim _parent;
			readonly List<Frame> _items;

			#region constructors
			/// <summary>Creates an empty Collection.</summary>
			internal FrameCollection(Anim parent, short numFrames)
			{
				_parent = parent;
				ItemLimit = numFrames;
				if (ItemLimit == -1) _items = new List<Frame>();
				else _items = new List<Frame>(ItemLimit);
			}
			#endregion constructors

			int _removeAt(int index)
			{
				if (index >= 0 && index < Count)
				{
					_items.RemoveAt(index);
					return (index == Count ? index - 1 : index);
				}
				else return -1;
			}

			#region public methods
			/// <summary>Deletes the specified item from the Collection.</summary>
			/// <param name="index">Item index.</param>
			/// <returns><see langword="true"/> if successful, <see langword="false"/> for an invalid <paramref name="index"/> value.</returns>
			/// <remarks>Cannot remove the lone <see cref="Frame"/> in a single-<see cref="Frame"/> collection.</remarks>
			[Obsolete("Use RemoveAt", true)]
			public bool Remove(int index) => RemoveAt(index);

			/// <summary>Deletes the specified item from the Collection.</summary>
			/// <param name="index">Item index.</param>
			/// <returns><see langword="true"/> if successful, <see langword="false"/> for an invalid <paramref name="index"/> value.</returns>
			/// <remarks>Cannot remove the lone <see cref="Frame"/> in a single-<see cref="Frame"/> collection.</remarks>
			public bool RemoveAt(int index)
			{
				if (Count == 1) return false;

				bool success = (_removeAt(index) != -1);
				if (success) _parent.recalculateDimensions();
				return success;
			}

			/// <summary>Adds the given item to the end of the Collection.</summary>
			/// <param name="item">The item to be added.</param>
			/// <returns>The index of the added item if successful, otherwise <b>-1</b>.</returns>
			public int Add(Frame item)
			{
				int index = -1;
				if (ItemLimit == -1 || Count < ItemLimit)
				{
					_items.Add(item);
					index = Count - 1;
					_items[index]._parent = _parent;
					_parent.recalculateDimensions();
				}
				return index;
			}

			/// <summary>Removes all Frames from the collection.</summary>
			public void Clear()
			{
				_items.Clear();
				_parent.Dirty();
			}

			/// <summary>Inserts the given item at the specified index.</summary>
			/// <param name="index">Location of the item.</param>
			/// <param name="item">The item to be added.</param>
			/// <returns>The index of the added item if successful, otherwise <b>-1</b>.</returns>
			public int Insert(int index, Frame item)
			{
				if ((ItemLimit == -1 || Count < ItemLimit) && index >= 0 && index <= Count)
				{
					_items.Insert(index, item);
					_items[index]._parent = _parent;
					_parent.recalculateDimensions();
				}
				else index = -1;
				return index;
			}

			/// <summary>Expands or contracts the Collection, populating as necessary.</summary>
			/// <param name="value">The new size of the Collection. Must be greater than <b>0</b>.</param>
			/// <param name="allowTruncate">Controls if the Collection is allowed to get smaller.</param>
			/// <exception cref="InvalidOperationException"><paramref name="value"/> is smaller than <see cref="FixedSizeCollection{T}.Count"/> and <paramref name="allowTruncate"/> is <see langword="false"/>.</exception>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> must be greater than 0.</exception>
			/// <remarks>If the Collection expands, the new items will be a blank <see cref="Frame"/>. When truncating, items will be removed starting from the last index.</remarks>
			public void SetCount(int value, bool allowTruncate)
			{
				if (value == Count) return;
				else if (value < 1) throw new ArgumentOutOfRangeException("value", "value must be greater than 0");
				else if (value < Count)
				{
					if (!allowTruncate) throw new InvalidOperationException("Reducing 'value' will cause data loss");
					else while (Count > value) _removeAt(Count - 1);
				}
				else while (Count < value) Add(new Frame(_parent));
				_parent.recalculateDimensions();
			}
			#endregion public methods

			#region public properties
			/// <summary>Gets or sets the Frame at the specified index.</summary>
			/// <param name="index">The item location within the collection.</param>
			/// <returns>The Frame at the specified index.<br/>-or-<br/><see langword="null"/> for invalid values of <paramref name="index"/>.</returns>
			/// <remarks>No action is taken when attempting to set with invalid values of <paramref name="index"/>.</remarks>
			public Frame this[int index]
			{
				get
				{
					if (index >= 0 && index < Count) return _items[index];
					else return null;
				}
				set
				{
					if (index >= 0 && index < Count)
					{
						_items[index] = value;
						_items[index]._parent = _parent;
						_parent.recalculateDimensions();
					}
				}
			}

			/// <summary>Gets the number of Frames in the collection.</summary>
			public int Count => _items.Count;

			/// <summary>Gets the maximum number of objects allowed in the Collection.</summary>
			/// <remarks>A value of <b>-1</b> means unlimited.</remarks>
			public int ItemLimit { get; }
			#endregion public properties

			#region IEnumerable members
			public IEnumerator<Frame> GetEnumerator()
			{
				return ((IEnumerable<Frame>)_items).GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return ((IEnumerable)_items).GetEnumerator();
			}
			#endregion
		}
	}
}