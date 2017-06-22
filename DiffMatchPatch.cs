/*
 * Copyright 2008 Google Inc. All Rights Reserved.
 * Author: fraser@google.com (Neil Fraser)
 * Author: anteru@developer.shelter13.net (Matthaeus G. Chajdas)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Diff Match and Patch
 * http://code.google.com/p/google-diff-match-patch/
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace DiffMatchPatch
{
    internal static class CompatibilityExtensions
    {
        // JScript splice function
        public static List<T> Splice<T>(this List<T> input, int start, int count,
            params T[] objects)
        {
            List<T> deletedRange = input.GetRange(start, count);
            input.RemoveRange(start, count);
            input.InsertRange(start, objects);

            return deletedRange;
        }
    }

    /**-
     * The data structure representing a diff is a List of Diff objects:
     * {Diff<T>(Operation.DELETE, "Hello"), Diff<T>(Operation.INSERT, "Goodbye"),
     *  Diff<T>(Operation.EQUAL, " world.")}
     * which means: delete "Hello", add "Goodbye" and keep " world."
     */
    public enum Operation
    {
        DELETE, INSERT, EQUAL
    }

    /**
     * Class representing one diff operation.
     */
    public class Diff<T>
    {
        public Operation operation;
        // One of: INSERT, DELETE or EQUAL.
        public List<Symbol<T>> text;
        // The text associated with this diff operation.

        /**
         * Constructor.  Initializes the diff with the provided values.
         * @param operation One of INSERT, DELETE or EQUAL.
         * @param text The text being applied.
         */
        public Diff(Operation operation, List<Symbol<T>> text)
        {
            // Construct a diff with the specified operation and text.
            this.operation = operation;
            this.text = text;
        }

        /**
         * Display a human-readable version of this Diff.
         * @return text version.
         */
        public override string ToString()
        {
            return "Diff<T>(" + this.operation + ",\"" + text.ToString() + "\")";
        }

        /**
         * Is this Diff equivalent to another Diff?
         * @param d Another Diff to compare against.
         * @return true or false.
         */
        public override bool Equals(Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Diff return false.
            Diff<T> p = obj as Diff<T>;
            if ((System.Object)p == null)
            {
                return false;
            }

            // Return true if the fields match.
            return this.Equals(p);
        }

        public bool Equals(Diff<T> obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }
            if (obj.text == null && this.text == null)
                return true;
            else if (obj.text == null || this.text == null)
                return false;

            // Return true if the fields match.
            return obj.operation == this.operation &&
                obj.text.IndexOf(this.text) == 0 &&
                obj.text.Count == this.text.Count;
        }

        public override int GetHashCode()
        {
            return text.GetHashCode() ^ operation.GetHashCode();
        }
    }


    /**
     * Class representing one Patch<T> operation.
     */
    public class Patch<T>
    {
        public List<Diff<T>> diffs = new List<Diff<T>>();
        public int start1;
        public int start2;
        public int length1;
        public int length2;

        public override string ToString()
        {
            return ToString(new TextSymbolReader<T>());
        }

        /**
         * Emmulate GNU diff's format.
         * Header: @@ -382,8 +481,9 @@
         * Indicies are printed as 1-based, not 0-based.
         * @return The GNU diff string.
         */
        public string ToString(SymbolTextReader<T> reader)
        {
            string coords1, coords2;
            if (this.length1 == 0)
            {
                coords1 = this.start1 + ",0";
            }
            else if (this.length1 == 1)
            {
                coords1 = Convert.ToString(this.start1 + 1);
            }
            else
            {
                coords1 = (this.start1 + 1) + "," + this.length1;
            }
            if (this.length2 == 0)
            {
                coords2 = this.start2 + ",0";
            }
            else if (this.length2 == 1)
            {
                coords2 = Convert.ToString(this.start2 + 1);
            }
            else
            {
                coords2 = (this.start2 + 1) + "," + this.length2;
            }
            StringBuilder text = new StringBuilder();
            text.Append("@@ -").Append(coords1).Append(" +").Append(coords2)
                .Append(" @@\n");
            // Escape the body of the Patch<T> with %xx notation.
            foreach (Diff<T> aDiff in this.diffs)
            {
                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        text.Append('+');
                        break;
                    case Operation.DELETE:
                        text.Append('-');
                        break;
                    case Operation.EQUAL:
                        text.Append(' ');
                        break;
                }

                text.Append(HttpUtility.UrlEncode(reader.TextFromSymbols(aDiff.text), new UTF8Encoding())).Append("\n");
            }

            return diff_match_patch<T>.unescapeForEncodeUriCompatability(text.ToString());
        }
    }


    /**
     * Class containing the diff, match and Patch<T> methods.
     * Also Contains the behaviour settings.
     */
    public class diff_match_patch<T>
    {
        // Defaults.
        // Set these on your diff_match_Patch<T> instance to override the defaults.

        // Number of seconds to map a diff before giving up (0 for infinity).
        public float Diff_Timeout = 1.0f;
        // Cost of an empty edit operation in terms of edit characters.
        public short Diff_EditCost = 4;
        // At what point is no match declared (0.0 = perfection, 1.0 = very loose).
        public float Match_Threshold = 0.5f;
        // How far to search for a match (0 = exact location, 1000+ = broad match).
        // A match this many characters away from the expected location will add
        // 1.0 to the score (0.0 is a perfect match).
        public int Match_Distance = 1000;
        // When deleting a large block of text (over ~64 characters), how close
        // do the contents have to be to match the expected contents. (0.0 =
        // perfection, 1.0 = very loose).  Note that Match_Threshold controls
        // how closely the end points of a delete need to match.
        public float Patch_DeleteThreshold = 0.5f;
        // Chunk size for context length.
        public short Patch_Margin = 4;

        // The number of bits in an int.
        private short Match_MaxBits = 32;


        //  DIFF FUNCTIONS

        /**
         * Find the differences between two texts.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param checklines Speedup flag.  If false, then don't run a
         *     line-level diff first to identify the changed areas.
         *     If true, then run a faster slightly less optimal diff.
         * @return List of Diff objects.
         */
        public List<Diff<T>> diff_main(List<Symbol<T>> text1, List<Symbol<T>> text2)
        {
            // Set a deadline by which time the diff must be complete.
            DateTime deadline;
            if (this.Diff_Timeout <= 0)
            {
                deadline = DateTime.MaxValue;
            }
            else
            {
                deadline = DateTime.Now + new TimeSpan(((long)(Diff_Timeout * 1000)) * 10000);
            }
            return diff_main(text1, text2, deadline);
        }

        /**
         * Find the differences between two texts.  Simplifies the problem by
         * stripping any common prefix or suffix off the texts before diffing.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param checklines Speedup flag.  If false, then don't run a
         *     line-level diff first to identify the changed areas.
         *     If true, then run a faster slightly less optimal diff.
         * @param deadline Time when the diff should be complete by.  Used
         *     internally for recursive calls.  Users should set DiffTimeout
         *     instead.
         * @return List of Diff objects.
         */
        private List<Diff<T>> diff_main(List<Symbol<T>> text1, List<Symbol<T>> text2, DateTime deadline)
        {
            // Check for null inputs not needed since null can't be passed in C#.

            // Check for equality (speedup).
            List<Diff<T>> diffs;
            if (text1.SequenceEqual(text2))
            {
                diffs = new List<Diff<T>>();
                if (text1.Count != 0)
                {
                    diffs.Add(new Diff<T>(Operation.EQUAL, text1));
                }
                return diffs;
            }

            // Trim off common prefix (speedup).
            int commonlength = diff_commonPrefix(text1, text2);
            List<Symbol<T>> commonprefix = text1.GetRange(0, commonlength);
            text1 = text1.RangeFrom(commonlength);
            text2 = text2.RangeFrom(commonlength);

            // Trim off common suffix (speedup).
            commonlength = diff_commonSuffix(text1, text2);
            List<Symbol<T>> commonsuffix = text1.GetRange(text1.Count - commonlength, commonlength);
            text1 = text1.GetRange(0, text1.Count - commonlength);
            text2 = text2.GetRange(0, text2.Count - commonlength);

            // Compute the diff on the middle block.
            diffs = diff_compute(text1, text2, deadline);

            // Restore the prefix and suffix.
            if (commonprefix.Count != 0)
            {
                diffs.Insert(0, (new Diff<T>(Operation.EQUAL, commonprefix)));
            }
            if (commonsuffix.Count != 0)
            {
                diffs.Add(new Diff<T>(Operation.EQUAL, commonsuffix));
            }

            diff_cleanupMerge(diffs);
            return diffs;
        }

        /**
         * Find the differences between two texts.  Assumes that the texts do not
         * have any common prefix or suffix.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param checklines Speedup flag.  If false, then don't run a
         *     line-level diff first to identify the changed areas.
         *     If true, then run a faster slightly less optimal diff.
         * @param deadline Time when the diff should be complete by.
         * @return List of Diff objects.
         */
        private List<Diff<T>> diff_compute(List<Symbol<T>> text1, List<Symbol<T>> text2, DateTime deadline)
        {
            List<Diff<T>> diffs = new List<Diff<T>>();

            if (text1.Count == 0)
            {
                // Just add some text (speedup).
                diffs.Add(new Diff<T>(Operation.INSERT, text2));
                return diffs;
            }

            if (text2.Count == 0)
            {
                // Just delete some text (speedup).
                diffs.Add(new Diff<T>(Operation.DELETE, text1));
                return diffs;
            }

            List<Symbol<T>> longtext = text1.Count > text2.Count ? text1 : text2;
            List<Symbol<T>> shorttext = text1.Count > text2.Count ? text2 : text1;
            int i = longtext.IndexOf(shorttext);
            if (i != -1)
            {
                // Shorter text is inside the longer text (speedup).
                Operation op = (text1.Count > text2.Count) ?
                    Operation.DELETE : Operation.INSERT;
                diffs.Add(new Diff<T>(op, longtext.GetRange(0, i)));
                diffs.Add(new Diff<T>(Operation.EQUAL, shorttext));
                diffs.Add(new Diff<T>(op, longtext.RangeFrom(i + shorttext.Count)));
                return diffs;
            }

            if (shorttext.Count == 1)
            {
                // Single character string.
                // After the previous speedup, the character can't be an equality.
                diffs.Add(new Diff<T>(Operation.DELETE, text1));
                diffs.Add(new Diff<T>(Operation.INSERT, text2));
                return diffs;
            }

            // Check to see if the problem can be split in two.
            List<Symbol<T>>[] hm = diff_halfMatch(text1, text2);
            if (hm != null)
            {
                // A half-match was found, sort out the return data.
                List<Symbol<T>> text1_a = hm[0];
                List<Symbol<T>> text1_b = hm[1];
                List<Symbol<T>> text2_a = hm[2];
                List<Symbol<T>> text2_b = hm[3];
                List<Symbol<T>> mid_common = hm[4];
                // Send both pairs off for separate processing.
                List<Diff<T>> diffs_a = diff_main(text1_a, text2_a, deadline);
                List<Diff<T>> diffs_b = diff_main(text1_b, text2_b, deadline);
                // Merge the results.
                diffs = diffs_a;
                diffs.Add(new Diff<T>(Operation.EQUAL, mid_common));
                diffs.AddRange(diffs_b);
                return diffs;
            }

            return diff_bisect(text1, text2, deadline);
        }

        /**
         * Find the 'middle snake' of a diff, split the problem in two
         * and return the recursively constructed diff.
         * See Myers 1986 paper: An O(ND) Difference Algorithm and Its Variations.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param deadline Time at which to bail if not yet complete.
         * @return List of Diff objects.
         */
        protected List<Diff<T>> diff_bisect(List<Symbol<T>> text1, List<Symbol<T>> text2, DateTime deadline)
        {
            // Cache the text lengths to prevent multiple calls.
            int text1_length = text1.Count;
            int text2_length = text2.Count;
            int max_d = (text1_length + text2_length + 1) / 2;
            int v_offset = max_d;
            int v_length = 2 * max_d;
            int[] v1 = new int[v_length];
            int[] v2 = new int[v_length];
            for (int x = 0; x < v_length; x++)
            {
                v1[x] = -1;
                v2[x] = -1;
            }
            v1[v_offset + 1] = 0;
            v2[v_offset + 1] = 0;
            int delta = text1_length - text2_length;
            // If the total number of characters is odd, then the front path will
            // collide with the reverse path.
            bool front = (delta % 2 != 0);
            // Offsets for start and end of k loop.
            // Prevents mapping of space beyond the grid.
            int k1start = 0;
            int k1end = 0;
            int k2start = 0;
            int k2end = 0;
            for (int d = 0; d < max_d; d++)
            {
                // Bail out if deadline is reached.
                if (DateTime.Now > deadline)
                {
                    break;
                }

                // Walk the front path one step.
                for (int k1 = -d + k1start; k1 <= d - k1end; k1 += 2)
                {
                    int k1_offset = v_offset + k1;
                    int x1;
                    if (k1 == -d || k1 != d && v1[k1_offset - 1] < v1[k1_offset + 1])
                    {
                        x1 = v1[k1_offset + 1];
                    }
                    else
                    {
                        x1 = v1[k1_offset - 1] + 1;
                    }
                    int y1 = x1 - k1;
                    while (x1 < text1_length && y1 < text2_length
                          && text1[x1] == text2[y1])
                    {
                        x1++;
                        y1++;
                    }
                    v1[k1_offset] = x1;
                    if (x1 > text1_length)
                    {
                        // Ran off the right of the graph.
                        k1end += 2;
                    }
                    else if (y1 > text2_length)
                    {
                        // Ran off the bottom of the graph.
                        k1start += 2;
                    }
                    else if (front)
                    {
                        int k2_offset = v_offset + delta - k1;
                        if (k2_offset >= 0 && k2_offset < v_length && v2[k2_offset] != -1)
                        {
                            // Mirror x2 onto top-left coordinate system.
                            int x2 = text1_length - v2[k2_offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return diff_bisectSplit(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }

                // Walk the reverse path one step.
                for (int k2 = -d + k2start; k2 <= d - k2end; k2 += 2)
                {
                    int k2_offset = v_offset + k2;
                    int x2;
                    if (k2 == -d || k2 != d && v2[k2_offset - 1] < v2[k2_offset + 1])
                    {
                        x2 = v2[k2_offset + 1];
                    }
                    else
                    {
                        x2 = v2[k2_offset - 1] + 1;
                    }
                    int y2 = x2 - k2;
                    while (x2 < text1_length && y2 < text2_length
                        && text1[text1_length - x2 - 1]
                        == text2[text2_length - y2 - 1])
                    {
                        x2++;
                        y2++;
                    }
                    v2[k2_offset] = x2;
                    if (x2 > text1_length)
                    {
                        // Ran off the left of the graph.
                        k2end += 2;
                    }
                    else if (y2 > text2_length)
                    {
                        // Ran off the top of the graph.
                        k2start += 2;
                    }
                    else if (!front)
                    {
                        int k1_offset = v_offset + delta - k2;
                        if (k1_offset >= 0 && k1_offset < v_length && v1[k1_offset] != -1)
                        {
                            int x1 = v1[k1_offset];
                            int y1 = v_offset + x1 - k1_offset;
                            // Mirror x2 onto top-left coordinate system.
                            x2 = text1_length - v2[k2_offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return diff_bisectSplit(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }
            }
            // Diff took too long and hit the deadline or
            // number of diffs equals number of characters, no commonality at all.
            List<Diff<T>> diffs = new List<Diff<T>>();
            diffs.Add(new Diff<T>(Operation.DELETE, text1));
            diffs.Add(new Diff<T>(Operation.INSERT, text2));
            return diffs;
        }

        /**
         * Given the location of the 'middle snake', split the diff in two parts
         * and recurse.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param x Index of split point in text1.
         * @param y Index of split point in text2.
         * @param deadline Time at which to bail if not yet complete.
         * @return LinkedList of Diff objects.
         */
        private List<Diff<T>> diff_bisectSplit(List<Symbol<T>> text1, List<Symbol<T>> text2,
            int x, int y, DateTime deadline)
        {
            List<Symbol<T>> text1a = text1.GetRange(0, x);
            List<Symbol<T>> text2a = text2.GetRange(0, y);
            List<Symbol<T>> text1b = text1.RangeFrom(x);
            List<Symbol<T>> text2b = text2.RangeFrom(y);

            // Compute both diffs serially.
            List<Diff<T>> diffs = diff_main(text1a, text2a, deadline);
            List<Diff<T>> diffsb = diff_main(text1b, text2b, deadline);

            diffs.AddRange(diffsb);
            return diffs;
        }

        /**
         * Determine the common prefix of two strings.
         * @param text1 First string.
         * @param text2 Second string.
         * @return The number of characters common to the start of each string.
         */
        public int diff_commonPrefix(List<Symbol<T>> text1, List<Symbol<T>> text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            int n = Math.Min(text1.Count, text2.Count);
            for (int i = 0; i < n; i++)
            {
                if (text1[i] != text2[i])
                {
                    return i;
                }
            }
            return n;
        }

        /**
         * Determine the common suffix of two strings.
         * @param text1 First string.
         * @param text2 Second string.
         * @return The number of characters common to the end of each string.
         */
        public int diff_commonSuffix(List<Symbol<T>> text1, List<Symbol<T>> text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            int text1_length = text1.Count;
            int text2_length = text2.Count;
            int n = Math.Min(text1.Count, text2.Count);
            for (int i = 1; i <= n; i++)
            {
                if (text1[text1_length - i] != text2[text2_length - i])
                {
                    return i - 1;
                }
            }
            return n;
        }

        /**
         * Determine if the suffix of one string is the prefix of another.
         * @param text1 First string.
         * @param text2 Second string.
         * @return The number of characters common to the end of the first
         *     string and the start of the second string.
         */
        protected int diff_commonOverlap(List<Symbol<T>> text1, List<Symbol<T>> text2)
        {
            // Cache the text lengths to prevent multiple calls.
            int text1_length = text1.Count;
            int text2_length = text2.Count;
            // Eliminate the null case.
            if (text1_length == 0 || text2_length == 0)
            {
                return 0;
            }
            // Truncate the longer string.
            if (text1_length > text2_length)
            {
                text1 = text1.RangeFrom(text1_length - text2_length);
            }
            else if (text1_length < text2_length)
            {
                text2 = text2.GetRange(0, text1_length);
            }
            int text_length = Math.Min(text1_length, text2_length);
            // Quick check for the worst case.
            if (text1.SequenceEqual(text2))
            {
                return text_length;
            }

            // Start by looking for a single character match
            // and increase length until no match is found.
            // Performance analysis: http://neil.fraser.name/news/2010/11/04/
            int best = 0;
            int length = 1;
            while (true)
            {
                List<Symbol<T>> pattern = text1.RangeFrom(text_length - length);
                int found = text2.IndexOf(pattern);
                if (found == -1)
                {
                    return best;
                }
                length += found;
                if (found == 0 || text1.RangeFrom(text_length - length) == text2.GetRange(0, length))
                {
                    best = length;
                    length++;
                }
            }
        }

        /**
         * Do the two texts share a Substring which is at least half the length of
         * the longer text?
         * This speedup can produce non-minimal diffs.
         * @param text1 First string.
         * @param text2 Second string.
         * @return Five element String array, containing the prefix of text1, the
         *     suffix of text1, the prefix of text2, the suffix of text2 and the
         *     common middle.  Or null if there was no match.
         */

        protected List<Symbol<T>>[] diff_halfMatch(List<Symbol<T>> text1, List<Symbol<T>> text2)
        {
            if (Diff_Timeout <= 0)
            {
                // Don't risk returning a non-optimal diff if we have unlimited time.
                return null;
            }
            List<Symbol<T>> longtext = text1.Count > text2.Count ? text1 : text2;
            List<Symbol<T>> shorttext = text1.Count > text2.Count ? text2 : text1;
            if (longtext.Count < 4 || shorttext.Count * 2 < longtext.Count)
            {
                return null;  // Pointless.
            }

            // First check if the second quarter is the seed for a half-match.
            List<Symbol<T>>[] hm1 = diff_halfMatchI(longtext, shorttext, (longtext.Count + 3) / 4);
            // Check again based on the third quarter.
            List<Symbol<T>>[] hm2 = diff_halfMatchI(longtext, shorttext, (longtext.Count + 1) / 2);
            List<Symbol<T>>[] hm;
            if (hm1 == null && hm2 == null)
            {
                return null;
            }
            else if (hm2 == null)
            {
                hm = hm1;
            }
            else if (hm1 == null)
            {
                hm = hm2;
            }
            else
            {
                // Both matched.  Select the longest.
                hm = hm1[4].Count > hm2[4].Count ? hm1 : hm2;
            }

            // A half-match was found, sort out the return data.
            if (text1.Count > text2.Count)
            {
                return hm;
                //return new string[]{hm[0], hm[1], hm[2], hm[3], hm[4]};
            }
            else
            {
                return new List<Symbol<T>>[] { hm[2], hm[3], hm[0], hm[1], hm[4] };
            }
        }

        /**
         * Does a Substring of shorttext exist within longtext such that the
         * Substring is at least half the length of longtext?
         * @param longtext Longer string.
         * @param shorttext Shorter string.
         * @param i Start index of quarter length Substring within longtext.
         * @return Five element string array, containing the prefix of longtext, the
         *     suffix of longtext, the prefix of shorttext, the suffix of shorttext
         *     and the common middle.  Or null if there was no match.
         */
        private List<Symbol<T>>[] diff_halfMatchI(List<Symbol<T>> longtext, List<Symbol<T>> shorttext, int i)
        {
            // Start with a 1/4 length Substring at position i as a seed.
            List<Symbol<T>> seed = longtext.GetRange(i, longtext.Count / 4);
            int j = -1;
            List<Symbol<T>> best_common = Symbol<T>.EmptyList;
            List<Symbol<T>> best_longtext_a = Symbol<T>.EmptyList, best_longtext_b = Symbol<T>.EmptyList;
            List<Symbol<T>> best_shorttext_a = Symbol<T>.EmptyList, best_shorttext_b = Symbol<T>.EmptyList;
            while (j < shorttext.Count && (j = shorttext.IndexOf(seed, j + 1)) != -1)
            {
                int prefixLength = diff_commonPrefix(longtext.RangeFrom(i), shorttext.RangeFrom(j));
                int suffixLength = diff_commonSuffix(longtext.GetRange(0, i), shorttext.GetRange(0, j));
                if (best_common.Count < suffixLength + prefixLength)
                {
                    best_common = shorttext.GetRange(j - suffixLength, suffixLength).Concat(shorttext.GetRange(j, prefixLength)).ToList();
                    best_longtext_a = longtext.GetRange(0, i - suffixLength);
                    best_longtext_b = longtext.RangeFrom(i + prefixLength);
                    best_shorttext_a = shorttext.GetRange(0, j - suffixLength);
                    best_shorttext_b = shorttext.RangeFrom(j + prefixLength);
                }
            }
            if (best_common.Count * 2 >= longtext.Count)
                return new List<Symbol<T>>[]{best_longtext_a, best_longtext_b, best_shorttext_a, best_shorttext_b, best_common};
            else
                return null;
        }

        /**
         * Reduce the number of edits by eliminating semantically trivial
         * equalities.
         * @param diffs List of Diff objects.
         */
        public void diff_cleanupSemantic(List<Diff<T>> diffs)
        {
            bool changes = false;
            // Stack of indices where equalities are found.
            Stack<int> equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            List<Symbol<T>> lastequality = null;
            int pointer = 0;  // Index of current position.
                              // Number of characters that changed prior to the equality.
            int length_insertions1 = 0;
            int length_deletions1 = 0;
            // Number of characters that changed after the equality.
            int length_insertions2 = 0;
            int length_deletions2 = 0;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].operation == Operation.EQUAL)
                {  // Equality found.
                    equalities.Push(pointer);
                    length_insertions1 = length_insertions2;
                    length_deletions1 = length_deletions2;
                    length_insertions2 = 0;
                    length_deletions2 = 0;
                    lastequality = diffs[pointer].text;
                }
                else
                {  // an insertion or deletion
                    if (diffs[pointer].operation == Operation.INSERT)
                    {
                        length_insertions2 += diffs[pointer].text.Count;
                    }
                    else
                    {
                        length_deletions2 += diffs[pointer].text.Count;
                    }
                    // Eliminate an equality that is smaller or equal to the edits on both
                    // sides of it.
                    if (lastequality != null && (lastequality.Count
                        <= Math.Max(length_insertions1, length_deletions1))
                        && (lastequality.Count
                            <= Math.Max(length_insertions2, length_deletions2)))
                    {
                        // Duplicate record.
                        diffs.Insert(equalities.Peek(),
                                     new Diff<T>(Operation.DELETE, lastequality));
                        // Change second copy to insert.
                        diffs[equalities.Peek() + 1].operation = Operation.INSERT;
                        // Throw away the equality we just deleted.
                        equalities.Pop();
                        if (equalities.Count > 0)
                        {
                            equalities.Pop();
                        }
                        pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                        length_insertions1 = 0;  // Reset the counters.
                        length_deletions1 = 0;
                        length_insertions2 = 0;
                        length_deletions2 = 0;
                        lastequality = null;
                        changes = true;
                    }
                }
                pointer++;
            }

            // Normalize the diff.
            if (changes)
            {
                diff_cleanupMerge(diffs);
            }
            diff_cleanupSemanticLossless(diffs);

            // Find any overlaps between deletions and insertions.
            // e.g: <del>abcxxx</del><ins>xxxdef</ins>
            //   -> <del>abc</del>xxx<ins>def</ins>
            // e.g: <del>xxxabc</del><ins>defxxx</ins>
            //   -> <ins>def</ins>xxx<del>abc</del>
            // Only extract an overlap if it is as big as the edit ahead or behind it.
            pointer = 1;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer - 1].operation == Operation.DELETE &&
                    diffs[pointer].operation == Operation.INSERT)
                {
                    List<Symbol<T>> deletion = diffs[pointer - 1].text;
                    List<Symbol<T>> insertion = diffs[pointer].text;
                    int overlap_length1 = diff_commonOverlap(deletion, insertion);
                    int overlap_length2 = diff_commonOverlap(insertion, deletion);
                    if (overlap_length1 >= overlap_length2)
                    {
                        if (overlap_length1 >= deletion.Count / 2.0 ||
                            overlap_length1 >= insertion.Count / 2.0)
                        {
                            // Overlap found.
                            // Insert an equality and trim the surrounding edits.
                            diffs.Insert(pointer, new Diff<T>(Operation.EQUAL, insertion.GetRange(0, overlap_length1)));
                            diffs[pointer - 1].text = deletion.GetRange(0, deletion.Count - overlap_length1);
                            diffs[pointer + 1].text = insertion.RangeFrom(overlap_length1);
                            pointer++;
                        }
                    }
                    else
                    {
                        if (overlap_length2 >= deletion.Count / 2.0 || overlap_length2 >= insertion.Count / 2.0)
                        {
                            // Reverse overlap found.
                            // Insert an equality and swap and trim the surrounding edits.
                            diffs.Insert(pointer, new Diff<T>(Operation.EQUAL, deletion.GetRange(0, overlap_length2)));
                            diffs[pointer - 1].operation = Operation.INSERT;
                            diffs[pointer - 1].text = insertion.GetRange(0, insertion.Count - overlap_length2);
                            diffs[pointer + 1].operation = Operation.DELETE;
                            diffs[pointer + 1].text = deletion.RangeFrom(overlap_length2);
                            pointer++;
                        }
                    }
                    pointer++;
                }
                pointer++;
            }
        }

        /**
         * Look for single edits surrounded on both sides by equalities
         * which can be shifted sideways to align the edit to a word boundary.
         * e.g: The c<ins>at c</ins>ame. -> The <ins>cat </ins>came.
         * @param diffs List of Diff objects.
         */
        public void diff_cleanupSemanticLossless(List<Diff<T>> diffs)
        {
            int pointer = 1;
            // Intentionally ignore the first and last element (don't need checking).
            while (pointer < diffs.Count - 1)
            {
                if (diffs[pointer - 1].operation == Operation.EQUAL && diffs[pointer + 1].operation == Operation.EQUAL)
                {
                    // This is a single edit surrounded by equalities.
                    List<Symbol<T>> equality1 = diffs[pointer - 1].text;
                    List<Symbol<T>> edit = diffs[pointer].text;
                    List<Symbol<T>> equality2 = diffs[pointer + 1].text;

                    // First, shift the edit as far left as possible.
                    int commonOffset = this.diff_commonSuffix(equality1, edit);
                    if (commonOffset > 0)
                    {
                        List<Symbol<T>> commonString = edit.RangeFrom(edit.Count - commonOffset);
                        equality1 = equality1.GetRange(0, equality1.Count - commonOffset);
                        edit = commonString.Concat(edit.GetRange(0, edit.Count - commonOffset)).ToList();
                        equality2 = commonString.Concat(equality2).ToList();
                    }

                    // Second, step character by character right,
                    // looking for the best fit.
                    List<Symbol<T>> bestEquality1 = equality1;
                    List<Symbol<T>> bestEdit = edit;
                    List<Symbol<T>> bestEquality2 = equality2;
                    int bestScore = diff_cleanupSemanticScore(equality1, edit) + diff_cleanupSemanticScore(edit, equality2);
                    while (edit.Count != 0 && equality2.Count != 0
                        && edit[0] == equality2[0])
                    {
                        equality1.Add(edit[0]);
                        edit = edit.RangeFrom(1);
                        edit.Add(equality2[0]);
                        equality2 = equality2.RangeFrom(1);
                        int score = diff_cleanupSemanticScore(equality1, edit) + diff_cleanupSemanticScore(edit, equality2);
                        // The >= encourages trailing rather than leading whitespace on
                        // edits.
                        if (score >= bestScore)
                        {
                            bestScore = score;
                            bestEquality1 = equality1;
                            bestEdit = edit;
                            bestEquality2 = equality2;
                        }
                    }

                    if (diffs[pointer - 1].text != bestEquality1)
                    {
                        // We have an improvement, save it back to the diff.
                        if (bestEquality1.Count != 0)
                        {
                            diffs[pointer - 1].text = bestEquality1;
                        }
                        else
                        {
                            diffs.RemoveAt(pointer - 1);
                            pointer--;
                        }
                        diffs[pointer].text = bestEdit;
                        if (bestEquality2.Count != 0)
                        {
                            diffs[pointer + 1].text = bestEquality2;
                        }
                        else
                        {
                            diffs.RemoveAt(pointer + 1);
                            pointer--;
                        }
                    }
                }
                pointer++;
            }
        }

        /**
         * Given two strings, comAdde a score representing whether the internal
         * boundary falls on logical boundaries.
         * Scores range from 6 (best) to 0 (worst).
         * @param one First string.
         * @param two Second string.
         * @return The score.
         */
        private int diff_cleanupSemanticScore(List<Symbol<T>> one, List<Symbol<T>> two)
        {
            if (one.Count == 0 || two.Count == 0)
            {
                // Edges are the best.
                return 6;
            }

            // Each port of this function behaves slightly differently due to
            // subtle differences in each language's definition of things like
            // 'whitespace'.  Since this function's purpose is largely cosmetic,
            // the choice has been made to use each language's native features
            // rather than force total conformity.
            Symbol<T> char1 = one[one.Count - 1];
            Symbol<T> char2 = two[0];

            return char1.BoundaryScore(char2);
        }

        /**
         * Reduce the number of edits by eliminating operationally trivial
         * equalities.
         * @param diffs List of Diff objects.
         */
        public void diff_cleanupEfficiency(List<Diff<T>> diffs)
        {
            bool changes = false;
            // Stack of indices where equalities are found.
            Stack<int> equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            List<Symbol<T>> lastequality = Symbol<T>.EmptyList;
            int pointer = 0;  // Index of current position.
                              // Is there an insertion operation before the last equality.
            bool pre_ins = false;
            // Is there a deletion operation before the last equality.
            bool pre_del = false;
            // Is there an insertion operation after the last equality.
            bool post_ins = false;
            // Is there a deletion operation after the last equality.
            bool post_del = false;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].operation == Operation.EQUAL)
                {  // Equality found.
                    if (diffs[pointer].text.Count < this.Diff_EditCost
                        && (post_ins || post_del))
                    {
                        // Candidate found.
                        equalities.Push(pointer);
                        pre_ins = post_ins;
                        pre_del = post_del;
                        lastequality = diffs[pointer].text;
                    }
                    else
                    {
                        // Not a candidate, and can never become one.
                        equalities.Clear();
                        lastequality = Symbol<T>.EmptyList;
                    }
                    post_ins = post_del = false;
                }
                else
                {  // An insertion or deletion.
                    if (diffs[pointer].operation == Operation.DELETE)
                    {
                        post_del = true;
                    }
                    else
                    {
                        post_ins = true;
                    }
                    /*
                     * Five types to be split:
                     * <ins>A</ins><del>B</del>XY<ins>C</ins><del>D</del>
                     * <ins>A</ins>X<ins>C</ins><del>D</del>
                     * <ins>A</ins><del>B</del>X<ins>C</ins>
                     * <ins>A</del>X<ins>C</ins><del>D</del>
                     * <ins>A</ins><del>B</del>X<del>C</del>
                     */
                    if ((lastequality.Count != 0)
                        && ((pre_ins && pre_del && post_ins && post_del)
                        || ((lastequality.Count < this.Diff_EditCost / 2)
                        && ((pre_ins ? 1 : 0) + (pre_del ? 1 : 0) + (post_ins ? 1 : 0)
                        + (post_del ? 1 : 0)) == 3)))
                    {
                        // Duplicate record.
                        diffs.Insert(equalities.Peek(), new Diff<T>(Operation.DELETE, lastequality));
                        // Change second copy to insert.
                        diffs[equalities.Peek() + 1].operation = Operation.INSERT;
                        equalities.Pop();  // Throw away the equality we just deleted.
                        lastequality = Symbol<T>.EmptyList;
                        if (pre_ins && pre_del)
                        {
                            // No changes made which could affect previous entry, keep going.
                            post_ins = post_del = true;
                            equalities.Clear();
                        }
                        else
                        {
                            if (equalities.Count > 0)
                            {
                                equalities.Pop();
                            }

                            pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                            post_ins = post_del = false;
                        }
                        changes = true;
                    }
                }
                pointer++;
            }

            if (changes)
            {
                diff_cleanupMerge(diffs);
            }
        }

        /**
         * Reorder and merge like edit sections.  Merge equalities.
         * Any edit section can move as long as it doesn't cross an equality.
         * @param diffs List of Diff objects.
         */
        public void diff_cleanupMerge(List<Diff<T>> diffs)
        {
            // Add a dummy entry at the end.
            diffs.Add(new Diff<T>(Operation.EQUAL, Symbol<T>.EmptyList));
            int pointer = 0;
            int count_delete = 0;
            int count_insert = 0;
            List<Symbol<T>> text_delete = Symbol<T>.EmptyList;
            List<Symbol<T>> text_insert = Symbol<T>.EmptyList;
            int commonlength;
            while (pointer < diffs.Count)
            {
                switch (diffs[pointer].operation)
                {
                    case Operation.INSERT:
                        count_insert++;
                        text_insert.AddRange(diffs[pointer].text);
                        pointer++;
                        break;
                    case Operation.DELETE:
                        count_delete++;
                        text_delete.AddRange(diffs[pointer].text);
                        pointer++;
                        break;
                    case Operation.EQUAL:
                        // Upon reaching an equality, check for prior redundancies.
                        if (count_delete + count_insert > 1)
                        {
                            if (count_delete != 0 && count_insert != 0)
                            {
                                // Factor out any common prefixies.
                                commonlength = this.diff_commonPrefix(text_insert, text_delete);
                                if (commonlength != 0)
                                {
                                    if ((pointer - count_delete - count_insert) > 0 &&
                                      diffs[pointer - count_delete - count_insert - 1].operation
                                          == Operation.EQUAL)
                                    {
                                        diffs[pointer - count_delete - count_insert - 1].text.AddRange(text_insert.GetRange(0, commonlength));
                                    }
                                    else
                                    {
                                        diffs.Insert(0, new Diff<T>(Operation.EQUAL,
                                            text_insert.GetRange(0, commonlength)));
                                        pointer++;
                                    }
                                    text_insert = text_insert.RangeFrom(commonlength);
                                    text_delete = text_delete.RangeFrom(commonlength);
                                }
                                // Factor out any common suffixies.
                                commonlength = this.diff_commonSuffix(text_insert, text_delete);
                                if (commonlength != 0)
                                {
                                    diffs[pointer].text = text_insert.RangeFrom(text_insert.Count - commonlength).Concat(diffs[pointer].text).ToList();
                                    text_insert = text_insert.GetRange(0, text_insert.Count - commonlength);
                                    text_delete = text_delete.GetRange(0, text_delete.Count - commonlength);
                                }
                            }
                            // Delete the offending records and add the merged ones.
                            if (count_delete == 0)
                            {
                                diffs.Splice(pointer - count_insert,
                                    count_delete + count_insert,
                                    new Diff<T>(Operation.INSERT, text_insert));
                            }
                            else if (count_insert == 0)
                            {
                                diffs.Splice(pointer - count_delete,
                                    count_delete + count_insert,
                                    new Diff<T>(Operation.DELETE, text_delete));
                            }
                            else
                            {
                                diffs.Splice(pointer - count_delete - count_insert,
                                    count_delete + count_insert,
                                    new Diff<T>(Operation.DELETE, text_delete),
                                    new Diff<T>(Operation.INSERT, text_insert));
                            }
                            pointer = pointer - count_delete - count_insert +
                                (count_delete != 0 ? 1 : 0) + (count_insert != 0 ? 1 : 0) + 1;
                        }
                        else if (pointer != 0
                          && diffs[pointer - 1].operation == Operation.EQUAL)
                        {
                            // Merge this equality with the previous one.
                            diffs[pointer - 1].text.AddRange(diffs[pointer].text);
                            diffs.RemoveAt(pointer);
                        }
                        else
                        {
                            pointer++;
                        }
                        count_insert = 0;
                        count_delete = 0;
                        text_delete = Symbol<T>.EmptyList;
                        text_insert = Symbol<T>.EmptyList;
                        break;
                }
            }
            if (diffs[diffs.Count - 1].text.Count == 0)
            {
                diffs.RemoveAt(diffs.Count - 1);  // Remove the dummy entry at the end.
            }

            // Second pass: look for single edits surrounded on both sides by
            // equalities which can be shifted sideways to eliminate an equality.
            // e.g: A<ins>BA</ins>C -> <ins>AB</ins>AC
            bool changes = false;
            pointer = 1;
            // Intentionally ignore the first and last element (don't need checking).
            while (pointer < (diffs.Count - 1))
            {
                if (diffs[pointer - 1].operation == Operation.EQUAL &&
                  diffs[pointer + 1].operation == Operation.EQUAL)
                {
                    // This is a single edit surrounded by equalities.
                    if (diffs[pointer].text.EndsWith(diffs[pointer - 1].text))
                    {
                        // Shift the edit over the previous equality.
                        diffs[pointer].text = diffs[pointer - 1].text.Concat(diffs[pointer].text.GetRange(0, diffs[pointer].text.Count - diffs[pointer - 1].text.Count)).ToList();
                        diffs[pointer + 1].text = diffs[pointer - 1].text.Concat(diffs[pointer + 1].text).ToList();
                        diffs.Splice(pointer - 1, 1);
                        changes = true;
                    }
                    else if (diffs[pointer].text.StartsWith(diffs[pointer + 1].text))
                    {
                        // Shift the edit over the next equality.
                        diffs[pointer - 1].text.AddRange(diffs[pointer + 1].text);
                        diffs[pointer].text = diffs[pointer].text.RangeFrom(diffs[pointer + 1].text.Count).Concat(diffs[pointer + 1].text).ToList();
                        diffs.Splice(pointer + 1, 1);
                        changes = true;
                    }
                }
                pointer++;
            }
            // If shifts were made, the diff needs reordering and another shift sweep.
            if (changes)
            {
                this.diff_cleanupMerge(diffs);
            }
        }

        /**
         * loc is a location in text1, comAdde and return the equivalent location in
         * text2.
         * e.g. "The cat" vs "The big cat", 1->1, 5->8
         * @param diffs List of Diff objects.
         * @param loc Location within text1.
         * @return Location within text2.
         */
        public int diff_xIndex(List<Diff<T>> diffs, int loc)
        {
            int chars1 = 0;
            int chars2 = 0;
            int last_chars1 = 0;
            int last_chars2 = 0;
            Diff<T> lastDiff = null;
            foreach (Diff<T> aDiff in diffs)
            {
                if (aDiff.operation != Operation.INSERT)
                {
                    // Equality or deletion.
                    chars1 += aDiff.text.Count;
                }
                if (aDiff.operation != Operation.DELETE)
                {
                    // Equality or insertion.
                    chars2 += aDiff.text.Count;
                }
                if (chars1 > loc)
                {
                    // Overshot the location.
                    lastDiff = aDiff;
                    break;
                }
                last_chars1 = chars1;
                last_chars2 = chars2;
            }
            if (lastDiff != null && lastDiff.operation == Operation.DELETE)
            {
                // The location was deleted.
                return last_chars2;
            }
            // Add the remaining character length.
            return last_chars2 + (loc - last_chars1);
        }

        /**
         * Convert a Diff list into a pretty HTML report.
         * @param diffs List of Diff objects.
         * @param parser Defaults to a TextSymbolReader that casts all symbol values to a string
         * @return HTML representation.
         */
        public string diff_prettyHtml(List<Diff<T>> diffs, SymbolTextReader<T> reader, bool encodeHtmlChars = true)
        {
            if (reader == null)
                new TextSymbolReader<T>();

            StringBuilder html = new StringBuilder();
            foreach (Diff<T> aDiff in diffs)
            {
                string text = reader.TextFromSymbols(aDiff.text);
                if (encodeHtmlChars)
                    text = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\n", "&para;<br>");

                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        html.Append("<ins style=\"background:#e6ffe6;\">").Append(text)
                            .Append("</ins>");
                        break;
                    case Operation.DELETE:
                        html.Append("<del style=\"background:#ffe6e6;\">").Append(text)
                            .Append("</del>");
                        break;
                    case Operation.EQUAL:
                        html.Append("<span>").Append(text).Append("</span>");
                        break;
                }
            }
            return html.ToString();
        }

        /**
         * Compute and return the source text (all equalities and deletions).
         * @param diffs List of Diff objects.
         * @return Source text.
         */
        public List<Symbol<T>> diff_text1(List<Diff<T>> diffs)
        {
            List<Symbol<T>> text = Symbol<T>.EmptyList;
            foreach (Diff<T> aDiff in diffs)
            {
                if (aDiff.operation != Operation.INSERT)
                {
                    text.AddRange(aDiff.text);
                }
            }
            return text;
        }

        /**
         * Compute and return the destination text (all equalities and insertions).
         * @param diffs List of Diff objects.
         * @return Destination text.
         */
        public List<Symbol<T>> diff_text2(List<Diff<T>> diffs)
        {
            List<Symbol<T>> text = Symbol<T>.EmptyList;
            foreach (Diff<T> aDiff in diffs)
            {
                if (aDiff.operation != Operation.DELETE)
                {
                    text.AddRange(aDiff.text);
                }
            }
            return text;
        }

        /**
         * Compute the Levenshtein distance; the number of inserted, deleted or
         * substituted characters.
         * @param diffs List of Diff objects.
         * @param reader If you wish to calculate this based on next not symbol count you must preset a symbol text reader.  If null, this will just use the count of symbols not characters
         * @return Number of changes.
         */
        public int diff_levenshtein(List<Diff<T>> diffs, SymbolTextReader<T> reader = null)
        {
            int levenshtein = 0;
            int insertions = 0;
            int deletions = 0;
            foreach (Diff<T> aDiff in diffs)
            {
                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        insertions += reader == null ? aDiff.text.Count : reader.TextFromSymbols(aDiff.text).Length;
                        break;
                    case Operation.DELETE:
                        deletions += reader == null ? aDiff.text.Count : reader.TextFromSymbols(aDiff.text).Length;
                        break;
                    case Operation.EQUAL:
                        // A deletion and an insertion is one substitution.
                        levenshtein += Math.Max(insertions, deletions);
                        insertions = 0;
                        deletions = 0;
                        break;
                }
            }
            levenshtein += Math.Max(insertions, deletions);
            return levenshtein;
        }

        /**
         * Crush the diff into an encoded string which describes the operations
         * required to transform text1 into text2.
         * E.g. =3\t-2\t+ing  -> Keep 3 symbols, delete 2 symbols, insert 'ing'.
         * Operations are tab-separated.  Inserted text is escaped using %xx
         * notation.
         * @param diffs Array of Diff objects.
         * @param reader The reader to be used to transform symbols to text for "inserted" text.  Defaults to TextSymbolReader which casts symbol's values to strings
         * @param charDeltas if true, then use count of characters as parsed from symboles by the reader for removed/added instead count instead of raw symbol count
         * @return Delta text.
         */
        public string diff_toDelta(List<Diff<T>> diffs, SymbolTextReader<T> reader = null, bool charDeltas = false)
        {
            if (reader == null)
                reader = new TextSymbolReader<T>();

            StringBuilder text = new StringBuilder();
            foreach (Diff<T> aDiff in diffs)
            {
                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        text.Append("+").Append(HttpUtility.UrlEncode(reader.TextFromSymbols(aDiff.text), new UTF8Encoding())).Append("\t");
                        break;
                    case Operation.DELETE:
                        text.Append("-").Append(charDeltas ? reader.TextFromSymbols(aDiff.text).Length : aDiff.text.Count).Append("\t");
                        break;
                    case Operation.EQUAL:
                        text.Append("=").Append(charDeltas ? reader.TextFromSymbols(aDiff.text).Length : aDiff.text.Count).Append("\t");
                        break;
                }
            }
            string delta = text.ToString();
            if (delta.Length != 0)
            {
                // Strip off trailing tab character.
                delta = delta.Substring(0, delta.Length - 1);
                delta = unescapeForEncodeUriCompatability(delta);
            }
            return delta;
        }

        /**
         * Given the original text1, and an encoded string which describes the
         * operations required to transform text1 into text2, generate the full the full diff.
         * @param text1 Source string for the diff. (Leave null if charDetals is false and you provide the source)
         * @param source Source list of symbols for the diff. (Leave null if you wish to parse from text1 using the parser or have charDetals true)
         * @param delta Delta text.
         * @param parser The SymbolTextParser to be used to derive symbols from the text.
         * @param charDetals if true, then use count of characters instead of the count of symbols parsed from text by the parser for removed/added instead count instead of raw symbol count
         * @return Array of Diff objects or null if invalid.
         * @throws ArgumentException If invalid input.
         */
        public List<Diff<T>> diff_fromDelta(string text1, List<Symbol<T>> source, string delta, SymbolTextParser<T> parser, bool charDeltas = false)
        {
            if (parser == null)
                throw new ArgumentException("Symbol Text Parser is required!");

            if (!charDeltas && source == null)
            {
                if (text1 == null)
                {
                    throw new ArgumentException("text1 MUST be provided if charDeltas is false and source is null!");
                }
                else
                    source = parser.SymbolsFromText(text1);
            }
            else if (charDeltas && text1 == null)
                throw new ArgumentException("If charDetals is true you MUST provide text1");
                

            List<Diff<T>> diffs = new List<Diff<T>>();
            int pointer = 0;  // Cursor in text1
            string[] tokens = delta.Split(new string[] { "\t" }, StringSplitOptions.None);
            foreach (string token in tokens)
            {
                if (token.Length == 0)
                {
                    // Blank tokens are ok (from a trailing \t).
                    continue;
                }
                // Each token begins with a one character parameter which specifies the
                // operation of this token (delete, insert, equality).
                string param = token.Substring(1);
                switch (token[0])
                {
                    case '+':
                        // decode would change all "+" to " "
                        param = param.Replace("+", "%2b");

                        param = HttpUtility.UrlDecode(param, new UTF8Encoding(false, true));
                        //} catch (UnsupportedEncodingException e) {
                        //  // Not likely on modern system.
                        //  throw new Error("This system does not support UTF-8.", e);
                        //} catch (IllegalArgumentException e) {
                        //  // Malformed URI sequence.
                        //  throw new IllegalArgumentException(
                        //      "Illegal escape in diff_fromDelta: " + param, e);
                        //}
                        diffs.Add(new Diff<T>(Operation.INSERT, parser.SymbolsFromText(param)));
                        break;
                    case '-':
                    // Fall through.
                    case '=':
                        int n;
                        try
                        {
                            n = Convert.ToInt32(param);
                        }
                        catch (FormatException e)
                        {
                            throw new ArgumentException("Invalid number in diff_fromDelta: " + param, e);
                        }
                        if (n < 0)
                        {
                            throw new ArgumentException("Negative number in diff_fromDelta: " + param);
                        }
                        List<Symbol<T>> text;
                        try
                        {
                            if (charDeltas)
                                text = parser.SymbolsFromText(text1.Substring(pointer, n));
                            else
                                text = source.GetRange(pointer, n);

                            pointer += n;
                        }
                        catch (ArgumentOutOfRangeException e)
                        {
                            throw new ArgumentException("Delta length (" + pointer + ") larger than source length (" + (charDeltas ? text1.Length : source.Count)
                                + ").", e);
                        }
                        if (token[0] == '=')
                        {
                            diffs.Add(new Diff<T>(Operation.EQUAL, text));
                        }
                        else
                        {
                            diffs.Add(new Diff<T>(Operation.DELETE, text));
                        }
                        break;
                    default:
                        // Anything else is an error.
                        throw new ArgumentException("Invalid diff operation in diff_fromDelta: " + token[0]);
                }
            }
            if (pointer != (charDeltas ? text1.Length : source.Count))
            {
                throw new ArgumentException("Delta length (" + pointer + ") smaller than source text length (" + text1.Length + ").");
            }
            return diffs;
        }


        //  MATCH FUNCTIONS


        /**
         * Locate the best instance of 'pattern' in 'text' near 'loc'.
         * Returns -1 if no match found.
         * @param text The text to search.
         * @param pattern The pattern to search for.
         * @param loc The location to search around.
         * @return Best match index or -1.
         */
        public int match_main(List<Symbol<T>> text, List<Symbol<T>> pattern, int loc)
        {
            // Check for null inputs not needed since null can't be passed in C#.

            loc = Math.Max(0, Math.Min(loc, text.Count));
            if (text.SequenceEqual(pattern))
            {
                // Shortcut (potentially not guaranteed by the algorithm)
                return 0;
            }
            else if (text.Count == 0)
            {
                // Nothing to match.
                return -1;
            }
            else if (loc + pattern.Count <= text.Count && text.GetRange(loc, pattern.Count).SequenceEqual(pattern))
            {
                // Perfect match at the perfect spot!  (Includes case of null pattern)
                return loc;
            }
            else
            {
                // Do a fuzzy compare.
                return match_bitap(text, pattern, loc);
            }
        }

        /**
         * Locate the best instance of 'pattern' in 'text' near 'loc' using the
         * Bitap algorithm.  Returns -1 if no match found.
         * @param text The text to search.
         * @param pattern The pattern to search for.
         * @param loc The location to search around.
         * @return Best match index or -1.
         */
        protected int match_bitap(List<Symbol<T>> text, List<Symbol<T>> pattern, int loc)
        {
            // assert (Match_MaxBits == 0 || pattern.Length <= Match_MaxBits)
            //    : "Pattern too long for this application.";

            // Initialise the alphabet.
            Dictionary<int, int> s = match_alphabet(pattern);

            // Highest score beyond which we give up.
            double score_threshold = Match_Threshold;
            // Is there a nearby exact match? (speedup)
            int best_loc = text.IndexOf(pattern, loc);
            if (best_loc != -1)
            {
                score_threshold = Math.Min(match_bitapScore(0, best_loc, loc, pattern), score_threshold);
                // What about in the other direction? (speedup)
                best_loc = text.LastIndexOf(pattern, Math.Min(loc + pattern.Count, text.Count));
                if (best_loc != -1)
                {
                    score_threshold = Math.Min(match_bitapScore(0, best_loc, loc, pattern), score_threshold);
                }
            }

            // Initialise the bit arrays.
            int matchmask = 1 << (pattern.Count - 1);
            best_loc = -1;

            int bin_min, bin_mid;
            int bin_max = pattern.Count + text.Count;
            // Empty initialization added to appease C# compiler.
            int[] last_rd = new int[0];
            for (int d = 0; d < pattern.Count; d++)
            {
                // Scan for the best match; each iteration allows for one more error.
                // Run a binary search to determine how far from 'loc' we can stray at
                // this error level.
                bin_min = 0;
                bin_mid = bin_max;
                while (bin_min < bin_mid)
                {
                    if (match_bitapScore(d, loc + bin_mid, loc, pattern)
                        <= score_threshold)
                    {
                        bin_min = bin_mid;
                    }
                    else
                    {
                        bin_max = bin_mid;
                    }
                    bin_mid = (bin_max - bin_min) / 2 + bin_min;
                }
                // Use the result from this iteration as the maximum for the next.
                bin_max = bin_mid;
                int start = Math.Max(1, loc - bin_mid + 1);
                int finish = Math.Min(loc + bin_mid, text.Count) + pattern.Count;

                int[] rd = new int[finish + 2];
                rd[finish + 1] = (1 << d) - 1;
                for (int j = finish; j >= start; j--)
                {
                    int charMatch;
                    if (text.Count <= j - 1 || !s.ContainsKey(text[j - 1].GetHashCode()))
                    {
                        // Out of range.
                        charMatch = 0;
                    }
                    else
                    {
                        charMatch = s[text[j - 1].GetHashCode()];
                    }
                    if (d == 0)
                    {
                        // First pass: exact match.
                        rd[j] = ((rd[j + 1] << 1) | 1) & charMatch;
                    }
                    else
                    {
                        // Subsequent passes: fuzzy match.
                        rd[j] = ((rd[j + 1] << 1) | 1) & charMatch
                            | (((last_rd[j + 1] | last_rd[j]) << 1) | 1) | last_rd[j + 1];
                    }
                    if ((rd[j] & matchmask) != 0)
                    {
                        double score = match_bitapScore(d, j - 1, loc, pattern);
                        // This match will almost certainly be better than any existing
                        // match.  But check anyway.
                        if (score <= score_threshold)
                        {
                            // Told you so.
                            score_threshold = score;
                            best_loc = j - 1;
                            if (best_loc > loc)
                            {
                                // When passing loc, don't exceed our current distance from loc.
                                start = Math.Max(1, 2 * loc - best_loc);
                            }
                            else
                            {
                                // Already passed loc, downhill from here on in.
                                break;
                            }
                        }
                    }
                }
                if (match_bitapScore(d + 1, loc, loc, pattern) > score_threshold)
                {
                    // No hope for a (better) match at greater error levels.
                    break;
                }
                last_rd = rd;
            }
            return best_loc;
        }

        /**
         * Compute and return the score for a match with e errors and x location.
         * @param e Number of errors in match.
         * @param x Location of match.
         * @param loc Expected location of match.
         * @param pattern Pattern being sought.
         * @return Overall score for match (0.0 = good, 1.0 = bad).
         */
        private double match_bitapScore(int e, int x, int loc, List<Symbol<T>> pattern)
        {
            float accuracy = (float)e / pattern.Count;
            int proximity = Math.Abs(loc - x);
            if (Match_Distance == 0)
            {
                // Dodge divide by zero error.
                return proximity == 0 ? accuracy : 1.0;
            }
            return accuracy + (proximity / (float)Match_Distance);
        }

        /**
         * Initialise the alphabet for the Bitap algorithm.
         * @param pattern The text to encode.
         * @return Hash of character locations.
         */
        protected Dictionary<int, int> match_alphabet(List<Symbol<T>> pattern)
        {
            Dictionary<int, int> s = new Dictionary<int, int>();
            foreach (Symbol<T> c in pattern)
            {
                if (!s.ContainsKey(c.GetHashCode()))
                {
                    s.Add(c.GetHashCode(), 0);
                }
            }
            int i = 0;
            foreach (Symbol<T> c in pattern)
            {
                int value = s[c.GetHashCode()] | (1 << (pattern.Count - i - 1));
                s[c.GetHashCode()] = value;
                i++;
            }
            return s;
        }


        //  Patch<T> FUNCTIONS


        /**
         * Increase the context until it is unique,
         * but don't let the pattern expand beyond Match_MaxBits.
         * @param Patch<T> The Patch<T> to grow.
         * @param text Source text.
         */
        protected void patch_addContext(Patch<T> patch, List<Symbol<T>> text)
        {
            if (text.Count == 0)
            {
                return;
            }
            List<Symbol<T>> pattern = text.GetRange(patch.start2, patch.length1);
            int padding = 0;

            // Look for the first and last matches of pattern in text.  If two
            // different matches are found, increase the pattern length.
            while (text.IndexOf(pattern)
                != text.LastIndexOf(pattern)
                && pattern.Count < Match_MaxBits - Patch_Margin - Patch_Margin)
            {
                padding += Patch_Margin;
                pattern = text.JavaSubstring(Math.Max(0, patch.start2 - padding), Math.Min(text.Count, patch.start2 + patch.length1 + padding));
            }
            // Add one chunk for good luck.
            padding += Patch_Margin;

            // Add the prefix.
            List<Symbol<T>> prefix = text.JavaSubstring(Math.Max(0, patch.start2 - padding), patch.start2);
            if (prefix.Count != 0)
            {
                patch.diffs.Insert(0, new Diff<T>(Operation.EQUAL, prefix));
            }
            // Add the suffix.
            List<Symbol<T>> suffix = text.JavaSubstring(patch.start2 + patch.length1, Math.Min(text.Count, patch.start2 + patch.length1 + padding));
            if (suffix.Count != 0)
            {
                patch.diffs.Add(new Diff<T>(Operation.EQUAL, suffix));
            }

            // Roll back the start points.
            patch.start1 -= prefix.Count;
            patch.start2 -= prefix.Count;
            // Extend the lengths.
            patch.length1 += prefix.Count + suffix.Count;
            patch.length2 += prefix.Count + suffix.Count;
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * A set of diffs will be computed.
         * @param text1 Old text.
         * @param text2 New text.
         * @return List of Patch<T> objects.
         */
        public List<Patch<T>> patch_make(List<Symbol<T>> text1, List<Symbol<T>> text2)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            // No diffs provided, comAdde our own.
            List<Diff<T>> diffs = diff_main(text1, text2);
            if (diffs.Count > 2)
            {
                diff_cleanupSemantic(diffs);
                diff_cleanupEfficiency(diffs);
            }
            return patch_make(text1, diffs);
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * text1 will be derived from the provided diffs.
         * @param diffs Array of Diff objects for text1 to text2.
         * @return List of Patch<T> objects.
         */
        public List<Patch<T>> patch_make(List<Diff<T>> diffs)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            // No origin string provided, comAdde our own.
            List<Symbol<T>> text1 = diff_text1(diffs);
            return patch_make(text1, diffs);
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * text2 is ignored, diffs are the delta between text1 and text2.
         * @param text1 Old text
         * @param text2 Ignored.
         * @param diffs Array of Diff objects for text1 to text2.
         * @return List of Patch<T> objects.
         * @deprecated Prefer patch_make(string text1, List<Diff<T>> diffs).
         */
        public List<Patch<T>> patch_make(List<Symbol<T>> text1, List<Symbol<T>> text2, List<Diff<T>> diffs)
        {
            return patch_make(text1, diffs);
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * text2 is not provided, diffs are the delta between text1 and text2.
         * @param text1 Old text.
         * @param diffs Array of Diff objects for text1 to text2.
         * @return List of Patch<T> objects.
         */
        public List<Patch<T>> patch_make(List<Symbol<T>> text1, List<Diff<T>> diffs)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            List<Patch<T>> patches = new List<Patch<T>>();
            if (diffs.Count == 0)
            {
                return patches;  // Get rid of the null case.
            }
            Patch<T> patch = new Patch<T>();
            int char_count1 = 0;  // Number of characters into the text1 string.
            int char_count2 = 0;  // Number of characters into the text2 string.
                                  // Start with text1 (prepatch_text) and apply the diffs until we arrive at
                                  // text2 (postpatch_text). We recreate the patches one by one to determine
                                  // context info.
            List<Symbol<T>> prepatch_text = text1.Copy();

            List<Symbol<T>> postpatch_text = text1;
            foreach (Diff<T> aDiff in diffs)
            {
                if (patch.diffs.Count == 0 && aDiff.operation != Operation.EQUAL)
                {
                    // A new Patch<T> starts here.
                    patch.start1 = char_count1;
                    patch.start2 = char_count2;
                }

                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        patch.diffs.Add(aDiff);
                        patch.length2 += aDiff.text.Count;
                        postpatch_text.InsertRange(char_count2, aDiff.text);
                        break;
                    case Operation.DELETE:
                        patch.length1 += aDiff.text.Count;
                        patch.diffs.Add(aDiff);
                        postpatch_text.RemoveRange(char_count2, aDiff.text.Count);
                        break;
                    case Operation.EQUAL:
                        if (aDiff.text.Count <= 2 * Patch_Margin
                            && patch.diffs.Count() != 0 && aDiff != diffs.Last())
                        {
                            // Small equality inside a patch.
                            patch.diffs.Add(aDiff);
                            patch.length1 += aDiff.text.Count;
                            patch.length2 += aDiff.text.Count;
                        }

                        if (aDiff.text.Count >= 2 * Patch_Margin)
                        {
                            // Time for a new patch.
                            if (patch.diffs.Count != 0)
                            {
                                patch_addContext(patch, prepatch_text);
                                patches.Add(patch);
                                patch = new Patch<T>();
                                // Unlike Unidiff, our Patch<T> lists have a rolling context.
                                // http://code.google.com/p/google-diff-match-patch/wiki/Unidiff
                                // Update prePatch<T> text & pos to reflect the application of the
                                // just completed patch.
                                prepatch_text = postpatch_text;
                                char_count1 = char_count2;
                            }
                        }
                        break;
                }

                // Update the current character count.
                if (aDiff.operation != Operation.INSERT)
                {
                    char_count1 += aDiff.text.Count;
                }
                if (aDiff.operation != Operation.DELETE)
                {
                    char_count2 += aDiff.text.Count;
                }
            }
            // Pick up the leftover Patch<T> if not empty.
            if (patch.diffs.Count != 0)
            {
                patch_addContext(patch, prepatch_text);
                patches.Add(patch);
            }

            return patches;
        }

        /**
         * Given an array of patches, return another array that is identical.
         * @param patches Array of Patch<T> objects.
         * @return Array of Patch<T> objects.
         */
        public List<Patch<T>> patch_deepCopy(List<Patch<T>> patches)
        {
            List<Patch<T>> patchesCopy = new List<Patch<T>>();
            foreach (Patch<T> aPatch in patches)
            {
                Patch<T> patchCopy = new Patch<T>();
                foreach (Diff<T> aDiff in aPatch.diffs)
                {
                    Diff<T> diffCopy = new Diff<T>(aDiff.operation, aDiff.text.Copy());
                    patchCopy.diffs.Add(diffCopy);
                }
                patchCopy.start1 = aPatch.start1;
                patchCopy.start2 = aPatch.start2;
                patchCopy.length1 = aPatch.length1;
                patchCopy.length2 = aPatch.length2;
                patchesCopy.Add(patchCopy);
            }
            return patchesCopy;
        }

        /**
         * Merge a set of patches onto the text.  Return a patched text, as well
         * as an array of true/false values indicating which patches were applied.
         * @param patches Array of Patch<T> objects
         * @param text Old text.
         * @return Two element Object array, containing the new text and an array of
         *      bool values.
         */
        public Object[] patch_apply(List<Patch<T>> patches, List<Symbol<T>> text)
        {
            if (patches.Count == 0)
            {
                return new Object[] { text, new bool[0] };
            }

            // Deep copy the patches so that no changes are made to originals.
            patches = patch_deepCopy(patches);

            List<Symbol<T>> nullPadding = this.patch_addPadding(patches);
            text = nullPadding.Concat(text).Concat(nullPadding).ToList();
            patch_splitMax(patches);

            int x = 0;
            // delta keeps track of the offset between the expected and actual
            // location of the previous patch.  If there are patches expected at
            // positions 10 and 20, but the first Patch<T> was found at 12, delta is 2
            // and the second Patch<T> has an effective expected position of 22.
            int delta = 0;
            bool[] results = new bool[patches.Count];
            foreach (Patch<T> aPatch in patches)
            {
                int expected_loc = aPatch.start2 + delta;
                List<Symbol<T>> text1 = diff_text1(aPatch.diffs);
                int start_loc;
                int end_loc = -1;
                if (text1.Count > this.Match_MaxBits)
                {
                    // patch_splitMax will only provide an oversized pattern
                    // in the case of a monster delete.
                    start_loc = match_main(text, text1.GetRange(0, this.Match_MaxBits), expected_loc);
                    if (start_loc != -1)
                    {
                        end_loc = match_main(text, text1.RangeFrom(text1.Count - this.Match_MaxBits), expected_loc + text1.Count - this.Match_MaxBits);
                        if (end_loc == -1 || start_loc > end_loc)
                        {
                            // Can't find valid trailing context.  Drop this patch.
                            start_loc = -1;
                        }
                    }
                }
                else
                {
                    start_loc = this.match_main(text, text1, expected_loc);
                }
                if (start_loc == -1)
                {
                    // No match found.  :(
                    results[x] = false;
                    // Subtract the delta for this failed patch from subsequent patches.
                    delta -= aPatch.length2 - aPatch.length1;
                }
                else
                {
                    // Found a match.  :)
                    results[x] = true;
                    delta = start_loc - expected_loc;
                    List<Symbol<T>> text2;
                    if (end_loc == -1)
                    {
                        text2 = text.JavaSubstring(start_loc, Math.Min(start_loc + text1.Count, text.Count));
                    }
                    else
                    {
                        text2 = text.JavaSubstring(start_loc, Math.Min(end_loc + this.Match_MaxBits, text.Count));
                    }
                    if (text1 == text2)
                    {
                        // Perfect match, just shove the Replacement text in.
                        text = text.GetRange(0, start_loc).Concat(diff_text2(aPatch.diffs)).Concat(text.RangeFrom(start_loc + text1.Count)).ToList();
                    }
                    else
                    {
                        // Imperfect match.  Run a diff to get a framework of equivalent
                        // indices.
                        List<Diff<T>> diffs = diff_main(text1, text2);
                        if (text1.Count > this.Match_MaxBits
                            && this.diff_levenshtein(diffs) / (float)text1.Count
                            > this.Patch_DeleteThreshold)
                        {
                            // The end points match, but the content is unacceptably bad.
                            results[x] = false;
                        }
                        else
                        {
                            diff_cleanupSemanticLossless(diffs);
                            int index1 = 0;
                            foreach (Diff<T> aDiff in aPatch.diffs)
                            {
                                if (aDiff.operation != Operation.EQUAL)
                                {
                                    int index2 = diff_xIndex(diffs, index1);
                                    if (aDiff.operation == Operation.INSERT)
                                    {
                                        // Insertion
                                        text.InsertRange(start_loc + index2, aDiff.text);
                                    }
                                    else if (aDiff.operation == Operation.DELETE)
                                    {
                                        // Deletion
                                        text.RemoveRange(start_loc + index2, diff_xIndex(diffs, index1 + aDiff.text.Count) - index2);
                                    }
                                }
                                if (aDiff.operation != Operation.DELETE)
                                {
                                    index1 += aDiff.text.Count;
                                }
                            }
                        }
                    }
                }
                x++;
            }
            // Strip the padding off.
            text = text.GetRange(nullPadding.Count, text.Count - 2 * nullPadding.Count);
            return new Object[] { text, results };
        }

        /**
         * Add some padding on text start and end so that edges can match something.
         * Intended to be called only from within patch_apply.
         * @param patches Array of Patch<T> objects.
         * @return The padding string added to each side.
         */
        public List<Symbol<T>> patch_addPadding(List<Patch<T>> patches)
        {
            short paddingLength = this.Patch_Margin;
            List<Symbol<T>> nullPadding = Symbol<T>.EmptyList;
            for (short x = 1; x <= paddingLength; x++)
            {
				nullPadding.Add(new Symbol<T>());
            }

            // Bump all the patches forward.
            foreach (Patch<T> aPatch in patches)
            {
                aPatch.start1 += paddingLength;
                aPatch.start2 += paddingLength;
            }

            // Add some padding on start of first diff.
            Patch<T> patch = patches.First();
            List<Diff<T>> diffs = patch.diffs;
            if (diffs.Count == 0 || diffs.First().operation != Operation.EQUAL)
            {
                // Add nullPadding equality.
                diffs.Insert(0, new Diff<T>(Operation.EQUAL, nullPadding));
                patch.start1 -= paddingLength;  // Should be 0.
                patch.start2 -= paddingLength;  // Should be 0.
                patch.length1 += paddingLength;
                patch.length2 += paddingLength;
            }
            else if (paddingLength > diffs.First().text.Count)
            {
                // Grow first equality.
                Diff<T> firstDiff = diffs.First();
                int extraLength = paddingLength - firstDiff.text.Count;
                firstDiff.text = nullPadding.RangeFrom(firstDiff.text.Count).Concat(firstDiff.text).ToList();
                patch.start1 -= extraLength;
                patch.start2 -= extraLength;
                patch.length1 += extraLength;
                patch.length2 += extraLength;
            }

            // Add some padding on end of last diff.
            patch = patches.Last();
            diffs = patch.diffs;
            if (diffs.Count == 0 || diffs.Last().operation != Operation.EQUAL)
            {
                // Add nullPadding equality.
                diffs.Add(new Diff<T>(Operation.EQUAL, nullPadding));
                patch.length1 += paddingLength;
                patch.length2 += paddingLength;
            }
            else if (paddingLength > diffs.Last().text.Count)
            {
                // Grow last equality.
                Diff<T> lastDiff = diffs.Last();
                int extraLength = paddingLength - lastDiff.text.Count;
                lastDiff.text.AddRange(nullPadding.GetRange(0, extraLength));
                patch.length1 += extraLength;
                patch.length2 += extraLength;
            }

            return nullPadding;
        }

        /**
         * Look through the patches and break up any which are longer than the
         * maximum limit of the match algorithm.
         * Intended to be called only from within patch_apply.
         * @param patches List of Patch<T> objects.
         */
        public void patch_splitMax(List<Patch<T>> patches)
        {
            short patch_size = this.Match_MaxBits;
            for (int x = 0; x < patches.Count; x++)
            {
                if (patches[x].length1 <= patch_size)
                {
                    continue;
                }
                Patch<T> bigpatch = patches[x];
                // Remove the big old patch.
                patches.Splice(x--, 1);
                int start1 = bigpatch.start1;
                int start2 = bigpatch.start2;
                List<Symbol<T>> precontext = Symbol<T>.EmptyList;
                while (bigpatch.diffs.Count != 0)
                {
                    // Create one of several smaller patches.
                    Patch<T> patch = new Patch<T>();
                    bool empty = true;
                    patch.start1 = start1 - precontext.Count;
                    patch.start2 = start2 - precontext.Count;
                    if (precontext.Count != 0)
                    {
                        patch.length1 = patch.length2 = precontext.Count;
                        patch.diffs.Add(new Diff<T>(Operation.EQUAL, precontext));
                    }
                    while (bigpatch.diffs.Count != 0 && patch.length1 < patch_size - this.Patch_Margin)
                    {
                        Operation diff_type = bigpatch.diffs[0].operation;
                        List<Symbol<T>> diff_text = bigpatch.diffs[0].text;
                        if (diff_type == Operation.INSERT)
                        {
                            // Insertions are harmless.
                            patch.length2 += diff_text.Count;
                            start2 += diff_text.Count;
                            patch.diffs.Add(bigpatch.diffs.First());
                            bigpatch.diffs.RemoveAt(0);
                            empty = false;
                        }
                        else if (diff_type == Operation.DELETE && patch.diffs.Count == 1
                          && patch.diffs.First().operation == Operation.EQUAL
                          && diff_text.Count > 2 * patch_size)
                        {
                            // This is a large deletion.  Let it pass in one chunk.
                            patch.length1 += diff_text.Count;
                            start1 += diff_text.Count;
                            empty = false;
                            patch.diffs.Add(new Diff<T>(diff_type, diff_text));
                            bigpatch.diffs.RemoveAt(0);
                        }
                        else
                        {
                            // Deletion or equality.  Only take as much as we can stomach.
                            diff_text = diff_text.GetRange(0, Math.Min(diff_text.Count, patch_size - patch.length1 - Patch_Margin));
                            patch.length1 += diff_text.Count;
                            start1 += diff_text.Count;
                            if (diff_type == Operation.EQUAL)
                            {
                                patch.length2 += diff_text.Count;
                                start2 += diff_text.Count;
                            }
                            else
                            {
                                empty = false;
                            }
                            patch.diffs.Add(new Diff<T>(diff_type, diff_text));
                            if (diff_text.SequenceEqual(bigpatch.diffs[0].text))
                            {
                                bigpatch.diffs.RemoveAt(0);
                            }
                            else
                            {
                                bigpatch.diffs[0].text = bigpatch.diffs[0].text.RangeFrom(diff_text.Count);
                            }
                        }
                    }
                    // Compute the head context for the next patch.
                    precontext = this.diff_text2(patch.diffs);
                    precontext = precontext.RangeFrom(Math.Max(0, precontext.Count - this.Patch_Margin));

                    List<Symbol<T>> postcontext = null;
                    // Append the end context for this patch.
                    if (diff_text1(bigpatch.diffs).Count > Patch_Margin)
                    {
                        postcontext = diff_text1(bigpatch.diffs).GetRange(0, Patch_Margin);
                    }
                    else
                    {
                        postcontext = diff_text1(bigpatch.diffs);
                    }

                    if (postcontext.Count != 0)
                    {
                        patch.length1 += postcontext.Count;
                        patch.length2 += postcontext.Count;
                        if (patch.diffs.Count != 0
                            && patch.diffs[patch.diffs.Count - 1].operation
                            == Operation.EQUAL)
                        {
                            patch.diffs[patch.diffs.Count - 1].text.AddRange(postcontext);
                        }
                        else
                        {
                            patch.diffs.Add(new Diff<T>(Operation.EQUAL, postcontext));
                        }
                    }
                    if (!empty)
                    {
                        patches.Splice(++x, 0, patch);
                    }
                }
            }
        }

        /**
         * Take a list of patches and return a textual representation.
         * @param patches List of Patch<T> objects.
         * @param reader The symbol reader to use to parse the symbols to text for the patch.  You should probably have a Symbol Parser that reliably convers that back to symbols.  Defaults to TextSymbolReader
         * @return Text representation of patches.
         */
        public string patch_toText(List<Patch<T>> patches, SymbolTextReader<T> reader)
        {
            StringBuilder text = new StringBuilder();
            foreach (Patch<T> aPatch in patches)
            {
                text.Append(reader != null ? aPatch.ToString(reader) : aPatch.ToString());
            }
            return text.ToString();
        }

        /**
         * Parse a textual representation of patches and return a List of Patch
         * objects.
         * @param textline Text representation of patches.
         * @return List of Patch<T> objects.
         * @throws ArgumentException If invalid input.
         */
        public List<Patch<T>> patch_fromText(string textline, SymbolTextParser<T> parser)
        {
            List<Patch<T>> patches = new List<Patch<T>>();
            if (textline == null || textline.Length == 0)
            {
                return patches;
            }
            string[] text = textline.Split('\n');
            int textPointer = 0;
            Patch<T> patch;
            Regex patchHeader = new Regex("^@@ -(\\d+),?(\\d*) \\+(\\d+),?(\\d*) @@$");
            Match m;
            char sign;
            string line;
            while (textPointer < text.Length)
            {
                m = patchHeader.Match(text[textPointer]);

                if (!m.Success)
                    throw new ArgumentException("Invalid Patch<T> string: " + text[textPointer]);

                patch = new Patch<T>();
                patches.Add(patch);
                patch.start1 = Convert.ToInt32(m.Groups[1].Value);
                if (m.Groups[2].Length == 0)
                {
                    patch.start1--;
                    patch.length1 = 1;
                }
                else if (m.Groups[2].Value == "0")
                {
                    patch.length1 = 0;
                }
                else
                {
                    patch.start1--;
                    patch.length1 = Convert.ToInt32(m.Groups[2].Value);
                }

                patch.start2 = Convert.ToInt32(m.Groups[3].Value);
                if (m.Groups[4].Length == 0)
                {
                    patch.start2--;
                    patch.length2 = 1;
                }
                else if (m.Groups[4].Value == "0")
                {
                    patch.length2 = 0;
                }
                else
                {
                    patch.start2--;
                    patch.length2 = Convert.ToInt32(m.Groups[4].Value);
                }
                textPointer++;

                while (textPointer < text.Length)
                {
                    try
                    {
                        sign = text[textPointer][0];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Blank line?  Whatever.
                        textPointer++;
                        continue;
                    }
                    line = text[textPointer].Substring(1);
                    line = line.Replace("+", "%2b");
                    line = HttpUtility.UrlDecode(line, new UTF8Encoding(false, true));
                    if (sign == '-')
                    {
                        // Deletion.
                        patch.diffs.Add(new Diff<T>(Operation.DELETE, parser.SymbolsFromText(line)));
                    }
                    else if (sign == '+')
                    {
                        // Insertion.
                        patch.diffs.Add(new Diff<T>(Operation.INSERT, parser.SymbolsFromText(line)));
                    }
                    else if (sign == ' ')
                    {
                        // Minor equality.
                        patch.diffs.Add(new Diff<T>(Operation.EQUAL, parser.SymbolsFromText(line)));
                    }
                    else if (sign == '@')
                    {
                        // Start of next patch.
                        break;
                    }
                    else
                    {
                        // WTF?
                        throw new ArgumentException("Invalid Patch<T> mode '" + sign + "' in: " + line);
                    }
                    textPointer++;
                }
            }
            return patches;
        }

        /**
         * Unescape selected chars for compatability with JavaScript's encodeURI.
         * In speed critical applications this could be dropped since the
         * receiving application will certainly decode these fine.
         * Note that this function is case-sensitive.  Thus "%3F" would not be
         * unescaped.  But this is ok because it is only called with the output of
         * HttpUtility.UrlEncode which returns lowercase hex.
         *
         * Example: "%3f" -> "?", "%24" -> "$", etc.
         *
         * @param str The string to escape.
         * @return The escaped string.
         */
        public static string unescapeForEncodeUriCompatability(string str)
        {
            return str.Replace("%21", "!").Replace("%7e", "~")
                .Replace("%27", "'").Replace("%28", "(").Replace("%29", ")")
                .Replace("%3b", ";").Replace("%2f", "/").Replace("%3f", "?")
                .Replace("%3a", ":").Replace("%40", "@").Replace("%26", "&")
                .Replace("%3d", "=").Replace("%2b", "+").Replace("%24", "$")
                .Replace("%2c", ",").Replace("%23", "#");
        }
    }
}
