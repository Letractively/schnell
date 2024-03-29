#region License, Terms and Author(s)
//
// Schnell - Wiki widgets
// Copyright (c) 2007 Atif Aziz. All rights reserved.
//
//  Author(s):
//      Atif Aziz, http://www.raboof.com
//
// This library is free software; you can redistribute it and/or modify it 
// under the terms of the GNU Lesser General Public License as published by 
// the Free Software Foundation; either version 2.1 of the License, or (at 
// your option) any later version.
//
// This library is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or 
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public 
// License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with this library; if not, write to the Free Software Foundation, 
// Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA 
//
#endregion

namespace Schnell
{
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.UI;
    using Schnell;

    #endregion

    public sealed class HtmlFormatter
    {
        private static readonly Dictionary<Type, HtmlTextWriterTag> _tagByToken;

        public void Format(IEnumerable<WikiToken> tokens, HtmlTextWriter writer) 
        {
            if (tokens == null) throw new ArgumentNullException("tokens");
            if (writer == null) throw new ArgumentNullException("writer");

            Stack<WikiToken> stack = new Stack<WikiToken>();

            IEnumerator<WikiToken> e = tokens.GetEnumerator();

            while (e.MoveNext())
            {
                WikiToken token = e.Current;

                if (token is WikiTextToken)
                {
                    writer.WriteEncodedText(((WikiTextToken) token).Text);
                }
                else if (token is WikiEndToken)
                {
                    WikiToken popped = stack.Pop();
                    Debug.Assert(popped.GetType() == ((WikiEndToken) token).Start.GetType());

                    writer.RenderEndTag();

                    if (!(popped is WikiMonospaceToken) && !(popped is WikiHyperlinkToken))
                        writer.WriteLine();
                }
                else if (token is WikiCodeToken)
                {
                    writer.InnerWriter.Write("<pre>");
                    HttpUtility.HtmlEncode(((WikiCodeToken) token).Text, writer.InnerWriter);
                    writer.InnerWriter.WriteLine("</pre>");
                }
                else if (token is WikiImageToken)
                {
                    WikiImageToken image = (WikiImageToken) token;
                    
                    if (image.Href.Length > 0)
                    {
                        writer.AddAttribute(HtmlTextWriterAttribute.Href, image.Href);
                        writer.RenderBeginTag(HtmlTextWriterTag.A);
                    }

                    writer.AddAttribute(HtmlTextWriterAttribute.Src, image.Src);
                    writer.AddAttribute(HtmlTextWriterAttribute.Alt, string.Empty);
                    writer.RenderBeginTag(HtmlTextWriterTag.Img);
                    writer.RenderEndTag();

                    if (image.Href.Length > 0)
                        writer.RenderEndTag();
                }
                else if (token is WikiHyperlinkToken)
                {
                    WikiHyperlinkToken hyperlink = (WikiHyperlinkToken) token;
                    writer.AddAttribute(HtmlTextWriterAttribute.Href, hyperlink.Href);
                    writer.RenderBeginTag(HtmlTextWriterTag.A);
                    writer.WriteEncodedText(hyperlink.Text);
                    writer.RenderEndTag();
                }
                else if (token is WikiWordToken)
                {
                    WikiWordToken word = (WikiWordToken) token;

                    if (word.Url != null)
                    {
                        writer.AddAttribute(HtmlTextWriterAttribute.Href, word.Url.ToString());
                        writer.RenderBeginTag(HtmlTextWriterTag.A);
                        writer.WriteEncodedText(word.Text);
                        writer.RenderEndTag();
                    }
                    else
                    {
                        writer.WriteEncodedText(word.Text);
                    }
                }
                else 
                {
                    if (token is WikiHeadingToken)
                    {
                        List<WikiToken> subtokens = new List<WikiToken>(ReadToEnd(e, token));

                        string text = ExtractText(subtokens);
                        if (text.Length > 0)
                        {
                            string id = Regex.Replace(text, 
                                @"[^a-z0-9-\.\:_]", "_", 
                                RegexOptions.IgnoreCase | 
                                RegexOptions.CultureInvariant | 
                                RegexOptions.Compiled);
                            
                            writer.AddAttribute(HtmlTextWriterAttribute.Id, id);
                        }

                        WikiHeadingToken heading = (WikiHeadingToken) token;
                        writer.RenderBeginTag("h" + heading.Level.ToString(System.Globalization.CultureInfo.InvariantCulture));                        
                        Format(subtokens, writer);
                        writer.RenderEndTag();
                    }
                    else
                    {
                        HtmlTextWriterTag tag;
                        Type tokenType = token.GetType();

                        if (!_tagByToken.TryGetValue(tokenType, out tag))
                            throw new Exception("Don't know how to handle " + token.GetType().Name + ".");

                        writer.RenderBeginTag(tag);
                        stack.Push(token);
                    }
                }
            }

            Debug.Assert(stack.Count == 0);
        }

