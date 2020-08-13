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

        private static readonly string[] NoPlural={
            "Media",
            "Data"
        };

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

        private static readonly string[] SpecialExtends = {
            "enum",
            "flags",
            "interface",
            "jsonIgnore"
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
            string dbInterface = null;
            string dbClass = null;
            string dbInterfaceNs = null;
            string dbClassNs = null;
            string ns = null;
            string collectionType = "List";
            bool jsonNav=true;

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
                        ns = args[++i];
                        if (ns == "null")
                        {
                            ns = null;
                        }
                        break;

                    case "-dbinterface":
                        dbInterface = args[++i];
                        if (dbInterface == "null")
                        {
                            dbInterface = null;
                        }
                        break;

                    case "-dbclass":
                        dbClass = args[++i];
                        if (dbClass == "null")
                        {
                            dbClass = null;
                        }
                        break;

                    case "-dbinterfacens":
                        dbInterfaceNs = args[++i];
                        if (dbInterfaceNs == "null")
                        {
                            dbInterfaceNs = null;
                        }
                        break;

                    case "-dbclassns":
                        dbClassNs = args[++i];
                        if (dbClassNs == "null")
                        {
                            dbClassNs = null;
                        }
                        break;

                    case "-jsonnav":
                        jsonNav=bool.Parse(args[++i]);
                        break;


                }
            }
            
            if(file==null){
                throw new Exception("-csv required");
            }

            if(ns==null){
                throw new Exception("-namespace required");
            }

            if (dbClassNs == null)
            {
                dbClassNs = ns;
            }

            if (dbInterfaceNs == null)
            {
                dbInterfaceNs = ns;
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
            var nameReg=new Regex(@"[\w<>]+");
            var annotationReg=new Regex(@"@([\w!?]+)\s*(:\s*(\w+))?");
            var dbSets = new StringBuilder();
            var dbSetsInterface = new StringBuilder();

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
                    var parts = type.Split(':',2,StringSplitOptions.None);
                    type = parts[0];
                    var extend = (parts.Length > 1 ? parts[1] : string.Empty)
                        .Split(ExtendSplit, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s=>s.Trim())
                        .ToArray();
                    var isInterface = extend.Contains("interface");
                    var isEnum = extend.Contains("enum");
                    var isFlags = extend.Contains("flags");
                    var ignoreJsonClass = extend.Contains("jsonIgnore");
                    var usingNs=new Dictionary<string,bool>();

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
                            var gv=m.Groups[3].Value;
                            if(!string.IsNullOrWhiteSpace(gv)){
                                annotations[m.Groups[1].Value] = gv;
                            }else{
                                annotations[m.Groups[1].Value] = "true";
                            }
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

                        string att;
                        
                        bool json;
                        bool jsonExplicit;
                        if(annotations.TryGetValue("json",out att)){
                            jsonExplicit=true;
                            if(!bool.TryParse(att,out json)){
                                throw new FormatException("Invalid @jsonIgnore value:"+att);
                            }
                        }else{
                            jsonExplicit=false;
                            json=true;
                        }
                        var jsonNavProp=jsonNav?json:json&&jsonExplicit;

                        string defaultValue=null;
                        bool enumClassDefault=false;

                        if(annotations.TryGetValue("ecd",out att)){
                            if(!bool.TryParse(att,out enumClassDefault)){
                                throw new FormatException("Invalid @ecd annotation format: @ecd:"+att);
                            }
                            if(enumClassDefault){
                                annotations["readonly"]="true";

                            }
                        }

                        bool isReadonly=false;
                        if(annotations.TryGetValue("readonly",out att)){
                            if(!bool.TryParse(att,out isReadonly)){
                                throw new FormatException("Invalid @readonly annotation format: @readonly:"+att);
                            }
                        }

                        bool notMapped=false;
                        if(annotations.TryGetValue("notMapped",out att)){
                            if(!bool.TryParse(att,out notMapped)){
                                throw new FormatException("Invalid @notMapped annotation format: @notMapped:"+att);
                            }
                            usingNs["System.ComponentModel.DataAnnotations.Schema"]=true;
                        }

                        bool isJsonOptional=true;
                        
                        
                        var prop=value.Split(':');
                        string name;
                        string propType;
                        if(prop.Length==1){
                            throw new Exception(type+"."+value+" requires a type");
                        }else{
                            name=nameReg.Match(prop[0]).Value;
                            propType=prop[1].Trim();
                        }

                        var isRequired=propType.EndsWith('!');
                        if(isRequired){
                            propType=propType.Replace("!","");
                            isJsonOptional=false;
                        }
                        
                        if(propType.Contains('[')){
                            if(!jsonNavProp){
                                json=false;
                            }
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
                        }

                        if (propType.ToLower() == "string" && max>0)
                        {
                            builder.Append($"        [MaxLength({max})]\n");
                        }

                        if(enumClassDefault){
                            annotations["default"]=propType+"."+type;
                        }

                        if(annotations.ContainsKey("default")){
                            usingNs["System.ComponentModel"]=true;
                            defaultValue=annotations["default"];
                        }

                        if(name=="Id"){
                            isJsonOptional=false;
                        }

                        if(annotations.TryGetValue("json!",out att)){
                            if(!bool.TryParse(att,out var jsonRequired)){
                                throw new FormatException("Invalid @json! annotation format: @json!:"+att);
                            }
                            isJsonOptional=!jsonRequired;
                        }

                        if(annotations.TryGetValue("json?",out att)){
                            if(!bool.TryParse(att,out var jsonOptional)){
                                throw new FormatException("Invalid @json? annotation format: @json?:"+att);
                            }
                            isJsonOptional=jsonOptional;
                        }

                        if (isEnum)
                        {
                            builder.Append($"        {name} = {propType},\n");
                            if(json){
                                tsBuilder.Append($"    {name}={ToTsType(propType)},\n");
                            }
                        }
                        else
                        {
                            if(!json){
                                builder.Append("        [Newtonsoft.Json.JsonIgnore]\n");
                                builder.Append("        [System.Text.Json.Serialization.JsonIgnore]\n");
                            }
                            if(isRequired){
                                builder.Append("        [Required]\n");
                            }
                            if(notMapped){
                                builder.Append("        [NotMapped]\n");
                            }
                            var defaultSyntax=defaultValue==null?"":" = "+defaultValue+";";
                            if(defaultValue!=null){
                                builder.Append("        [DefaultValue("+defaultValue+")]\n");
                            }
                            builder.Append($"        {(isInterface ? "" : "public ")}{propType} {name} {{ get;{(isReadonly?"":" set;")} }}{defaultSyntax}\n");
                            if(json){
                                tsBuilder.Append($"    {name}{(isJsonOptional?"?":"")}:{ToTsType(propType)};\n");
                            }
                            
                            if( name!="Id" && name.EndsWith("Id") && !notMapped &&
                                (propType == "int" || propType == "int?" || propType == "Guid" || propType == "Guid?"))
                            {
                                name = name.Substring(0, name.Length - 2);
                                if(prop.Length>2){
                                    propType=prop[2].Trim();
                                }else{
                                    propType=name;
                                }
                                
                                if(propType!="none"){
                                    if(!jsonNavProp){
                                        builder.Append("        [Newtonsoft.Json.JsonIgnore]\n");
                                        builder.Append("        [System.Text.Json.Serialization.JsonIgnore]\n");
                                    }
                                    builder.Append($"        {(isInterface ? "" : "public ")}{propType} {name} {{ get; set; }}\n");
                                    if(jsonNavProp){
                                        tsBuilder.Append($"    {name}{(isJsonOptional?"?":"")}:{ToTsType(propType)};\n");
                                    }
                                }
                            }
                            builder.Append("\n");
                            if(json){
                                tsBuilder.Append("\n");
                            }
                        }
                    }

                    if (!isEnum && !isInterface)
                    {
                        var pl=type;
                        if(NoPlural.All(np=>!pl.EndsWith(np))){
                            if (pl.EndsWith("s"))
                            {
                                pl += "es";
                            }
                            else if (pl.EndsWith("y"))
                            {
                                pl = pl.Substring(0, pl.Length - 1) + "ies";
                            }
                            else
                            {
                                pl+="s";
                            }
                        }
                        if (dbClass != null)
                        {
                            dbSets.AppendLine($"        public virtual DbSet<{type}> {pl} {{ get; set; }}");
                        }
                        if (dbInterface != null)
                        {
                            dbSetsInterface.AppendLine($"        DbSet<{type}> {pl} {{ get; }}");
                        }
                        
                    }

                    if(csOut!=null){
                        var filepath = Path.GetFullPath(Path.Combine(csOut, type.Split('<')[0] + ".cs"));
                        $"// {filepath}".Dump();

                        var extendsString = string.Join(", ", extend
                            .Where(e => !SpecialExtends.Contains(e) && !e.Contains(':')));

                        var wheres=extend.Where(e=>e.Contains(':')).Select(e=>"where "+e).ToList();
                        if(wheres.Count>0){
                            var str=string.Join(' ',wheres);
                            extendsString=str+(string.IsNullOrWhiteSpace(extendsString)?"":", "+str);
                        }else{
                            extendsString=string.IsNullOrWhiteSpace(extendsString)?"":": "+extendsString;
                        }

                        var usingSyntax=string.Join("",usingNs.Select(p=>"using "+p.Key+";\n"));

                        var def = string.Format(
                            tmpl,
                            isFlags ? "[Flags]" : "",
                            isEnum ? "enum" : (isInterface?"interface":"partial class"),
                            type,
                            builder.ToString(),
                            ns,
                            extendsString,
                            usingSyntax)
                            .Dump();
                        File.WriteAllText(filepath, def);
                    }



                    if (tsOut != null && !ignoreJsonClass)
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

            if (dbClass != null)
            {
                var name = Path.GetFileName(dbClass);
                int x = name.IndexOf('.');
                if (x != -1)
                {
                    name = name.Substring(0,x);
                }
                var value = string.Format(
                    dbSetTmpl,
                    dbClassNs,
                    "class " + name,
                    dbSets.ToString());
                File.WriteAllText(dbClass, value);
                value.Dump();
            }

            if (dbInterface != null)
            {
                var name = Path.GetFileName(dbInterface);
                int x = name.IndexOf('.');
                if (x != -1)
                {
                    name = name.Substring(0,x);
                }
                var value = string.Format(
                    dbSetTmpl,
                    dbInterfaceNs,
                    "interface " + name,
                    dbSetsInterface.ToString());
                File.WriteAllText(dbInterface, value);
                value.Dump();
            }
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
{6}
namespace {4}
{{
    {0}
    public {1} {2} {5}
    {{
{3}
    }}
}}
";

const string dbSetTmpl =
@"using System;
using Microsoft.EntityFrameworkCore;

namespace {0}
{{
    public partial {1}
    {{
{2}
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
