﻿//MIT License

//Copyright (c) 2020-2024 Jordi Corbilla

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ListToTable
{
    public class TableDic<TV, T> : Base<T> where T : class
    {
        public TableDic(Dictionary<TV, T> dictionary, Options options = null)
        {
            if (dictionary.Count == 0) return;
            if (options != null)
                Options = options;
            PropertyNames = [];
            MaxWidth = [];
            Keys = dictionary.Select(x => x.Key).ToList();
            Items = dictionary.Select(x => x.Value).ToList();

            var properties = typeof(T).GetProperties();

            //Add the additional Key
            PropertyNames.Add(new PropertyName(Options.KeyName));
            MaxWidth.Add(Options.KeyName, Options.KeyName.Length);

            foreach (var property in properties)
            {
                PropertyNames.Add(new PropertyName(property.Name));
                MaxWidth.Add(property.Name, property.Name.Length);
            }

            //Workout the key length
            foreach (var row in Keys)
            {
                var subValueLength = ObjectToString(row);

                if (subValueLength.Length > MaxWidth[Options.KeyName])
                    MaxWidth[Options.KeyName] = subValueLength.Length;
            }

            foreach (var row in Items)
                if (properties.Length != 0)
                {
                    foreach (var property in PropertyNames)
                    {
                        var value = GetValue(row, new PropertyName(property.Name));
                        if (value.Length > MaxWidth[property.Name])
                            MaxWidth[property.Name] = value.Length;
                    }
                }
                else
                {
                    var props = row.GetType().GetProperties();

                    var propertyIndex = 0;
                    foreach (var propertyInfo in props)
                    {
                        // Indexed property (collections)
                        if (propertyInfo.GetIndexParameters().Length > 0)
                        {
                            var reading = true;
                            var index = 0;
                            while (reading)
                                try
                                {
                                    var valueProp = $"{Options.DynamicName}{index}";
                                    var res = propertyInfo.GetValue(row, [index]);
                                    if (!MaxWidth.TryGetValue(valueProp, out int value))
                                    {
                                        PropertyNames.Add(new PropertyName(valueProp, index, propertyIndex));
                                        value = valueProp.Length;
                                        MaxWidth.Add(valueProp, value);
                                    }

                                    if (res.ToString().Length > value)
                                        MaxWidth[valueProp] = res.ToString().Length;
                                    index++;
                                }
                                catch (Exception)
                                {
                                    reading = false;
                                }
                        }
                        else
                        {
                            if (!MaxWidth.TryGetValue(propertyInfo.Name, out int value))
                            {
                                PropertyNames.Add(new PropertyName(propertyInfo.Name));
                                value = propertyInfo.Name.Length;
                                MaxWidth.Add(propertyInfo.Name, value);
                            }

                            var valueProp = GetValue(row, new PropertyName(propertyInfo.Name));
                            if (valueProp.Length > value)
                                MaxWidth[propertyInfo.Name] = valueProp.Length;
                        }

                        propertyIndex++;
                    }
                }
        }

        public List<TV> Keys { get; set; }

        public TableDic<TV, T> FilterColumns(string[] columns, FilterAction action = FilterAction.Exclude)
        {
            var filter = columns.ToDictionary(column => column, column => false);
            ColumnFilter = filter;
            ColumnAction = action;
            return this;
        }

        public TableDic<TV, T> HighlightValue(HighlightOperator operation)
        {
            if (!Operation.TryGetValue(operation.Field, out List<HighlightOperator> value))
                Operation.Add(operation.Field, [operation]);
            else
                value.Add(operation);
            return this;
        }

        public TableDic<TV, T> OverrideColumnsNames(Dictionary<string, string> columns)
        {
            ColumnNameOverrides = columns;
            foreach (var (key, value) in ColumnNameOverrides)
                if (value.Length > MaxWidth[key])
                    MaxWidth[key] = value.Length;
            return this;
        }

        public TableDic<TV, T> ColumnContentTextJustification(Dictionary<string, TextJustification> columns)
        {
            ColumnTextJustification = columns;
            return this;
        }

        public TableDic<TV, T> HighlightRows(ConsoleColor backgroundColor, ConsoleColor foregroundColor)
        {
            BackgroundColor = backgroundColor;
            ForegroundColor = foregroundColor;
            return this;
        }

        public void ToConsole()
        {
            if (Items.Count == 0) return;
            var s = "|";

            var filteredPropertyNames = FilterProperties();

            foreach (var property in filteredPropertyNames)
            {
                var headerName = property.Name;
                if (ColumnNameOverrides.TryGetValue(property.Name, out string value))
                    headerName = value;

                var length = MaxWidth[property.Name] - headerName.Length;

                var totalLength = $"{new string(' ', length)}{headerName.ToValidOutput()}".Length;
                var remaining = totalLength - $"{new string(' ', length / 2)}{headerName.ToValidOutput()}".Length;
                s += $" {new string(' ', length / 2)}{headerName.ToValidOutput()}{new string(' ', remaining)} |";
            }

            Console.WriteLine(s);

            s = filteredPropertyNames.Aggregate("|",
                (current, name) => current + $" {new string('-', MaxWidth[name.Name])} |");

            Console.WriteLine(s);

            for (var index = 0; index < Items.Count; index++)
            {
                var row = Items[index];
                Console.Write("|");
                foreach (var property in filteredPropertyNames)
                    if (property.Name == Options.KeyName)
                    {
                        var keyValueParsed = ObjectToString(Keys[index]);

                        var lengthParsed = MaxWidth[property.Name] - keyValueParsed.Length;

                        if (ColumnTextJustification.TryGetValue(property.Name, out TextJustification value))
                            switch (value)
                            {
                                case TextJustification.Centered:
                                    var totalLength = $"{new string(' ', lengthParsed)}{keyValueParsed.ToValidOutput()}"
                                        .Length;
                                    var remaining =
                                        totalLength -
                                        $"{new string(' ', lengthParsed / 2)}{keyValueParsed.ToValidOutput()}".Length;
                                    ConsoleRender(
                                        $"{new string(' ', lengthParsed / 2)}{keyValueParsed.ToValidOutput()}{new string(' ', remaining)}",
                                        property.Name);
                                    break;
                                case TextJustification.Right:
                                    ConsoleRender($"{new string(' ', lengthParsed)}{keyValueParsed.ToValidOutput()}",
                                        property.Name);
                                    break;
                                case TextJustification.Left:
                                    ConsoleRender($"{keyValueParsed.ToValidOutput()}{new string(' ', lengthParsed)}",
                                        property.Name);
                                    break;
                                case TextJustification.Justified:
                                    ConsoleRender($"{keyValueParsed.ToValidOutput()}{new string(' ', lengthParsed)}",
                                        property.Name);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        else
                            ConsoleRender($"{keyValueParsed.ToValidOutput()}{new string(' ', lengthParsed)}",
                                property.Name);
                    }
                    else
                    {
                        var valueProp = GetValue(row, property);
                        var length = MaxWidth[property.Name] - valueProp.Length;

                        if (ColumnTextJustification.TryGetValue(property.Name, out TextJustification value))
                            switch (value)
                            {
                                case TextJustification.Centered:
                                    var totalLength = $"{new string(' ', length)}{valueProp.ToValidOutput()}".Length;
                                    var remaining =
                                        totalLength - $"{new string(' ', length / 2)}{valueProp.ToValidOutput()}".Length;
                                    ConsoleRender(
                                        $"{new string(' ', length / 2)}{valueProp.ToValidOutput()}{new string(' ', remaining)}",
                                        property.Name);
                                    break;
                                case TextJustification.Right:
                                    ConsoleRender($"{new string(' ', length)}{valueProp.ToValidOutput()}", property.Name);
                                    break;
                                case TextJustification.Left:
                                    ConsoleRender($"{valueProp.ToValidOutput()}{new string(' ', length)}", property.Name);
                                    break;
                                case TextJustification.Justified:
                                    ConsoleRender($"{valueProp.ToValidOutput()}{new string(' ', length)}", property.Name);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        else
                            ConsoleRender($"{valueProp.ToValidOutput()}{new string(' ', length)}", property.Name);
                    }

                Console.Write(Environment.NewLine);
            }

            Console.WriteLine();
        }

        public override string ToString()
        {
            if (Items.Count == 0) return "";
            var s = "|";
            var stringBuilder = new StringBuilder();

            var filteredPropertyNames = FilterProperties();

            foreach (var property in filteredPropertyNames)
            {
                var headerName = property.Name;
                if (ColumnNameOverrides.TryGetValue(property.Name, out string value))
                    headerName = value;

                var length = MaxWidth[property.Name] - headerName.Length;

                var totalLength = $"{new string(' ', length)}{headerName.ToValidOutput()}".Length;
                var remaining = totalLength - $"{new string(' ', length / 2)}{headerName.ToValidOutput()}".Length;
                s += $" {new string(' ', length / 2)}{headerName.ToValidOutput()}{new string(' ', remaining)} |";
            }

            stringBuilder.AppendLine(s);

            s = filteredPropertyNames.Aggregate("|",
                (current, name) => current + $" {new string('-', MaxWidth[name.Name])} |");

            stringBuilder.AppendLine(s);

            for (var index = 0; index < Items.Count; index++)
            {
                var row = Items[index];
                stringBuilder.Append('|');
                foreach (var property in filteredPropertyNames)
                    if (property.Name == Options.KeyName)
                    {
                        var keyValueParsed = ObjectToString(Keys[index]);

                        var lengthParsed = MaxWidth[property.Name] - keyValueParsed.Length;

                        if (ColumnTextJustification.TryGetValue(property.Name, out TextJustification value))
                            switch (value)
                            {
                                case TextJustification.Centered:
                                    var totalLength = $"{new string(' ', lengthParsed)}{keyValueParsed.ToValidOutput()}"
                                        .Length;
                                    var remaining =
                                        totalLength -
                                        $"{new string(' ', lengthParsed / 2)}{keyValueParsed.ToValidOutput()}".Length;
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{new string(' ', lengthParsed / 2)}{keyValueParsed.ToValidOutput()}{new string(' ', remaining)}");
                                    stringBuilder.Append(" |");
                                    break;
                                case TextJustification.Right:
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{new string(' ', lengthParsed)}{keyValueParsed.ToValidOutput()}");
                                    stringBuilder.Append(" |");
                                    break;
                                case TextJustification.Left:
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{keyValueParsed.ToValidOutput()}{new string(' ', lengthParsed)}");
                                    stringBuilder.Append(" |");
                                    break;
                                case TextJustification.Justified:
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{keyValueParsed.ToValidOutput()}{new string(' ', lengthParsed)}");
                                    stringBuilder.Append(" |");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        else
                        {
                            stringBuilder.Append(' ');
                            stringBuilder.Append($"{keyValueParsed.ToValidOutput()}{new string(' ', lengthParsed)}");
                            stringBuilder.Append(" |");
                        }
                    }
                    else
                    {
                        var valueProp = GetValue(row, property);
                        var length = MaxWidth[property.Name] - valueProp.Length;

                        if (ColumnTextJustification.TryGetValue(property.Name, out TextJustification value))
                            switch (value)
                            {
                                case TextJustification.Centered:
                                    var totalLength = $"{new string(' ', length)}{valueProp.ToValidOutput()}".Length;
                                    var remaining = totalLength - $"{new string(' ', length / 2)}{valueProp.ToValidOutput()}".Length;
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{new string(' ', length / 2)}{valueProp.ToValidOutput()}{new string(' ', remaining)}");
                                    stringBuilder.Append(" |");
                                    break;
                                case TextJustification.Right:
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{new string(' ', length)}{valueProp.ToValidOutput()}");
                                    stringBuilder.Append(" |");
                                    break;
                                case TextJustification.Left:
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{valueProp.ToValidOutput()}{new string(' ', length)}");
                                    stringBuilder.Append(" |");
                                    break;
                                case TextJustification.Justified:
                                    stringBuilder.Append(' ');
                                    stringBuilder.Append($"{valueProp.ToValidOutput()}{new string(' ', length)}");
                                    stringBuilder.Append(" |");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        else
                        {
                            stringBuilder.Append(' ');
                            stringBuilder.Append($"{valueProp.ToValidOutput()}{new string(' ', length)}");
                            stringBuilder.Append(" |");
                        }
                    }

                stringBuilder.Append(Environment.NewLine);
            }

            stringBuilder.AppendLine();
            return stringBuilder.ToString();
        }

        public void ToCsv(string fileName)
        {
            using var file = new StreamWriter(fileName);
            file.WriteLine(ToCsv());
        }

        public string ToCsv()
        {
            var stringBuilder = new StringBuilder();
            if (Items.Count == 0) return "";
            var s = "";
            var filteredPropertyNames = FilterProperties();
            foreach (var property in filteredPropertyNames)
            {
                var headerName = property.Name;
                if (ColumnNameOverrides.TryGetValue(property.Name, out string value))
                    headerName = value;

                s += $"{headerName.ToCsv()},";
            }

            s = s.Remove(s.Length - 1);
            stringBuilder.AppendLine(s);

            for (var i = 0; i < Items.Count; i++)
            {
                var row = Items[i];
                s = "";
                foreach (var t in filteredPropertyNames)
                    if (t.Name == Options.KeyName)
                    {
                        var keyValueParsed = ObjectToString(Keys[i]);
                        s += $"{keyValueParsed.ToCsv()},";
                    }
                    else
                    {
                        var property = t;
                        var s1 = GetValue(row, property);
                        s += $"{s1.ToCsv()},";
                    }

                s = s.Remove(s.Length - 1);
                stringBuilder.AppendLine(s);
            }

            return stringBuilder.ToString();
        }

        public void ToMarkDown(string fileName, bool consoleVerbose = false)
        {
            using var file = new StreamWriter(fileName);
            file.WriteLine(ToMarkDown());

            if (consoleVerbose)
                Console.WriteLine(ToMarkDown());
        }

        public string ToMarkDown()
        {
            if (Items.Count == 0) return "";
            var stringBuilder = new StringBuilder();
            var s = "|";

            var filteredPropertyNames = FilterProperties();

            foreach (var property in filteredPropertyNames)
            {
                var headerName = property.Name;
                if (ColumnNameOverrides.TryGetValue(property.Name, out string value))
                    headerName = value;

                var length = MaxWidth[property.Name] - headerName.Length;
                s += $" {headerName.ToValidOutput()}{new string(' ', length)} |";
            }

            stringBuilder.AppendLine(s);

            s = "|";
            foreach (var property in filteredPropertyNames)
            {
                var columnSeparator = $" {new string('-', MaxWidth[property.Name])} |";
                if (ColumnTextJustification.TryGetValue(property.Name, out TextJustification value))
                    switch (value)
                    {
                        case TextJustification.Centered:
                            columnSeparator = columnSeparator.Replace("- ", ": ");
                            columnSeparator = columnSeparator.Replace(" -", " :");
                            break;
                        case TextJustification.Right:
                            columnSeparator = columnSeparator.Replace("- ", ": ");
                            break;
                        case TextJustification.Left:
                            columnSeparator = columnSeparator.Replace(" -", " :");
                            break;
                        case TextJustification.Justified:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                s += columnSeparator;
            }

            stringBuilder.AppendLine(s);

            for (var i = 0; i < Items.Count; i++)
            {
                var row = Items[i];
                s = "|";
                foreach (var property in filteredPropertyNames)
                    if (property.Name == Options.KeyName)
                    {
                        var keyValueParsed = ObjectToString(Keys[i]);
                        var length = MaxWidth[property.Name] - keyValueParsed.Length;
                        s += $" {keyValueParsed.ToValidOutput()}{new string(' ', length)} |";
                    }
                    else
                    {
                        var value = GetValue(row, property);
                        var length = MaxWidth[property.Name] - value.Length;
                        s += $" {value.ToValidOutput()}{new string(' ', length)} |";
                    }

                stringBuilder.AppendLine(s);
            }

            stringBuilder.AppendLine();

            return stringBuilder.ToString();
        }

        public void ToHtml(string fileName)
        {
            using var file = new StreamWriter(fileName);
            file.WriteLine(ToHtml());
        }

        public string ToHtml()
        {
            var stringBuilder = new StringBuilder();
            if (Items.Count == 0) return "";
            stringBuilder.AppendLine("<table style=\"border-collapse: collapse; width: 100%;\">");
            stringBuilder.AppendLine("<tr>");

            var filteredPropertyNames = FilterProperties();
            foreach (var property in filteredPropertyNames)
            {
                var headerName = property.Name;
                if (ColumnNameOverrides.TryGetValue(property.Name, out string value))
                    headerName = value;

                stringBuilder.AppendLine(
                    $"<th style=\"text-align: center; background-color: #04163d; color: white;padding: 4px;border: 1px solid #dddddd; font-family:monospace; font-size: 14px;\">{headerName.ToHtml()}</th>");
            }

            stringBuilder.AppendLine("</tr>");

            var rowNumber = 1;
            for (var index = 0; index < Items.Count; index++)
            {
                var row = Items[index];
                stringBuilder.AppendLine("<tr>");
                foreach (var property in filteredPropertyNames)
                {
                    var color = rowNumber % 2 == 0 ? "#f2f2f2" : "white";
                    var value = property.Name == Options.KeyName ? ObjectToString(Keys[index]) : GetValue(row, property);

                    stringBuilder.AppendLine(
                        $"<td style=\"text-align: right; color: black; background-color: {color};padding: 4px;border: 1px solid #dddddd; font-family:monospace; font-size: 14px;\">{value.ToHtml()}</td>");
                }

                rowNumber++;
                stringBuilder.AppendLine("</tr>");
            }

            stringBuilder.AppendLine("</table>");

            return stringBuilder.ToString();
        }

        public static TableDic<TV, T> Add(Dictionary<TV, T> dictionary)
        {
            return new TableDic<TV, T>(dictionary);
        }

        public static TableDic<TV, T> Add(Dictionary<TV, T> dictionary, Options options)
        {
            return new TableDic<TV, T>(dictionary, options);
        }
    }
}