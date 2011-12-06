/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 1.0
 */

using System;

namespace Idmr.LfdReader
{
	/// <summary>Collection class for the resources in an LfdFile</summary>
	public class ResourceCollection
	{
		Resource[] _items = null;
		int _count = 0;
		bool _canEditStructure = false;
		string _lockedMessage = "Collection structure is currently locked";

		#region constructors
		/// <summary>Creates a new empty Collection</summary>
		/// <remarks>Structure is unlocked, Add( ), Insert( ) and Remove( ) can be used</remarks>
		public ResourceCollection()
		{
			_canEditStructure = true;
		}
		
		/// <summary>Creates a new Collection with multiple initial Resource placeholders</summary>
		/// <param name="quantity">Number of Resources to start with</param>
		/// <remarks>Structure is locked</remarks>
		public ResourceCollection(int quantity)
		{
			if (quantity > 0) _count = quantity;
			else _count = 1;
			_items = new Resource[_count];
			for (int i=0;i<_count;i++) _items[i] = new Resource();
		}
		
		/// <summary>Creates a new Collection with the specified structure</summary>
		/// <remarks>Structure is locked. Using LfdCategory.Normal is the same as using the blank constructor</remarks>
		public ResourceCollection(LfdFile.LfdCategory category)
		{
			if (category == LfdFile.LfdCategory.Battle)
			{
				_count = 2;
				_items = new Resource[2];
				_items[0] = new Text();
				_items[0].Name = "battle#";
				_items[1] = new Delt();
				_items[1].Name = "b#gal";
			}
			else if (category == LfdFile.LfdCategory.Cockpit)
			{
				_count = 3;
				_items = new Resource[3];
				_items[0] = new Panl();
				_items[1] = new Mask();
				_items[2] = new Pltt();
			}
			else _canEditStructure = true;
		}
		#endregion constructors
		
		#region public methods
		/// <summary>Returns the resource with the specified Tag value, otherwise <i>null</i></summary>
		/// <param name="tag">User-defined data</param>
		public Resource GetResourceByTag(object tag)
		{
			//System.Diagnostics.Debug.WriteLine("tag to match: " + tag);
			for (int i = 0; i < _count; i++)
			{
				//System.Diagnostics.Debug.WriteLine("_items[" + i + "] tag: " + _items[i].Tag);
				if (_items[i].Tag != null && _items[i].Tag.ToString() == tag.ToString()) return _items[i];
			}
			//System.Diagnostics.Debug.WriteLine("didn't find Tag");
			return null;
		}

		/// <summary>Returns the index of the resource with the specified Tag value, otherwise -1</summary>
		/// <param name="tag">User-defined data</param>
		public int GetIndexByTag(object tag)
		{
			//System.Diagnostics.Debug.WriteLine("tag to match: " + tag);
			for (int i = 0; i < _count; i++)
			{
				//System.Diagnostics.Debug.WriteLine("_items[" + i + "] tag: " + _items[i].Tag);
				if (_items[i].Tag != null && _items[i].Tag.ToString() == tag.ToString()) return i;
			}
			//System.Diagnostics.Debug.WriteLine("didn't find Tag");
			return -1;
		}

		/// <summary>Finds the resource with the specified Tag value and replaces it with <i>resource</i></summary>
		/// <remarks>If <i>tag</i> is not found, no action is taken</remarks>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked and Resource Type or Name have been changed</exception>
		public void SetByTag(object tag, Resource resource)
		{
			for (int i = 0; i < _count; i++)
				if (_items[i].Tag == tag)
				{
					if (!_canEditStructure && (_items[i].Name != resource.Name || _items[i].Type != resource.Type))
						throw new InvalidOperationException(_lockedMessage + " (SetByTag)");
					_items[i] = resource;
					_items[i].Tag = tag;
					break;
				}
		}

		/// <summary>Empties the Collection of entries</summary>
		/// <remarks>All existing resources are lost, <i>Count</i> is set to zero</remarks>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public void Clear()
		{
			if (!_canEditStructure) throw new InvalidOperationException(_lockedMessage + " (Clear)");
			_count = 0;
			_items = null;
		}
		
