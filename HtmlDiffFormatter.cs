using System;
using System.Collections.Generic;
using System.Linq;

namespace csga5000.HtmlDiffFomratter
{
	public class HtmlDiffFormatter
	{
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
			public bool tag = false;
			public string text = "";
			public bool startTag { get; set; }
			public bool selfClosing { get; set; }
			public DiffMatchPatch.Operation operation = DiffMatchPatch.Operation.EQUAL;

			/**
			 * Will append the character or change the operation if it is non-space and the first character and this is a tag that is "equal"
			 * Will also change "selfClosing" if c is / and text is "<".
			 * It doesn't append the character if the op changes so you can redo the characters iteration.
			 * 
			 * @return If the operation has changed
			 * */
			public bool appendTextOrChangeOp(char c, DiffMatchPatch.Operation op)
			{
				var changed = false;
				if (c == ' ')
					return false;

				//If it's a closing tag we don't change the operation...
				if (c == '/' && text == "<")
					selfClosing = true;
				else if (text == "<" && operation != op)
				{
					operation = op;
					return true;
				}

				this.text += c;
				return changed;
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

		protected List<DiffSeg> segsForDiffs(List<DiffMatchPatch.Diff> diffs)
		{
			//Note: Assumes valid HTML.  If we are concerned about this we can make run the HTML through a validator or something and fail if it isn't (which should only happen if they edit the html themselves)
			//Note: Also probaly assumes you don't have spaces in your tags.  That could also be validated/fixed.  Even a regex should be-able to handle that assuming it's valid html.  (See above comment ^)
			//Note: This will not necessarily show changes in styling.  That.. would be very hard to do.  But hopefully it is accurate in terms of visible character value diffing.
			var intag = false;

			var segs = new List<DiffSeg>();
			var seg = new DiffSeg();
			seg.operation = diffs.ElementAt(0).operation;

			var firstdiff = 0;

			for (var di = 0; di < diffs.Count; di++)
			{
				var diff = diffs.ElementAt(di);

				//Special case for when tag spans multiple diffs.  It's not fun if it does.
				//But essentially, our goal is to get 4 things.  The text that will no longer show before the tag, the text that will now show before the tag, the tag itself, and what ever is left in the last diff this tag spans
				//With those four things, we can remove all the spanned diffs and replace them with those 4 diffs.
				//I believe this is probably styling imperfect for changes after
				if (intag)
				{
					var finishedTag = false;
					var changedOp = false;
					var treatAsEqual = false;//Hah.  Another hack to conditionally change behavior.  Since we specially handle < operation, if it's a </..> where the < is inserted, and the /.. is equal then it should register the tag as an insert but handle the inner characters as a equals

					//If any text is going to appear before the tag, we can just add that here
					var added = new DiffMatchPatch.Diff(DiffMatchPatch.Operation.INSERT, "");

					//If any text is going to disappear before the tag, we can just add that here
					var deleted = new DiffMatchPatch.Diff(DiffMatchPatch.Operation.DELETE, "");

					var remaining = new DiffMatchPatch.Diff(DiffMatchPatch.Operation.INSERT, "");

					List<DiffMatchPatch.Diff> tagDiffs = new DiffMatchPatch.Diff[] {
						added,
						deleted,
						//Rest can be added later
					}.ToList();

					//Note:  Breaking in the for loop below may cause issues. di is used below, and breaking may prevent the incrementor. (As the incremend should happen pre-condition when you don't break, and probably won't happen if you do.  Not sure, would have to test.)
					for (; di < diffs.Count && !finishedTag; di++)
					{
						var adiff = diffs.ElementAt(di);

						remaining.operation = adiff.operation;

						for (var chi = 0; chi < adiff.text.Length; chi++)
						{
							//Redo that last character if the op changed.  Should be the first "real" character
							if (changedOp)
							{
								chi--;
								changedOp = false;
							}
							var adch = adiff.text.ElementAt(chi);

							if (finishedTag)
							{
								//Add what's left in this diff to a new diff that we'll put on the end of the diffs we generated previously.  I know this is a pain, I'm sorry.
								remaining.text += adch;
								continue;
							}

							if (seg.operation == DiffMatchPatch.Operation.DELETE && !treatAsEqual)
							{
								//Ignore any deleted characters inside the tag that aren't >
								if (adiff.operation == DiffMatchPatch.Operation.DELETE)
								{
									//Removed stuff is part of removed tag, and treat everything else as "added"
									//It will still be segmented after this incase something weird happened like a new tag being inside the old tag.
									//Note that we don't need to worry about the new tag's > because it's not in a "deleted" diff
									seg.text += adch;

									if (adch == '>')
									{
										finishedTag = true;
										continue;
									}
								}
								//Since we'll segment what is added afterwards, we can just add all inserted/equal chars that appear before the deleted > to added segment text
								else if (adiff.operation != DiffMatchPatch.Operation.DELETE)
								{
									//Unless this happens.  This changes everything.
									if (diff.operation == DiffMatchPatch.Operation.INSERT && adch == '<')
									{
										//Ignore removed stuff so far, and add what's the same.  Can continue loop with new tag, since we still haven't found the end, but this time it'll be an inserted tag not a deleted one
										tagDiffs.Add(new DiffMatchPatch.Diff(seg.operation, seg.text));
										seg.operation = DiffMatchPatch.Operation.INSERT;
										seg.text = "<";
									}
									else if (diff.operation != DiffMatchPatch.Operation.INSERT)
									{
										//Equal stuff isn't in a tag anymore, but is visible outside where the tag was (since the tag is being deleted)
										if (diff.operation == DiffMatchPatch.Operation.EQUAL)
											changedOp = seg.appendTextOrChangeOp(adch, adiff.operation);

										if (!changedOp)
											added.text += adch;
									}
								}
							}
							else if (seg.operation == DiffMatchPatch.Operation.INSERT && !treatAsEqual)
							{
								//We're looking at an inserted <.

								if (adiff.operation != DiffMatchPatch.Operation.DELETE && adch == '>')
								{
									finishedTag = true;
									changedOp = seg.appendTextOrChangeOp(adch, adiff.operation);
								}
								else if (adiff.operation != DiffMatchPatch.Operation.INSERT)
								{
									if (adiff.operation == DiffMatchPatch.Operation.EQUAL)
									{
										changedOp = seg.appendTextOrChangeOp(adch, adiff.operation);
									}
									if (adch == '/' && adiff.operation != DiffMatchPatch.Operation.INSERT)
									{
										treatAsEqual = true;
									}
									//Anything equal OR deleted inside, since it won't show now, is deleted
									else if (!changedOp)
										deleted.text += adch;
								}
								else
								{
									//Anything inserted inside is just part of the new tag.
									changedOp = seg.appendTextOrChangeOp(adch, adiff.operation);
								}
							}
							else
							{
								if (adiff.operation != DiffMatchPatch.Operation.DELETE)
								{
									if (adch == '>')
										finishedTag = true;

									changedOp = seg.appendTextOrChangeOp(adch, adiff.operation);
								}
								//else.. it's just removed form inside the tag.  So no problemo.
								//TODO:  UNLESS!  The end tag registers as "removed" and there is a new one later.
							}
						}
					}

					//We have found the end of the diff spanning tag.  Now we can add the changes made.
					tagDiffs.Add(new DiffMatchPatch.Diff(seg.operation, seg.text));
					tagDiffs.Add(remaining);

					//All diffs in this range should now be accounted for in the new diffs
					diffs.RemoveRange(firstdiff, di - firstdiff);

					//Add those.
					while (0 < tagDiffs.Count)
					{
						var td = tagDiffs.Last();
						tagDiffs.RemoveAt(tagDiffs.Count - 1);
						if (td.text != "")
						{
							diffs.Insert(firstdiff, td);
						}
					}
					di = firstdiff;

					intag = false;

					diff = diffs.ElementAt(di);
					seg.text = "";//So that it doesn't double add the seg below.
				}

				//Actually segment.  (yes, everything above was JUST for the diff spanning tag condition)
				for (var chi = 0; chi < diff.text.Count(); chi++)
				{
					var ch = diff.text.ElementAt(chi);
					firstdiff = di;

					seg.operation = diff.operation;

					if (intag)
					{
						seg.text += ch;

						if (ch == '>')
						{
							if (diff.operation != DiffMatchPatch.Operation.DELETE)
							{
								segs.Add(seg);
								seg = new DiffSeg();
							}
							intag = false;
						}
					}
					else
					{
						if (ch != '<')
						{
							seg.text += ch;
						}
						else
						{
							intag = true;

							if (seg.text != "")
								segs.Add(seg);

							seg = new DiffSeg
							{
								startTag = true,
								tag = true,
								operation = diff.operation,
								text = "<"
							};
							//See if this is a closing tag
							for (var chii = chi + 1; chii < diff.text.Length; chii++)
							{
								var dchi = diff.text.ElementAt(chii);
								if (dchi == ' ')
									continue;
								else if (dchi == '/')
								{
									seg.startTag = false;
									//Skip all these spaces and the /, since the self closing code will hickup if we don't.
									chi = chii;
									seg.text += '/';
								}
								//Any non-space char should make break
								break;
							}
						}
					}
				}
				if (!intag)
				{
					if (seg.text != "")
						segs.Add(seg);
					seg = new DiffSeg();
				}
			}

			//TODO:  You may have thought we were done with this mess.  But not really, need to do some post processing somewhere for this case:
			//BEFORE: Something<li>Content</li><li>Content2</li>Other Content
			//After:  Something<li>Content2</li>Other Content
			//Should be possible if we can determine depth and find the beginning tag for removed ending tags.  Same for added tags.  Good luck with the rest, you get to figure it out. :)

			return segs;
		}

		public string diffOutput(List<DiffMatchPatch.Diff> diffs)
		{
			var segs = segsForDiffs(diffs);

			var output = "";

			var depth = 0;

			for (var i = 0; i < segs.Count; i++)
			{
				var markText = "";
				var seg = segs.ElementAt(i);
				var startDepth = depth;

				if (!seg.tag)
				{
					markText += seg.text;
				}
				//This will only occur if there is an end tag that does not have a proceding start tag
				else if (!seg.startTag)
				{
					depth--;

					//There was a closing tag with no starting, so we include all the text in the line so far.
					if (depth < startDepth)
					{
						output += formatter.textForChange(markText, seg.operation);
						startDepth = depth;
					}

					if (seg.operation != DiffMatchPatch.Operation.DELETE)
						output += seg.text;
				}
				else
				{
					depth++;

					var res = textForTagSeg(segs, i);

					i = res.Item1 - 1;//-1 to account for that it's about to be incremented

					output += formatter.textForChange(markText, seg.operation);
					markText = "";
					output += res.Item2;
				}

				if (markText != "")
				{
					output += formatter.textForChange(markText, seg.operation);
					markText = "";
				}
			}

			return output;
		}
		/**
		 * A new seg index, and the elements content
		 **/
		protected Tuple<int, string> textForTagSeg(List<DiffSeg> segs, int si)
		{
			var seg = segs.ElementAt(si);
			var elementContent = seg.text;
			var i2 = si + 1;
			var depth = 1;

			Nullable<DiffMatchPatch.Operation> innerOp = null;

			//TODO:  If needed, we can do something with this.  For now, lets keep it simple.
			//List<DiffSeg> after = new List<DiffSeg>();
			for (; i2 < segs.Count && depth > 0; i2++)
			{
				var tSeg = segs.ElementAt(i2);
				if (innerOp == null)
					innerOp = tSeg.operation;

				if (tSeg.tag)
					depth += tSeg.startTag ? 1 : -1;

				if (tSeg.operation != innerOp && depth != 0)
				{
					if (elementContent != seg.text)
					{
						return new Tuple<int, string>(i2, formatter.textForChange(elementContent, seg.operation));
					}
					else
						break;
				}
				else
				{
					elementContent += tSeg.text;
				}
			}

			if (depth == 0)
			{
				return new Tuple<int, string>(i2 - 1, formatter.textForChange(elementContent, innerOp.HasValue ? innerOp.Value : seg.operation));
			}
			//Should only happen in invalid HTML anyway?
			else
			{
				return new Tuple<int, string>(si + 1, seg.operation == DiffMatchPatch.Operation.DELETE ? "" : seg.text);
			}
		}
	}
}