using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiffMatchPatch
{
	public abstract class SymbolTextParser<T>
	{
		public abstract List<Symbol<T>> SymbolsFromText(string text);
	}
	public abstract class SymbolTextReader<T>
	{
		public abstract string TextFromSymbol(Symbol<T> symbol);

		public string TextFromSymbols(List<Symbol<T>> symbols)
		{
			StringBuilder s = new StringBuilder();
			symbols.ForEach(sym => s.Append(TextFromSymbol(sym)));

			return s.ToString();
		}
	}
	public class TextSymbolReader<T> : SymbolTextReader<T>
	{
		public override string TextFromSymbol(Symbol<T> symbol)
		{
			return symbol.value.ToString();
		}
	}

	public class CharacterSymbolParser : SymbolTextParser<char>
	{
		public override List<Symbol<char>> SymbolsFromText(string text)
		{
			return text.Select(c => new Symbol<char>(c)).ToList();
		}
	}
	public class CharacterTextSymbolParser : SymbolTextParser<string>
	{
		public override List<Symbol<string>> SymbolsFromText(string text)
		{
			return text.Select(c => new Symbol<string>(""+c)).ToList();
		}
	}

	public class LineSymbolParser : SymbolTextParser<string>
	{
		public override List<Symbol<string>> SymbolsFromText(string text)
		{
			return text.Split(new char[] { '\n' }).Select(l => new Symbol<string>(l)).ToList();
		}
	}

	public class DelimitedTextSymbolParser : SymbolTextParser<string>
	{
		protected char[] delimiters;
		public DelimitedTextSymbolParser(char[] delimiters)
		{
			this.delimiters = delimiters;
		}

		public override List<Symbol<string>> SymbolsFromText(string text)
		{
			return text.Split(delimiters).Select(t => new Symbol<string>(t)).ToList();
		}
	}
	public class CheckCharacterSymbolParser : SymbolTextParser<string>
	{
		protected Func<char, bool> newSymbol;
		public CheckCharacterSymbolParser(Func<char, bool> newSymbol)
		{
			this.newSymbol = newSymbol;
		}

		public override List<Symbol<string>> SymbolsFromText(string text)
		{
			string s = "";

			List<Symbol<string>> symbols = Symbol<string>.EmptyList;

			foreach (char c in text)
			{
				if (s.Length == 0 || !this.newSymbol(c))
					s += c;
				else
				{
					symbols.Add(new Symbol<string>(s));
					s = "" + c;
				}
			}

			if (s.Length > 0)
				symbols.Add(new Symbol<string>(s));

			return symbols;
		}
	}
	public class WordSymbolParser : SymbolTextParser<string>
	{
		protected bool inword = false;
		protected bool newSymbol(char c)
		{
			return inword ? !Char.IsLetterOrDigit(c) : true;
		}

		public override List<Symbol<string>> SymbolsFromText(string text)
		{
			string s = "";

			List<Symbol<string>> symbols = Symbol<string>.EmptyList;

			inword = Char.IsLetterOrDigit(text[0]);
			foreach (char c in text)
			{
				if (!this.newSymbol(c))
					s += c;
				else
				{
					symbols.Add(new Symbol<string>(s));
					s = "" + c;
					inword = Char.IsLetterOrDigit(c);
				}
			}

			if (s.Length > 0)
				symbols.Add(new Symbol<string>(s));

			return symbols;
		}
	}
	public class HtmlSymbolParser : SymbolTextParser<string>
	{
		SymbolTextParser<string> textParser;

		public HtmlSymbolParser(bool word = false)
		{
			if (word)
				initWithParser(new WordSymbolParser());
			else
				initWithParser(new CharacterTextSymbolParser());
		}
		public HtmlSymbolParser(SymbolTextParser<string> textParser)
		{
			initWithParser(textParser);
		}
		protected void initWithParser(SymbolTextParser<string> textParser)
		{
			this.textParser = textParser;
		}

		protected bool intag = false;
		protected bool inComment = false;
		protected bool nextnew = false;

		protected bool shouldUseTextParser(char c)
		{
			//Flip intag to false, but treet this character as part of a tag (return false)
			if (intag && c == '>')
			{
				nextnew = true;
				return intag = false;
			}
			//Flip intag to true and treet this character as part of a tag (return false)
			else if (!intag && c == '<')
				return !(intag = true);
			//Nothing has changed, keep treeting the text the same (as part of a tag if intag, or as text if not)
			else
				return !intag;
		}

		public override List<Symbol<string>> SymbolsFromText(string text)
		{
			intag = false;
			bool usingTextParser = true;
			string s = "";

			List<Symbol<string>> symbols = Symbol<string>.EmptyList;

			for (var i = 0; i < text.Length; i++)
			{
				char c = text[i];

				//Handle html comments.  Because somebody could have an html tag inside a comment and it could jack everything up.
				if (!inComment && i + 4 < text.Length && text.Substring(i, 4) == "<!--")
				{
					if (s.Length > 0 && usingTextParser)
					{
						symbols.AddRange(textParser.SymbolsFromText(s));
					}
					s = "<!--";
					inComment = true;
					i += 3;
					continue;
				}

				if (inComment)
				{
					if (text.Substring(i, 3) == "-->")
					{
						inComment = false;
						s += "-->";
						if (usingTextParser)
						{
							symbols.Add(new Symbol<string>(s));
							s = "";
						}
						i += 2;
					}
					else
						s += c;

					continue;
				}
				var isnew = nextnew;
				nextnew = false;
				var shouldUse = shouldUseTextParser(c);

				//We just flipped parsing methods, let's parse the text prior to this character using the current parser, and then reset to use the new method
				if (s.Length > 0 && (shouldUse != usingTextParser || isnew))
				{
					if (usingTextParser)
						symbols.AddRange(textParser.SymbolsFromText(s));
					else
						symbols.Add(new Symbol<string>(s));
					s = "";
				}

				s += c;
				usingTextParser = shouldUse;
			}

			if (s.Length > 0)
			{
				if (usingTextParser)
					symbols.AddRange(textParser.SymbolsFromText(s));
				else
					symbols.Add(new Symbol<string>(s));
			}

			return symbols;
		}
	}
}