		/// <summary>Adds a new Resource to the end of the Collection</summary>
		/// <returns>The index of the added Resource if successfull, otherwise -1</returns>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public int Add() { return _add(new Resource()); }

		/// <summary>Adds the given Resource to the end of the Collection</summary>
		/// <param name="resource">The Resource to be added</param>
		/// <returns>The index of the added Resource if successfull, otherwise -1</returns>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public int Add(Resource resource) { return _add(resource); }
		
		/// <summary>Inserts a new Resource at the specified index</summary>
		/// <param name="index">Location of the Resource within the Collection</param>
		/// <returns>The index of the added Resource if successfull, otherwise -1</returns>
		/// <exception cref="IndexOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public int Insert(int index) { return _insert(index, new Resource()); }

		/// <summary>Inserts the given Resource at the specified index</summary>
		/// <param name="index">Location of the Resource</param>
		/// <param name="resource">The Resource to be added</param>
		/// <returns>The index of the added Resource if successfull, otherwise -1</returns>
		/// <exception cref="IndexOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public int Insert(int index, Resource resource) { return _insert(index, resource); }
		
		/// <summary>Deletes the Resource at the specified index</summary>
		/// <remarks>If first and only Resource is specified, executes Clear( )</remarks>
		/// <param name="index">The index of the Resource to be deleted</param>
		/// <returns>The index of the next available Resource if successfull, otherwise -1</returns>
		/// <exception cref="IndexOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public int RemoveAt(int index)
		{
			if (index >= 0 && index < _count && _count > 1) { return _removeAt(index); }
			else if (index == 0 && _count == 1) Clear();
			return -1;
		}
		#endregion public methods
		
		#region public properties
		/// <summary>A single Resource within the collection</summary>
		/// <exception cref="IndexOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to set when structure is locked and Resource Type or Name have been changed</exception>
		public Resource this[int index]
		{
			get { return _items[index]; }
			set
			{
				if (!_canEditStructure && _items[index].Type != Resource.ResourceType.Undefined && (_items[index].Name != value.Name || _items[index].Type != value.Type))
					throw new InvalidOperationException(_lockedMessage + " (this)");
				_items[index] = value;
			}
		}
		
		/// <summary>Gets the number of objects in the Collection</summary>
		public int Count { get { return _count; } }
		
		/// <summary>Gets or sets whether or not the structure is unlocked</summary>
		/// <remarks>Default is <i>false</i>, set to <i>true</i> when using blank constructor or LfdCategory.Normal. When locked, Resource Names and Types and fixed, Collection cannot be resized</remarks>
		public bool CanEditStructure
		{
			get { return _canEditStructure; }
			set { _canEditStructure = value; }
		}
		#endregion public properties
		
		int _add(Resource item)
		{
			if (!_canEditStructure) throw new InvalidOperationException(_lockedMessage + " (add)");
			Resource[] tempItems = _items;
			_items = new Resource[_count+1];
			for (int i=0;i<(_count);i++) _items[i] = tempItems[i];
			_items[_count] = item;
			_count++;
			return (short)(_count-1);
		}
		int _insert(int index, Resource item)
		{
			if (!_canEditStructure) throw new InvalidOperationException(_lockedMessage + " (insert)");
			if (index >= 0 && index <= _count)
			{
				Resource[] tempItems = _items;
				_items = new Resource[_count+1];
				for (int i=0;i<index;i++) _items[i] = tempItems[i];
				_items[index] = item;
				for (int i=index;i<_count;i++) _items[i+1] = tempItems[i];
				_count++;
				return index;
			}
			else return -1;
		}
		int _removeAt(int index)
		{
			if (!_canEditStructure) throw new InvalidOperationException(_lockedMessage + " (remove)");
			_count--;
			Resource[] tempItems = _items;
			_items = new Resource[_count];
			for (int i=0;i<index;i++) _items[i] = tempItems[i];
			for (int i=index;i<_count;i++) _items[i] = tempItems[i+1];
			return (index == _count ? index-1 : index);
		}
	}
}