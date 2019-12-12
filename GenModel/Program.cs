using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;

namespace GenModel
{

    public static class Program
    {

        private static Dictionary<string, string> TsTypeMap = new Dictionary<string, string>()
        {
            {"bool","boolean" },
            {"int","number" },
            {"double","number" },
            {"decimal","number" },
            {"long","number" },
            {"guid","string" },
            {"timespan","string" },
            {"datetime","string|Date" },
            {"datetimeoffset","string|Date" },
        };

        private static string[] SpecialExtends = {
            "enum",
            "flags",
            "interface"
        };

        private static char[] ExtendSplit = {' ',','};

        private static string ToTsType(string type)
        {
            var isCollection = false;

            if (type.Contains('<'))
            {
                type = type.Substring(type.IndexOf('<') + 1).Replace(">", "");
                isCollection = true;
            }else if (type.Contains('['))
            {
                type = type.Replace("[", "").Replace("]", "");
                isCollection = true;
            }

            var isOptional = type.EndsWith("?");
            if (isOptional)
            {
                type = type.Replace("?", "");
            }

            if(TsTypeMap.TryGetValue(type.ToLower(),out string tsType))
            {
                type = tsType;
            }

            return type + (isCollection?"[]":"") + (isOptional ? "|null":"");
        }

        static void Main(string[] args)
        {
            if(args==null){
                args=new string[0];
            }

            string file = null;
            string csOut = null;
            string tsOut = null;
            string ns = null;
            string collectionType = "List";

            for(int i=0;i<args.Length;i++){
                switch(args[i].ToLower()){
                    case "-csv":
                        file=args[++i];
                        break;
                        
                    case "-csout":
                        csOut=args[++i];
                        if(csOut=="null"){
                            csOut=null;
                        }
                        break;
                        
                    case "-collectiontype":
                        collectionType=args[++i];
                        break;

                    case "-tsout":
                        tsOut = args[++i];
                        if(tsOut=="null"){
                            tsOut=null;
                        }
                        break;

                    case "-namespace":
                        ns=args[++i];
                        break;
                        
                }
            }
            
            if(file==null){
                throw new Exception("-csv required");
            }

            if(ns==null){
                throw new Exception("-namespace required");
            }


            if (csOut != null)
            {
                if (Directory.Exists(csOut))
                {
                    Directory.Delete(csOut, true);
                }
                Directory.CreateDirectory(csOut);
            }

            var builder = new StringBuilder();
            var tsBuilder = new StringBuilder();
            var tsFile = new StringBuilder();
            var nameReg=new Regex(@"\w+");
            var annotationReg=new Regex(@"@(\w+)\s*:\s*(\w+)");
            var dbSets=new StringBuilder();

            using (var reader = new StreamReader(file))
            using (var csv = new CsvReader(reader))
            {
                
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var type=csv.GetField("Shape Library");
                    if(type!="Entity Relationship"){
                        continue;
                    }

                    builder.Clear();
                    tsBuilder.Clear();
                    type = csv.GetField("Text Area 1");
                    var parts = type.Split(':');
                    type = parts[0];
                    var extend = (parts.Length > 1 ? parts[1] : string.Empty)
                        .Split(ExtendSplit, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s=>s.Trim())
                        .ToArray();
                    var isInterface = extend.Contains("interface");
                    var isEnum = extend.Contains("enum");
                    var isFlags = extend.Contains("flags");
                    type=nameReg.Match(type).Value;
                    for(int i=11;;i++){
                        csv.TryGetField<string>(i,out string value);
                        if(string.IsNullOrWhiteSpace(value)){
                            break;
                        }
                        
                        value=value.Trim();
                        if(value.StartsWith("#") || value.StartsWith(".")){
                            continue;
                        }
                        
                        int c=value.IndexOf('-');
                        if(c!=-1){
                            value=value.Substring(0,c).Trim();
                        }

                        var annotations=new Dictionary<string,string>();
                        value = annotationReg.Replace(value, (m) =>
                        {
                            annotations[m.Groups[1].Value] = m.Groups[2].Value;
                            return string.Empty;
                        });
                        int max;
                        if(annotations.TryGetValue("max",out string mv))
                        {
                            if(!int.TryParse(mv,out max))
                            {
                                throw new FormatException("Invalid @max annotation format: @max:"+mv);
                            }
                        }
                        else
                        {
                            max = 255;
                        }
                        
                        var prop=value.Split(':');
                        string name;
                        string propType;
                        bool isTsOptional=false;
                        if(prop.Length==1){
                            throw new Exception(type+"."+value+" requires a type");
                        }else{
                            name=nameReg.Match(prop[0]).Value;
                            propType=prop[1].Trim();
                        }
                        
                        if(propType.Contains('[')){
                            if(propType=="[]"){
                                propType=name.Substring(0,name.Length-1);
                            }else{
                                propType=propType.Replace("[","").Replace("]","");
                            }
                            propType=collectionType+"<"+propType+">";
                        }

                        if(propType.ToLower() == "updateid")
                        {
                            builder.Append($"        [Timestamp]\n");
                            propType="DateTime?";
                            isTsOptional=true;
                        }

                        if (propType.ToLower() == "string")
                        {
                            builder.Append($"        [MaxLength({max})]\n");
                        }

                        if (isEnum)
                        {
                            builder.Append($"        {name} = {propType},\n");
                            tsBuilder.Append($"    {name}{(isTsOptional?"?":"")}={ToTsType(propType)},\n");
                        }
                        else
                        {
                            builder.Append($"        {(isInterface ? "" : "public ")}{propType} {name} {{ get; set; }}\n");
                            tsBuilder.Append($"    {name}{(isTsOptional?"?":"")}:{ToTsType(propType)};\n");
                            
                            if( name!="Id" && name.EndsWith("Id") &&
                                (propType == "int" || propType == "int?" || propType == "Guid" || propType == "Guid?"))
                            {
                                name = name.Substring(0, name.Length - 2);
                                if(prop.Length>2){
                                    propType=prop[2].Trim();
                                }else{
                                    propType=name;
                                }
                                builder.Append($"        public {propType} {name} {{ get; set; }}\n");
                                tsBuilder.Append($"    {name}:{ToTsType(propType)};\n");
                            }
                            builder.Append("\n");
                            tsBuilder.Append("\n");
                        }
                    }

                    if (!isEnum)
                    {
                        var pl=type;
                        if(pl.EndsWith("s")){
                            pl+="es";
                        }else{
                            pl+="s";
                        }
                        dbSets.AppendLine($"        public virtual DbSet<{type}> {pl} {{ get; set; }}");
                    }

                    if(csOut!=null){
                        var filepath = Path.GetFullPath(Path.Combine(csOut, type + ".cs"));
                        $"// {filepath}".Dump();

                        var extendsString = string.Join(", ", extend
                            .Where(e => !SpecialExtends.Contains(e)));

                        var def = string.Format(
                            tmpl,
                            isFlags ? "[Flags]" : "",
                            isEnum ? "enum" : (isInterface?"interface":"partial class"),
                            type,
                            builder.ToString(),
                            ns,
                            string.IsNullOrWhiteSpace(extendsString)?"":": "+extendsString)
                            .Dump();
                        File.WriteAllText(filepath, def);
                    }



                    if (tsOut != null)
                    {
                        var def = string.Format(tsTmpl, "", isEnum ? "enum" : "interface", type, tsBuilder.ToString()).Dump();
                        tsFile.Append(def);
                    }


                    "".Dump();
                }

            }

            if (tsOut != null)
            {
                $"tsOut = {tsOut}".Dump();
                File.WriteAllText(tsOut, tsFile.ToString());
            }
            
            dbSets.ToString().Dump();
        }

        static T Dump<T>(this T obj){
            Console.WriteLine(obj);
            return obj;
        }

const string tmpl =
@"using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace {4}
{{
    {0}
    public {1} {2} {5}
    {{
{3}
    }}
}}
";

const string tsTmpl =
@"

{0}
export {1} {2}
{{
{3}
}}
";
    }
}
