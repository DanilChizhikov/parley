using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class MarkdownView : VisualElement
    {
        private const long CopiedResetMs = 1200;
        private const float ListIndentWidth = 14.0f;

        private readonly List<string> _signatures = new ();

        private string _source;

        public MarkdownView()
        {
            AddToClassList("pl-md");
        }

        public static VisualElement CodeBlock(string code, string language, bool highlight = true)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("pl-md-code");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-md-code__header");
            header.Add(ParleyStyles.Text(string.IsNullOrEmpty(language) ? "code" : language, "pl-md-code__lang"));
            header.Add(ParleyStyles.Spacer());
            Button copy = ParleyStyles.Button("Copy", null, "pl-button--small");
            copy.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = code;
                copy.text = "Copied";
                copy.schedule.Execute(() => copy.text = "Copy").ExecuteLater(CopiedResetMs);
            };

            header.Add(copy);
            container.Add(header);
            ScrollView scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("pl-md-code__scroll");
            Label label = new Label(highlight ? CodeHighlighter.Highlight(code, language) : InlineMarkdown.Escape(code)) { enableRichText = true };
            label.selection.isSelectable = true;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.AddToClassList("pl-md-code__text");
            ParleyStyles.UseMonospace(label);
            scroll.Add(label);
            container.Add(scroll);
            return container;
        }

        public void SetMarkdown(string markdown)
        {
            if (markdown == _source)
            {
                return;
            }

            _source = markdown;
            List<MarkdownBlock> blocks = MarkdownParser.Parse(markdown);
            int keep = 0;
            while (keep < blocks.Count && keep < _signatures.Count && keep < childCount && blocks[keep].Signature == _signatures[keep])
            {
                keep++;
            }

            while (childCount > keep)
            {
                RemoveAt(childCount - 1);
            }

            if (_signatures.Count > keep)
            {
                _signatures.RemoveRange(keep, _signatures.Count - keep);
            }

            for (int i = keep; i < blocks.Count; i++)
            {
                Add(Build(blocks[i]));
                _signatures.Add(blocks[i].Signature);
            }
        }

        private static VisualElement Build(MarkdownBlock block)
        {
            switch (block.Type)
            {
                case MarkdownBlockType.Heading:
                    return ParleyStyles.RichLabel(InlineMarkdown.ToRichText(block.Text), "pl-md-h" + Mathf.Clamp(block.Level, 1, 4));
                case MarkdownBlockType.Code:
                    return CodeBlock(block.Text, block.Language);
                case MarkdownBlockType.Quote:
                    VisualElement quote = new VisualElement();
                    quote.AddToClassList("pl-md-quote");
                    quote.Add(ParleyStyles.RichLabel(InlineMarkdown.ToRichText(block.Text), "pl-md-p"));
                    return quote;
                case MarkdownBlockType.List:
                    return BuildList(block);
                case MarkdownBlockType.Table:
                    return BuildTable(block);
                case MarkdownBlockType.Rule:
                    VisualElement rule = new VisualElement();
                    rule.AddToClassList("pl-md-rule");
                    return rule;
                default:
                    return ParleyStyles.RichLabel(InlineMarkdown.ToRichText(block.Text), "pl-md-p");
            }
        }

        private static VisualElement BuildList(MarkdownBlock block)
        {
            VisualElement list = new VisualElement();
            list.AddToClassList("pl-md-list");
            foreach (MarkdownListItem item in block.Items)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("pl-md-list__item");
                row.style.marginLeft = item.Indent * ListIndentWidth;
                row.Add(ParleyStyles.Text(Bullet(item), "pl-md-list__bullet"));
                row.Add(ParleyStyles.RichLabel(InlineMarkdown.ToRichText(item.Text), "pl-md-list__text"));
                list.Add(row);
            }

            return list;
        }

        private static string Bullet(MarkdownListItem item)
        {
            if (item.Checked.HasValue)
            {
                return item.Checked.Value ? "☑" : "☐";
            }

            if (char.IsDigit(item.Marker[0]))
            {
                return item.Marker;
            }

            return item.Indent % 2 == 0 ? "•" : "◦";
        }

        private static VisualElement BuildTable(MarkdownBlock block)
        {
            ScrollView scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("pl-md-table");
            int columns = 0;
            foreach (string[] row in block.Rows)
            {
                columns = Mathf.Max(columns, row.Length);
            }

            for (int rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
            {
                string[] cells = block.Rows[rowIndex];
                VisualElement row = new VisualElement();
                row.AddToClassList("pl-md-table__row");
                row.EnableInClassList("pl-md-table__row--header", rowIndex == 0);
                for (int column = 0; column < columns; column++)
                {
                    string cell = column < cells.Length ? cells[column] : string.Empty;
                    row.Add(ParleyStyles.RichLabel(InlineMarkdown.ToRichText(cell), "pl-md-table__cell"));
                }

                scroll.Add(row);
            }

            return scroll;
        }
    }
}