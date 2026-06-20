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

namespace Idmr.LfdReader
{
	public partial class Text
	{
		/// <summary>Represents a single string within a Text resource.</summary>
		// Developer note: all of the substring functions operate on _value directly, so there isn't a separate instance to track
		public class TextString : IDisposable
		{
			Text _parent;
			string _value;

			/// <summary>Create a new string with the specified value.</summary>
			/// <param name="parent">The parent Text resource.</param>
			/// <param name="value">The initial value</param>
			internal TextString(Text parent, string value)
			{
				_parent = parent;
				_value = value;
			}
			/// <summary>Creates a new empty string.</summary>
			/// <param name="parent">The parent Text resource.</param>
			internal TextString(Text parent) : this(parent, "") { }

			/// <summary>Gets the full length of the string.</summary>
			/// <remarks>Accounts for the final null-terms that are trimmed from <see cref="Value"/>.</remarks>
			public short Length => (short)(_value.Length + 2);

			/// <summary>Gets or sets the raw string value.</summary>
			/// <remarks>String and final SubString 0x00 End Markers are trimmed.<br/>
			/// <b>WARNING:</b> This is the raw string with null chars, no line breaks. Use <see cref="FormattedValue"/> for the user-friendly format.</remarks>
			public string Value
			{
				get => _value;
				set
				{
					_value = value.TrimEnd('\0');
					_parent.Dirty();
				}
			}

			/// <summary>Gets or sets <see cref="Value"/> in a text-friendly format.</summary>
			/// <remarks>Trailing null chars will be trimmed, substrings will be separated by line breaks.<br/>
			/// When setting, line breaks will be treated as substring separators, blank lines will be retained.</remarks>
			public string FormattedValue
			{
				get => _value.Replace("\n\0", "\r\n").Replace("\0", "\r\n");
				set
				{
					_value = value.TrimEnd('\0').Replace("\r\n", "\0").Replace("\0\0", "\0\n\0");
					_parent.Dirty();
				}
			}

			#region IDisposable members
			private bool _disposed;

			protected virtual void Dispose(bool disposing)
			{
				if (_disposed) return;

				if (disposing) { /* do nothing */ }

				_value = null;
				_parent = null;
				_disposed = true;
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}
			#endregion

			#region substrings

			/// <summary>Gets if the string is comprised of multiple substrings.</summary>
			/// <remarks>This is the preferred presence check versus a <see cref="SubstringCount"/> value check.</remarks>
			public bool HasSubstrings => _value.Contains("\0");

			/// <summary>The split, reminder that this re-splits on every access, store for use.</summary>
			string[] _subs => _value.Split('\0');

			/// <summary>Gets the number of substrings.</summary>
			/// <remarks>A value of <b>1</b> denotes no additional substrings.</remarks>
			public int SubstringCount => _subs.Length;

			/// <summary>Gets the specified substring.</summary>
			/// <param name="index">The substring index.</param>
			/// <returns>A new string.</returns>
			/// <exception cref="ArgumentOutOfRangeException">Invalid value for <paramref name="index"/>.</exception>
			public string GetSubstring(int index)
			{
				var subs = _subs;
				if (index < 0 || index >= subs.Length) throw new ArgumentOutOfRangeException("index", $"index must be between 0-{subs.Length - 1}");
				
				return subs[index];
			}

			/// <summary>Sets the specified substring to a new value.</summary>
			/// <param name="index">The substring index.</param>
			/// <param name="value">The new value.</param>
			/// <exception cref="ArgumentOutOfRangeException">Invalid value for <paramref name="index"/>.</exception>
			/// <remarks>If <paramref name="value"/> is <see langword="null"/> or empty, it is removed.</remarks>
			public void SetSubstring(int index, string value)
			{
				var subs = _subs;
				if (index < 0 || index >= subs.Length) throw new ArgumentOutOfRangeException("index", $"index must be between 0-{subs.Length - 1}");

				subs[index] = value;
				_value = string.Join("\0", subs).Replace("\0\0", "\0");	// the Replace removes a null/empty set
				_parent.Dirty();
			}

			/// <summary>Adds a substring to the end of <see cref="Value"/>.</summary>
			/// <param name="value">The new string.</param>
			/// <remarks>If <paramref name="value"/> is <see langword="null"/> or empty, no action is taken.</remarks>
			public void AddSubstring(string value)
			{
				if (string.IsNullOrEmpty(value)) return;

				_value += "\0" + value;
				_parent.Dirty();
			}

			/// <summary>Removes the substring at the specified index.</summary>
			/// <param name="index">The substring index.</param>
			/// <exception cref="ArgumentOutOfRangeException">Invalid value for <paramref name="index"/>.</exception>
			public void RemoveSubstringAt(int index) => SetSubstring(index, "");
			#endregion
		}
	}
}
