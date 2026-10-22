using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace UFN.App
{
    internal sealed class LegacyJson
    {
        readonly string text;int cursor;LegacyJson(string text){this.text=text;}
        public static object Parse(string text){var reader=new LegacyJson(text);var value=reader.Value();reader.Space();if(reader.cursor!=text.Length)throw new InvalidDataException("Trailing JSON data");return value;}
        void Space(){while(cursor<text.Length&&char.IsWhiteSpace(text[cursor]))cursor++;}
        char Take(){if(cursor>=text.Length)throw new InvalidDataException("Incomplete atlas JSON");return text[cursor++];}
        void Require(char c){Space();if(Take()!=c)throw new InvalidDataException("Invalid atlas JSON at "+cursor);}
        object Value()
        {
            Space();if(cursor>=text.Length)throw new InvalidDataException("Missing JSON value");char c=text[cursor];
            if(c=='"')return String();
            if(c=='{'){cursor++;var d=new Dictionary<string,object>();Space();if(text[cursor]=='}'){cursor++;return d;}while(true){string key=String();Require(':');d.Add(key,Value());Space();c=Take();if(c=='}')return d;if(c!=',')throw new InvalidDataException("Object separator");Space();}}
            if(c=='['){cursor++;var a=new List<object>();Space();if(text[cursor]==']'){cursor++;return a;}while(true){a.Add(Value());Space();c=Take();if(c==']')return a;if(c!=',')throw new InvalidDataException("Array separator");}}
            int start=cursor;while(cursor<text.Length&&!char.IsWhiteSpace(text[cursor])&&",]}".IndexOf(text[cursor])<0)cursor++;
            string token=text.Substring(start,cursor-start);if(token=="true")return true;if(token=="false")return false;if(token=="null")return null;
            return double.Parse(token,NumberStyles.Float,CultureInfo.InvariantCulture);
        }
        string String()
        {
            Require('"');var b=new StringBuilder();while(true){char c=Take();if(c=='"')return b.ToString();if(c!='\\'){b.Append(c);continue;}c=Take();switch(c){case '"':case '\\':case '/':b.Append(c);break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;case 'u':b.Append((char)int.Parse(text.Substring(cursor,4),NumberStyles.HexNumber));cursor+=4;break;default:throw new InvalidDataException("JSON escape");}}
        }
    }
}