        /// <summary>
        /// Returns all tokens up to the terminator of the given start token.
        /// </summary>
        /// <remarks>
        /// The source enumerator is left on the terminator token when this 
        /// method is finished reading.
        /// </remarks>

        private static IEnumerable<WikiToken> ReadToEnd(IEnumerator<WikiToken> e, WikiToken start)
        {
            Debug.Assert(e != null);
            Debug.Assert(start != null);
            Debug.Assert(!(start is WikiEndToken));

            while (e.MoveNext())
            {
                WikiEndToken end = e.Current as WikiEndToken;

                if (end != null && end.Start == start)
                    break;

                yield return e.Current;
            }
        }

        /// <summary>
        /// Extracts text from all text tokens found and creates a single 
        /// string from them.
        /// </summary>

        private static string ExtractText(IEnumerable<WikiToken> tokens)
        {
            Debug.Assert(tokens != null);

            StringBuilder sb = null;

            foreach (WikiToken token in tokens)
            {
                WikiTextToken text = token as WikiTextToken;
                
                if (text != null && text.Text.Length > 0)
                {
                    if (sb == null)    
                        sb = new StringBuilder();

                    sb.Append(text.Text);
                }
            }

            return sb != null && sb.Length > 0 ? sb.ToString() : string.Empty;
        }

        static HtmlFormatter()
        {
            _tagByToken = new Dictionary<Type, HtmlTextWriterTag>();
            _tagByToken.Add(typeof(WikiParaToken), HtmlTextWriterTag.P);
            _tagByToken.Add(typeof(WikiTableToken), HtmlTextWriterTag.Table);
            _tagByToken.Add(typeof(WikiRowToken), HtmlTextWriterTag.Tr);
            _tagByToken.Add(typeof(WikiCellToken), HtmlTextWriterTag.Td);
            _tagByToken.Add(typeof(WikiBoldToken), HtmlTextWriterTag.Strong);
            _tagByToken.Add(typeof(WikiItalicToken), HtmlTextWriterTag.Em);
            _tagByToken.Add(typeof(WikiStrikeToken), HtmlTextWriterTag.Del);
            _tagByToken.Add(typeof(WikiSuperscriptToken), HtmlTextWriterTag.Sup);
            _tagByToken.Add(typeof(WikiSubscriptToken), HtmlTextWriterTag.Sub);
            _tagByToken.Add(typeof(WikiMonospaceToken), HtmlTextWriterTag.Code);
            _tagByToken.Add(typeof(WikiQuoteToken), HtmlTextWriterTag.Blockquote);
            _tagByToken.Add(typeof(WikiBulletedListToken), HtmlTextWriterTag.Ul);
            _tagByToken.Add(typeof(WikiNumberedListToken), HtmlTextWriterTag.Ol);
            _tagByToken.Add(typeof(WikiListItemToken), HtmlTextWriterTag.Li);
        }
    }
}
