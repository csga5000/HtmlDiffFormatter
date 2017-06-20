using System;
using System.Collections.Generic;
using System.Linq;
using DiffMatchPatch;

namespace csga5000.HtmlDiffFomratter
{
	public class HtmlDiffFormatter
	{
		private static TextSymbolReader<string> _reader;
		public static TextSymbolReader<string> SymbolReader
		{
			get
			{
				if (_reader == null)
					_reader = new TextSymbolReader<string>();

				return _reader;
			}
		}
		private static HtmlSymbolParser _writer;
		public static HtmlSymbolParser SymbolParser
		{
			get
			{
				if (_writer == null)
					_writer = new HtmlSymbolParser(true);

				return _writer;
			}
		}

		private static diff_match_patch<string> _dmp;
		public static diff_match_patch<string> dmp {
			get {
				if (_dmp == null)
					_dmp = new diff_match_patch<string>();

				return _dmp;
			}
		}

		public abstract class Formatter
		{
			public abstract string textForChange(string text, DiffMatchPatch.Operation op);
		}
		protected class DefaultFormatter : HtmlDiffFormatter.Formatter
		{

			override public string textForChange(string text, DiffMatchPatch.Operation op)
			{
				if (text == "" || text == null)
					return "";

				switch (op)
				{
					case DiffMatchPatch.Operation.DELETE:
						return "<del style=\"text-decoration: line-through;color: red;\">" + text + "</del>";
					case DiffMatchPatch.Operation.INSERT:
						return "<ins style=\"text-decoration: underline;color: green;\">" + text + "</ins>";
					default:
						return text;
				}
			}
		}
		protected class DiffSeg
		{
			public static string[] SELF_CLOSING_TAGS = new string[] { "area", "base", "br", "col", "command", "embed", "hr", "img", "input", "keygen", "link", "meta", "param", "source", "track", "wbr", "!DOCTYPE" };
			public bool tag = false;
			public string tagName;
			protected string text;
			public bool startTag { get; set; }
			public bool selfClosing { get; set; }
			public DiffMatchPatch.Diff<string> diff { get; set; }

			protected List<DiffSeg> children;

			public DiffSeg(DiffMatchPatch.Diff<string> diff)
			{
				this.diff = diff;

				text = SymbolReader.TextFromSymbols(diff.text).Trim();

				if (text == null || text.Length == 0)
					return;

				if (text.IndexOf("<!--") == 0) {
					tagName = "<!--";
					selfClosing = true;
					startTag = true;
					tag = true;
				}
				else if (text[0] == '<')
				{
					children = new List<DiffSeg>();
					tag = true;
					tagName = "";

					bool named = false;

					foreach (var c in text)
					{
						if (!named) {
							if (c == '/')
								startTag = false;
							else if (c != ' ')
								tagName += c;
							else if (c == ' ' && tagName.Length > 0)
								named = true;
						}
						else
						{
							if (c == '/')
							{
								selfClosing = true;
								break;
							}
						}
					}

					//These elements are *always* self closing, so we'll let them slide if they didn't put the "/"
					if (SELF_CLOSING_TAGS.Contains(tagName))
						selfClosing = true;
				}
			}

			public void addChild(DiffSeg seg)
			{
				if (!tag)
					throw new Exception("Non-tag diff segments have no children");
				this.children.Add(seg);
			}

			public List<DiffSeg> getChildren()
			{
				if (!tag)
					throw new Exception("Non-tag diff segments have no children");
				return this.children;
			}

			public bool hasText()
			{
				if (!tag)
					throw new Exception("Diff segs that aren't tags *are* text!");

				return this.children.Any(c => !c.tag || c.hasText());
			}
			public string getText()
			{
				String text = this.text;
				if (tag)
					this.children.ForEach(c => text += c.getText());

				return text;
			}
		}

		Formatter formatter;

		public HtmlDiffFormatter()
		{
			formatter = new DefaultFormatter();
		}
		public HtmlDiffFormatter(Formatter formatter)
		{
			this.formatter = formatter;
		}

		protected int addChildren(DiffSeg seg, List<DiffSeg> segs, int index)
		{
			if (!seg.tag || seg.selfClosing || !seg.startTag)
				return index;

			var startingIndex = index;
			List<DiffSeg> children = seg.getChildren();
			children.Clear();

			for (index++; index < segs.Count; index++)
			{
				var cseg = segs[index];
				if (cseg.diff.operation == seg.diff.operation)
				{
					children.Add(cseg);
					if (!cseg.startTag)
					{
						return index;
					}
				}
				else
				{
					children.Clear();
					return startingIndex;
				}

				index = addChildren(seg, segs, index);
			}
			//This *should* mean bad html.  So we'll hack an end tag in.
			var endTagS = new List<Symbol<string>>();
			endTagS.Add(new Symbol<string>("</"+seg.tagName+">"));
			var endTagD = new Diff<string>(seg.diff.operation, endTagS);

			children.Add(new DiffSeg(endTagD));
			return index;
		}

		public string diffOutput(string startText, string endText)
		{
			var diffs = dmp.diff_main(SymbolParser.SymbolsFromText(startText), SymbolParser.SymbolsFromText(endText));

			dmp.diff_cleanupSemantic(diffs);

			return diffOutput(diffs);
		}

		public string diffOutput(List<Diff<string>> diffs)
		{
			var segs = diffs.Select(d => new DiffSeg(d)).ToList();

			var rootSegs = new List<DiffSeg>();

			//Setup tree matching html using recursion.  This only matches the HTML as far as elements with the same operation go..
			for (var index = 0; index < segs.Count; index++)
			{
				var seg = segs[index];
				rootSegs.Add(seg);

				index = addChildren(seg, segs, index);
			}

			//Build diff output

			var output = "";

			foreach (var seg in rootSegs)
			{
				//Since segments only contain the children that match their operation, all children are of the same operation.
				output += formatter.textForChange(seg.getText(), seg.diff.operation);
			}

			return output;
		}
	}
}
