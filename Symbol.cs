using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace DiffMatchPatch
{
	public class Symbol<T>
	{
		public static List<Symbol<T>> EmptyList {
			get {
				return new List<Symbol<T>>();
			}
		}
		public int BoundaryScore(Symbol<T> next)
		{
			char char1;
			char char2;

			if (value is string)
			{
				string self = value as string;
				char1 = self[self.Length - 1];
				self = next.value as string;
				char2 = self[self.Length - 1];
			}
			else if (value is char)
			{
				char1 = (value as string)[0];
				char2 = (next.value as string)[0];
			}
			else
			{
				return 0;
			}

			bool nonAlphaNumeric1 = !Char.IsLetterOrDigit(char1);
			bool nonAlphaNumeric2 = !Char.IsLetterOrDigit(char2);
			bool whitespace1 = nonAlphaNumeric1 && Char.IsWhiteSpace(char1);
			bool whitespace2 = nonAlphaNumeric2 && Char.IsWhiteSpace(char2);
			bool lineBreak1 = whitespace1 && Char.IsControl(char1);
			bool lineBreak2 = whitespace2 && Char.IsControl(char2);
			bool blankLine1 = lineBreak1;
			bool blankLine2 = lineBreak2;

			if (blankLine1 || blankLine2)
			{
				// Five points for blank lines.
				return 5;
			}
			else if (lineBreak1 || lineBreak2)
			{
				// Four points for line breaks.
				return 4;
			}
			else if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
			{
				// Three points for end of sentences.
				return 3;
			}
			else if (whitespace1 || whitespace2)
			{
				// Two points for whitespace.
				return 2;
			}
			else if (nonAlphaNumeric1 || nonAlphaNumeric2)
			{
				// One point for non-alphanumeric.
				return 1;
			}
			return 0;
		}

		public T value;

		public Symbol() { }

		public Symbol(T value)
		{
			this.value = value;
		}

		public bool IsLineDelimiter
		{
			get
			{
				return value.ToString() == "\n";
			}
		}

		public override bool Equals(System.Object obj)
		{
			if (obj is Symbol<T>)
			{
				return (obj as Symbol<T>).value.Equals(value);
			}
			return false;
		}
	}

	public static class ListExtensions
	{
		public static List<Symbol<char>> FromString(string s)
		{
			return s.Select(c => new Symbol<char>(c)).ToList();
		}

		public static List<Symbol<T>> RangeFrom<T>(this List<Symbol<T>> src, int index)
		{
			return src.GetRange(index, src.Count - index);
		}

		public static List<Symbol<T>> Copy<T>(this List<Symbol<T>> src)
		{
			var copy = new List<Symbol<T>>();

			src.ForEach(s => copy.Add(new Symbol<T>(s.value)));

			return copy;
		}

		public static int IndexOf<T>(this List<Symbol<T>> src, List<Symbol<T>> other)
		{
			return src.IndexOf(other, 0);
		}
		public static int IndexOf<T>(this List<Symbol<T>> src, List<Symbol<T>> other, int startIndex)
		{
			if (src.Count < other.Count)
				return -1;

			for (var si = startIndex; si < src.Count; si++)
			{
				var match = true;
				for (var oi = 0; oi < other.Count; oi++)
				{
					if (src.ElementAt(si + oi) != other.ElementAt(oi))
					{
						match = false;
						break;
					}
				}
				if (match)
					return si;
			}
			return -1;
		}

		public static int LastIndexOf<T>(this List<Symbol<T>> src, List<Symbol<T>> other)
		{
			return src.LastIndexOf(other, src.Count);
		}
		public static int LastIndexOf<T>(this List<Symbol<T>> src, List<Symbol<T>> other, int startIndex)
		{
			if (src.Count < other.Count)
				return -1;

			for (var si = startIndex - other.Count; si >= 0; si--)
			{
				var match = true;
				for (var oi = 0; oi < other.Count; oi++)
				{
					if (src.ElementAt(si + oi) != other.ElementAt(oi))
					{
						match = false;
						break;
					}
				}
				if (match)
					return si;
			}
			return -1;
		}
		public static bool EndsWith<T>(this List<Symbol<T>> src, List<Symbol<T>> other)
		{
			if (src.Count < other.Count)
				return false;

			var diff = src.Count() - other.Count();
			for (var i = other.Count - 1; i >= 0; i--)
			{
				if (other[i] != src[i + diff])
					return false;
			}
			return true;
		}
		public static bool StartsWith<T>(this List<Symbol<T>> src, List<Symbol<T>> item)
		{
			return src.IndexOf(item) == 0;
		}

		public static int IndexWhere<T>(this List<Symbol<T>> src, Func<Symbol<T>, bool> predicate, int after = 0)
		{
			var check = src.Skip(after);

			for (var i = 0; i < check.Count(); i++)
			{
				if (predicate(check.ElementAt(i)))
					return i;
			}
			return -1;
		}

		// Java substring function
		public static List<Symbol<T>> JavaSubstring<T>(this List<Symbol<T>> s, int begin, int end)
		{
			return s.GetRange(begin, end - begin);
		}
	}
}