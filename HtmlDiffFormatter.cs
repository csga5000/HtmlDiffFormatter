﻿using System;
using System.Collections.Generic;
using System.Linq;
using csga5000.DiffMatchPatch;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

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
            string[] tableElements = new string[] { "<table", "<td", "<tr" };
            string[] listElements = new string[] { "<li", "<ol", "<ul" };
            string image = "<img";
            string div = "<div";
            bool addedAttribute = false;
            HtmlAgilityPack.HtmlDocument html = new HtmlAgilityPack.HtmlDocument();

			override public string textForChange(string text, DiffMatchPatch.Operation op)
			{
				if (text == "" || text == null)
					return "";

                html.LoadHtml(text);
                var containsHtml = (html.DocumentNode.Descendants().Count() > 0);

                if (containsHtml)
                {
                    if (listElements.Any(text.Contains))
                    {
                        var listNodes = html.DocumentNode.Descendants().Where(n => n.Name == "li" || n.Name == "ol");

                        foreach (var li in listNodes)
                        {
                            addAttribute("class", Tuple.Create("styleDel", "styleIns"), op, li);
                        }
                    }

                    if (tableElements.Any(text.Contains))
                    {
                        var tableNodes = html.DocumentNode.Descendants().Where(t => t.Name == "table" || t.Name == "td" || t.Name == "tr");
                        foreach (var table in tableNodes)
                        {
                            if (table.Name == "table")
                            {
                                addAttribute("bordercolor", Tuple.Create("red", "green"), op, table);
                            }
                            else if (table.Name == "td")
                            {
                                addAttribute("class", Tuple.Create("tdDel", "tdIns"), op, table);
                            }
                            else
                            {
                                addAttribute("class", Tuple.Create("color: red", "color: green"), op, table);
                            }
                        }
                    }

                    if (text.Contains(div))
                    {
                        var divs = html.DocumentNode.Descendants().Where(d => d.Name == "div");
                        foreach (var div in divs)
                        {
                            addAttribute("class", Tuple.Create("styleDel", "styleIns"), op, div);
                        }
                    }

                    if (text.Contains(image))
                    {
                        var images = html.DocumentNode.Descendants().Where(t => t.Name == "img").ToList();
                        var imagefile = (op == Operation.INSERT) ? "image-ins" : "image-del";
                        var svg = "<img src='/Bundles/mco/images/" + imagefile + ".svg' class='svg' >";
                        foreach (var image in images)
                        {
                            addAttribute("class", Tuple.Create("imgDel", "imgIns"), op, image);
                            if(op != Operation.EQUAL)
                                image.ParentNode.InnerHtml = "<div class='img'>" + image.OuterHtml + "<br />" + svg + "</div>";
                        }
                    }

                    if (addedAttribute)
                    {
                        addedAttribute = false;
                        return html.DocumentNode.OuterHtml;
                    }

                }

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

            private void addAttribute(string key, Tuple<string, string> values, Operation op, HtmlNode element)
            {
                addedAttribute = true;
                switch (op)
                {
                    case DiffMatchPatch.Operation.DELETE:
                        element.Attributes.Add(key, values.Item1);
                        break;
                    case DiffMatchPatch.Operation.INSERT:
                        element.Attributes.Add(key, values.Item2);
                        break;
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
			public DiffMatchPatch.Operation op;

			protected List<DiffSeg> children;

			public DiffSeg(Operation op, string text)
			{
				this.op = op;
				this.text = text;
				text = text.Trim();
				startTag = true;

				if (text == null || text.Length == 0)
					return;

				if (text.IndexOf("<!--") == 0) {
					tagName = "<!--";
					selfClosing = true;
					tag = true;
				}
				else if (text[0] == '<')
				{
					tag = true;
					tagName = "";

					bool named = false;

					foreach (var c in text)
					{
						if (!named)
						{
							if (c == '<')
								continue;
							if (c == '/')
							{
								if (tagName.Length > 0)
								{
									selfClosing = true;
									named = true;
									break;
								}
								else
									startTag = false;
							}
							else if (c != ' ' && c != '>')
								tagName += c;
							else if ((c == ' ' && tagName.Length > 0) || c == '>')
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

				if (CanHaveChildren)
					children = new List<DiffSeg>();
			}
			public bool CanHaveChildren
			{
				get
				{
					return tag && startTag;
				}
			}

			public void addChild(DiffSeg seg)
			{
				if (!CanHaveChildren)
					throw new Exception("Non-tag diff segments have no children");
				this.children.Add(seg);
			}

			public List<DiffSeg> getChildren()
			{
				if (!CanHaveChildren)
					throw new Exception("Non-tag diff segments have no children");
				return this.children;
			}

			public bool hasText()
			{
				if (!CanHaveChildren)
					throw new Exception("Diff segs that aren't tags *are* text!");

				return this.children.Any(c => !c.tag || c.hasText());
			}
			public bool childrenMatch()
			{
				//If we have 1 or 0 children then we're just a tag with no children.
				if (!CanHaveChildren || children.Count < 2)
					return true;

				return this.children.All(c => c.op == op && c.childrenMatch());
			}
			public string getText()
			{
				String text = this.text;
				if (CanHaveChildren)
					this.children.ForEach(c => text += c.getText());

				return text;
			}
			public string getInnerText()
			{
				String text = "";
				if (CanHaveChildren)
					this.children.ForEach(c => text += c.getText());

				return text;
			}
			protected string textForGroup(List<DiffSeg> group)
			{
				var groupText = "";
				group.ForEach(ds => groupText += ds.getText());
				return groupText;
			}
			public string getFormattedText(Formatter formatter)
			{
				String text = this.text;

				if (CanHaveChildren && children.Count > 0)
				{
					if (childrenMatch())
					{
						return formatter.textForChange(getText(), op);
					}
					else
					{
						//We could just append the text of all the children's results t xmo "getFormattedText" but instead we group bits in sequence that are all the same operation.
						Operation currop = this.op;
						List<DiffSeg> sameOp = new List<DiffSeg>();

						for (var i = 0; i < children.Count; i++)
						{
							var curr = children[i];
							var canGroup = curr.childrenMatch();

							if (sameOp.Count == 0 && canGroup) {
								currop = curr.op;
								sameOp.Add(curr);
							}
							else if (canGroup && curr.op == currop)
								sameOp.Add(curr);

							else
							{
								if (sameOp.Count > 0)
								{
									text += formatter.textForChange(textForGroup(sameOp), currop);
									sameOp.Clear();
								}
								if (canGroup)
								{
									currop = curr.op;
									sameOp.Add(curr);
								}
								else
									text += curr.getFormattedText(formatter);
							}
						}
						if (sameOp.Count > 0)
							text += formatter.textForChange(textForGroup(sameOp), currop);
					}
				}
				else if (!tag)
					return formatter.textForChange(text, this.op);

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
        public static bool change;
		protected int addChildren(DiffSeg seg, List<DiffSeg> segs, int index)
		{
			if (!seg.tag || seg.selfClosing || !seg.startTag)
				return index;

			List<DiffSeg> children = seg.getChildren();
			children.Clear();
			for (index++; index < segs.Count; index++)
			{
				var cseg = segs[index];

				var ender = !cseg.startTag && cseg.tag;
                children.Add(cseg);
                if (ender)
				{
                    //The differ tends to make previous ending tags be "changed" if the same tag is on the end of what was really changed, and the real closing tag is "the same"
                    //If the HTML is valid, then we can be certain that if we're at an ending tag at this point in our recursion, then it must be the case where it was marked incorrectly
                    seg.op = cseg.op;
                    
					return index;
				}

				index = addChildren(cseg, segs, index);
			}
			//This *should* mean bad html.  So we'll hack an end tag in.
			children.Add(new DiffSeg(seg.op, "</" + seg.tagName + ">"));
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
			var segs = new List<DiffSeg>();

			diffs.ForEach(d => d.text.ForEach(t => segs.Add(new DiffSeg(d.operation, t.value))));

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
				output += seg.getFormattedText(formatter);
			}

			return output;
		}
	}
}
