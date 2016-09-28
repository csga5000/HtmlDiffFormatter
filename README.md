# HtmlDiffFormatter
Takes a diff output on html strings from googles diff match patch library and attempts to render a nice diff that is valid HTML and can be viewed as such

## Requirements
Requires google diff match patch.  I took the liberty of including it in this project because it's difficult to download of google code these days.  Feel free to replace it yourself if you like.

## Setup
Just include the class files somewhere in your project.  You can decide how to want to handle that.

Then do something like this:

```c#
using csga5000.HtmlDiffFomratter;

//... a class or something

var dmp = new DiffMatchPatch.diff_match_patch();
var startText = "<h1>Some html</h2>";
var endText = "<h2>Some new html</h2>";

var diffs = dmp.diff_main(startText, endText);

var output = (new HtmlDiffFormatter()).diffOutput(diffs);

//... rest of the class

```

In this example, the output would be: 
```html
<h2>Some<ins style="text-decoration: underline;color: green;"> new</ins> html</h2>
```

## Customizing the output

It should be pretty easy to change the formatting however you wish.
```c#
public class ColorOnlyFormatter : HtmlDiffFormatter.Formatter
{
	override protected string textForChange(string text, DiffMatchPatch.Operation op)
	{
		if (text == "" || text == null)
			return "";

		switch (op)
		{
			case DiffMatchPatch.Operation.DELETE:
				return "<del style=\"color: red;\">" + text + "</del>";
			case DiffMatchPatch.Operation.INSERT:
				return "<ins style=\"color: blue;\">" + text + "</ins>";
			default:
				return text;
		}
	}
}

//...
var dmp = new DiffMatchPatch.diff_match_patch();
var startText = "<h1>Some html toremove</h2>";
var endText = "<h2>Some new html</h2>";

var diffs = dmp.diff_main(startText, endText);

var output = (new HtmlDiffFormatter(new ColorOnlyFormatter())).diffOutput(diffs);
//...
```

Output in this case would be:
```html
<h2>Some <span style="color: blue;">new </span>html<span style="color: red;"> toremove</span></h2>
```

## Problems

If you have some issues feel free to delve into my code and make alterations.  If they're good changes, please do make a merge request!  If you have an issue you may also report it here, I may address it, but likely only if it affects my projects using this code.  Otherwise, it's up to the community - either you or someone else can try to tackle it.

I have lots of comments explaining what's going on (for my sanity as much as to help you) so it shouldn't be THAT hard to understand what it's doing.

## Copyrights and Liscensing crap

I include google diff match patch.  It uses this lisence: http://www.apache.org/licenses/LICENSE-2.0
At the time of writing I have not modified their code, but I reserve that I may change it in the future.  Check the file's commit history to ensure I have not modified it since.

Asside from the licensing of code I included in this project, you may use/modify/distribute the code however you like.  I'm not responsible for what you do with it.  I make no guarantees as to code quality.
