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
 * [NEW] implemented SetCount
 * v1.0
 */

using System;
using System.Collections.Generic;
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Anim : Resource
	{
		/// <summary>Object to maintain Anim image <see cref="Frame">Frames</see>.</summary>
		/// <remarks><see cref="ResizableCollection{T}.ItemLimit"/> is set to <b>50</b>.</remarks>
		public class FrameCollection : ResizableCollection<Frame>
		{
			internal Anim _parent;
			
			#region constructors
			/// <summary>Creates an empty Collection.</summary>
			internal FrameCollection(Anim parent)
			{
				_parent = parent;
				_itemLimit = 50;
				_items = new List<Frame>(_itemLimit);
			}
			#endregion constructors
	
			#region public methods
			/// <summary>Deletes the specified item from the Collection.</summary>
			/// <param name="index">Item index.</param>
			/// <returns><b>true</b> if successful, <b>false</b> for an invalid <paramref name="index"/> value.</returns>
			/// <remarks>Cannot remove the lone <see cref="Frame"/> in a single=<see cref="Frame"/> collection.</remarks>
			public bool Remove(int index)
			{
				if (Count == 1) return false;
				bool success = (_removeAt(index) != -1);
				_parent.recalculateDimensions();
				if (success) _isModified = true;
				return success;
			}

			/// <summary>Adds the given item to the end of the Collection.</summary>
			/// <param name="item">The item to be added.</param>
			/// <returns>The index of the added item if successful, otherwise <b>-1</b>.</returns>
			new public int Add(Frame item)
			{
				int index = _add(item);
				if (index != -1)
				{
					_items[index]._parent = _parent;
					_parent.recalculateDimensions();
					if (!_isLoading) _isModified = true;
				}
				return index;
			}

			/// <summary>Inserts the given item at the specified index.</summary>
			/// <param name="index">Location of the item.</param>
			/// <param name="item">The item to be added.</param>
			/// <returns>The index of the added item if successful, otherwise <b>-1</b>.</returns>
			new public int Insert(int index, Frame item)
			{
				index = _insert(index, item);
				if (index != -1)
				{
					_items[index]._parent = _parent;
					_parent.recalculateDimensions();
					_isModified = true;
				}
				return index;
			}

			/// <summary>Expands or contracts the Collection, populating as necessary.</summary>
			/// <param name="value">The new size of the Collection. Must be greater than <b>0</b>.</param>
			/// <param name="allowTruncate">Controls if the Collection is allowed to get smaller.</param>
			/// <exception cref="InvalidOperationException"><paramref name="value"/> is smaller than <see cref="FixedSizeCollection{T}.Count"/> and <paramref name="allowTruncate"/> is <b>false</b>.</exception>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> must be greater than 0.</exception>
			/// <remarks>If the Collection expands, the new items will be a blank <see cref="Frame"/>. When truncating, items will be removed starting from the last index.</remarks>
			public override void SetCount(int value, bool allowTruncate)
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
				if (!_isLoading) _isModified = true;
			}
			#endregion public methods
			
			#region public properties
			/// <summary>Gets or sets a single item within the Collection.</summary>
			/// <param name="index">The item location within the collection.</param>
			/// <returns>A single item within the collection<br/>-or-<br/><b>null</b> for invalid values of <paramref name="index"/>.</returns>
			/// <remarks>No action is taken when attempting to set with invalid values of <paramref name="index"/>.</remarks>
			new public Frame this[int index]
			{
				get { return _getItem(index); }
				set
				{
					_setItem(index, value);
					if (index >= 0 && index < Count)
					{
						_items[index]._parent = _parent;
						_parent.recalculateDimensions();
					}
				}
			}
			#endregion public properties
		}
	}
}