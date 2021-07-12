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

        private class GmType
        {
            public string Type{get;set;}
            public string Collection{get;set;}
            public List<GmProp> Props{get;}=new List<GmProp>();
            public GmProp GetProp(string name)
            {
                var prop=Props.FirstOrDefault(p=>p.Name==name);
                if(prop!=null){
                    return prop;
                }
                prop=new GmProp(){Name=name};
                Props.Add(prop);
                return prop;
            }
        }

        private class GmProp
        {
            public string Name{get;set;}
            public string Type{get;set;}
            public string BaseType{get;set;}
            public bool IsRefCollection{get;set;}
            public bool IsRefSingle{get;set;}
            public bool IsRefSingleId{get;set;}
            public GmProp RefIdProp{get;set;}
        }

        private class GmRelation
        {
            public GmType One{get;set;}
            public GmType Many{get;set;}
        }

        private enum ConfigOptionType
        {
            OneToMany=0,
            DeleteBehavior=1
        }

        private class ConfigOption
        {
            public ConfigOptionType Type{get;}
            public List<string> Args{get;}


            public string DeleteBehavior{get;}

            public string OneManyTypeOne{get;set;}
            public string OneManyPropOne{get;set;}
            public string OneManyTypeMany{get;set;}
            public string OneManyPropMany{get;set;}

            public ConfigOption(string line)
            {
                var args=line.Split(':').Select(o=>o.Trim()).ToList();
                try{
                    Type=Enum.Parse<ConfigOptionType>(args[0],true);
                }catch{
                    throw new FormatException("Invalid ConfigOption type - "+args[0]);
                }
                args.RemoveAt(0);
                Args=args;

                switch(Type){

                    case ConfigOptionType.DeleteBehavior:
                        DeleteBehavior=args[0];
                        break;

                    case ConfigOptionType.OneToMany:
                        var one=args[0].Split('.').Select(v=>v.Trim()).ToArray();
                        var many=args[1].Split('.').Select(v=>v.Trim()).ToArray();
                        if(one.Length!=2 || many.Length!=2){
                            throw new FormatException("Invlaid OneToMany - "+line);
                        }
                        OneManyTypeOne=one[0];
                        OneManyPropOne=one[1];
                        OneManyTypeMany=many[0];
                        OneManyPropMany=many[1];
                        break;

                }
            }
        }

        private enum MetaType
        {
            None = 0,
            Generator = 1
        }

        private static readonly string[] NoPlural={
            "Media",
            "Data",
            "Settings"
        };

        private static Dictionary<string, string> TsTypeMap = new Dictionary<string, string>()
        {
            {"object","any" },
            {"bool","boolean" },
            {"int","number" },
            {"double","number" },
            {"decimal","number" },
            {"long","number" },
            {"guid","string" },
            {"timespan","string" },
            {"datetime","string|Date" },
            {"datetimeoffset","string|Date" },
            {"nettopologysuite.geometries.point","GeoPoint"}
        };

        private static readonly string[] SpecialExtends = {
            "enum",
            "flags",
            "interface",
            "jsonIgnore",
            "notMapped"
        };

        private const string ConfigType="_Config_";

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

            var typeMap=new Dictionary<string,GmType>();
            var configOptions=new List<ConfigOption>();

            string file = null;
            string csOut = null;
            string tsOut = null;
            string dbHookOut = null;
            string tsHeader = null;
            string dbInterface = null;
            string dbClass = null;
            string dbInterfaceNs = null;
            string dbClassNs = null;
            string ns = null;
            string collectionType = "List";
            bool jsonNav=true;
            bool firestore=false;
            string uidInterface=null;
            string uidProp="Uid";

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

                    case "-tsheader":
                        tsHeader=args[++i];
                        if(tsHeader=="null"){
                            tsHeader=null;
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

                    case "-firestore":
                        firestore=bool.Parse(args[++i]);
                        break;

                    case "-uidinterface":
                        uidInterface=args[++i];
                        if(uidInterface=="null"){
                            uidInterface=null;
                        }
                        break;

                    case "-uidprop":
                        uidProp=args[++i];
                        break;

                    case "-dbhooksout":
                        dbHookOut=args[++i];
                        if(dbHookOut=="null"){
                            dbHookOut=null;
                        }
                        break;

                    case "-attach-debugger":
                        Console.WriteLine("Waiting for Debugger to Attach");
                        Console.WriteLine("PID:"+System.Diagnostics.Process.GetCurrentProcess().Id);
                        while(!System.Diagnostics.Debugger.IsAttached){
                            System.Threading.Thread.Sleep(100);
                        }
                        System.Diagnostics.Debugger.Break();
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

            

            var generators=new Dictionary<string,List<string>>();


            // First Pass
            using (var reader = new StreamReader(file))
            using (var csv = new CsvReader(reader))
            {
                
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                { // class loop
                    var type=csv.GetField("Shape Library");
                    if(type!="Entity Relationship"){
                        continue;
                    }
                    
                    type = csv.GetField("Text Area 1")?.Trim().Trim('"');
                    if(type==null || !type.StartsWith("@")){
                        continue;
                    }

                    MetaType mType;
                    if(!Enum.TryParse<MetaType>(type.Substring(1),out mType)){
                        continue;
                    }

                    for(int i=11;;i++)
                    { // property loop
                        csv.TryGetField<string>(i,out string value);
                        if(string.IsNullOrWhiteSpace(value)){
                            break;
                        }
                        
                        value=value.Trim();
                        if(value.StartsWith("#") || value.StartsWith(".")){
                            continue;
                        }

                        switch(mType){
                            case MetaType.Generator:{
                                var parts=value.Split(':',2);
                                if(parts.Length!=2){
                                    throw new FormatException("Invalid Generator property - "+value);
                                }

                                var key=parts[0];

                                if(!generators.ContainsKey(key)){
                                    generators[key]=new List<string>();
                                }
                                generators[key].Add(parts[1]);

                                break;
                            }
                        }

                    }

                }
            }

            StringBuilder dbSets=null;
            StringBuilder tsFile=null;
            StringBuilder dbHooksFile=null;
            bool hookRefSingle=false;
            bool hookRefCollection=false;
            var hookImports=new List<string>();
            StringBuilder dbSetsInterface=null;
            var nameReg=new Regex(@"[\w<>]+");
            var annotationReg=new Regex(@"@([\w!?]+)\s*(:\s*(\w+))?");
            var generatedProperties=new Dictionary<string,List<string>>();

            for(int pass=2;pass<=3;pass++){
                var writeToFile=pass==3;
                var includeGeneratedProps=pass==3;
                var generateProperties=pass==2;

                dbSets = new StringBuilder();
                dbSetsInterface = new StringBuilder();
                tsFile = new StringBuilder();
                dbHooksFile = new StringBuilder();

                var builder = new StringBuilder();
                var tsBuilder = new StringBuilder();

                
                using (var reader = new StreamReader(file))
                using (var csv = new CsvReader(reader))
                {
                    
                    csv.Read();
                    csv.ReadHeader();
                    while (csv.Read())
                    { // class loop
                        var type=csv.GetField("Shape Library");
                        if(type!="Entity Relationship"){
                            continue;
                        }

                        builder.Clear();
                        tsBuilder.Clear();
                        type = csv.GetField("Text Area 1")?.Trim('"');
                        if(type.StartsWith('@') || type=="_"){
                            continue;
                        }
                        var parts = type.Split(':',2,StringSplitOptions.None);
                        type = parts[0];
                        var extend = (parts.Length > 1 ? parts[1] : string.Empty)
                            .Split(ExtendSplit, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s=>s.Trim())
                            .ToList();
                        var isInterface = extend.Contains("interface");
                        var isEnum = extend.Contains("enum");
                        var isFlags = extend.Contains("flags");
                        var ignoreJsonClass = extend.Contains("jsonIgnore");
                        var typeNotMapped = extend.Contains("notMapped");
                        var usingNs=new Dictionary<string,bool>();

                        type=nameReg.Match(type).Value;
                        var copyFunctions=new Dictionary<string,List<string>>();
                        var propertyLines=new List<string>();
                        
                        var pl=ToPlural(type);

                        if(!typeMap.TryGetValue(type,out var gmType) && type!=ConfigType){
                            gmType=new GmType()
                            {
                                Collection=pl,
                                Type=type
                            };
                            typeMap[type]=gmType;
                        }

                        
                        for(int i=11;;i++)
                        { // property loop
                            csv.TryGetField<string>(i,out string value);
                            if(string.IsNullOrWhiteSpace(value)){
                                break;
                            }
                            
                            value=value.Trim();
                            if(value.StartsWith("#") || value.StartsWith(".")){
                                continue;
                            }

                            propertyLines.Add(value);
                        }

                        if(type==ConfigType){
                            if(pass==2){
                                foreach(var v in propertyLines){
                                    configOptions.Add(new ConfigOption(v));
                                }
                            }
                            continue;
                        }

                        if(includeGeneratedProps && generatedProperties.TryGetValue(type,out var genLines)){
                            propertyLines.AddRange(genLines);
                        }

                        foreach(var v in propertyLines){
                            
                            var value=v;
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

                            string att;

                            int max;
                            if(annotations.TryGetValue("max",out att))
                            {
                                if(!int.TryParse(att,out max))
                                {
                                    throw new FormatException("Invalid @max annotation format: @max:"+att);
                                }
                            }
                            else
                            {
                                max = 255;
                            }

                            string[] gen=null;
                            if(annotations.TryGetValue("gen",out att)){
                                gen=att.Split(',');
                            }
                            
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
                            bool isCollection=false;
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

                            var basePropType=propType;
                            
                            if(propType.Contains('[')){
                                if(!jsonNavProp){
                                    json=false;
                                }
                                if(propType=="[]"){
                                    propType=name.Substring(0,name.Length-1);
                                }else{
                                    propType=propType.Replace("[","").Replace("]","");
                                }
                                basePropType=propType;
                                propType=collectionType+"<"+propType+">";
                                isCollection=true;
                            }

                            if(annotations.TryGetValue("copy",out att)){
                                if(!copyFunctions.ContainsKey(att)){
                                    copyFunctions[att]=new List<string>();
                                }
                                copyFunctions[att].Add(name);
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

                            if(gen!=null && generateProperties){
                                foreach(var genName in gen){
                                    if(!generators.TryGetValue(genName,out var genTemplates)){
                                        throw new FormatException("No generator found for "+genName);
                                    }
                                    foreach(var template in genTemplates){
                                        var tParts=template.Split(':',2);
                                        if(tParts.Length!=2){
                                            throw new FormatException($"Invalid generator line - {genName} - {template}");
                                        }
                                        var targetName=tParts[0].Trim();
                                        if(!generatedProperties.TryGetValue(targetName,out var lines)){
                                            generatedProperties[targetName]=lines=new List<string>();
                                        }
                                        lines.Add(tParts[1]
                                            .Replace("{name}",name)
                                            .Replace("{propType}",propType).Trim());
                                    }
                                }
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

                                var gmProp=gmType.GetProp(name);
                                gmProp.Type=propType;
                                gmProp.BaseType=basePropType;

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
                                if(firestore && !notMapped && json){
                                    builder.Append("        [Google.Cloud.Firestore.FirestoreProperty]\n");

                                }
                                if(uidInterface!=null && name==uidProp){
                                    extend.Add(uidInterface);
                                }
                                var defaultSyntax=defaultValue==null?"":" = "+defaultValue+";";
                                if(defaultValue!=null){
                                    builder.Append("        [DefaultValue("+defaultValue+")]\n");
                                }
                                builder.Append($"        {(isInterface ? "" : "public ")}{propType} {name} {{ get;{(isReadonly?"":" set;")} }}{defaultSyntax}\n");
                                if(json){
                                    tsBuilder.Append($"    {name}{(isJsonOptional?"?":"")}:{ToTsType(propType)};\n");
                                }

                                if(isCollection){
                                    gmProp.IsRefCollection=true;

                                    var relationConfig=configOptions.FirstOrDefault(c=>
                                        c.Type==ConfigOptionType.OneToMany &&
                                        c.OneManyPropMany==name &&
                                        c.OneManyTypeOne==basePropType);

                                    var revProp=
                                        (typeMap.TryGetValue(basePropType,out var vp)?vp:null)
                                        ?.Props.FirstOrDefault(p=>p.IsRefSingle && p.Type==type && p.RefIdProp!=null &&
                                        (relationConfig==null?true:p.Name==relationConfig.OneManyPropOne));
                                    
                                if(revProp!=null){
                                        dbHooksFile.AppendFormat(dbHookRefCollectionTmpl,
                                            //0 BaseType, 1 RefProperty, 2 RefType, 3 BaseCollection, 4 RefCollection, 5 foreignKey
                                            type,name,basePropType,pl,ToPlural(basePropType),revProp.RefIdProp.Name);
                                        hookRefCollection=true;
                                    }
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
                                        gmProp.IsRefSingleId=true;

                                        var gmRefProp=gmType.GetProp(name);
                                        gmRefProp.IsRefSingle=true;
                                        gmRefProp.Type=propType;
                                        gmRefProp.BaseType=propType;
                                        gmRefProp.RefIdProp=gmProp;

                                        if(!jsonNavProp){
                                            builder.Append("        [Newtonsoft.Json.JsonIgnore]\n");
                                            builder.Append("        [System.Text.Json.Serialization.JsonIgnore]\n");
                                        }
                                        builder.Append($"        {(isInterface ? "" : "public ")}{propType} {name} {{ get; set; }}\n");
                                        if(jsonNavProp){
                                            tsBuilder.Append($"    {name}{(isJsonOptional?"?":"")}:{ToTsType(propType)};\n");
                                        }
                                        dbHooksFile.AppendFormat(dbHookRefSingleTmpl,
                                            //0 BaseType, 1 RefProperty, 2 RefType, 3 BaseCollection, 4 RefCollection, 5 foreignKey
                                            type,name,propType,pl,ToPlural(propType),name+"Id");
                                        hookRefSingle=true;
                                    }
                                }

                                builder.Append("\n");
                                if(json){
                                    tsBuilder.Append("\n");
                                }
                            }
                        }

                        if (!isEnum && !isInterface && !typeNotMapped)
                        {
                            if (dbClass != null)
                            {
                                dbSets.AppendLine($"        public virtual DbSet<{type}> {pl} {{ get; set; }}");
                            }
                            if (dbInterface != null)
                            {
                                dbSetsInterface.AppendLine($"        DbSet<{type}> {pl} {{ get; }}");
                            }
                            
                        }

                        foreach(var copy in copyFunctions){
                            builder.Append(
$@"        public static {type} {copy.Key}({type} obj)
        {{
            if(obj==null){{
                return null;
            }}
            return new {type}(){{
");                     foreach(var prop in copy.Value){
                                builder.Append($"                {prop}=obj.{prop},\n");
                            }
                            builder.Append("            };\n");
                            builder.Append("        }\n");
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

                            if(writeToFile){
                                var def = string.Format(
                                    tmpl,
                                    (isFlags ? "[Flags]" : "")+
                                    (firestore && !isEnum && !isInterface?"[Google.Cloud.Firestore.FirestoreData]":""),
                                    isEnum ? "enum" : (isInterface?"interface":"partial class"),
                                    type,
                                    builder.ToString(),
                                    ns,
                                    extendsString,
                                    usingSyntax)
                                    .Dump();
                                File.WriteAllText(filepath, def);
                            }
                        }



                        if(!ignoreJsonClass){
                            if (tsOut != null)
                            {
                                var def = string.Format(tsTmpl, "", isEnum ? "enum" : "interface", type, tsBuilder.ToString()).Dump();
                                tsFile.Append(def);
                            }

                            if(dbHookOut!=null && !isEnum)
                            {
                                var def = string.Format(dbHookTmpl,type,pl).Dump();
                                dbHooksFile.Append(def);
                                if(!hookImports.Contains(type)){
                                    hookImports.Add(type);
                                }
                            }
                        }


                        "".Dump();
                    }

                }
            }

            if (tsOut != null)
            {
                if(!string.IsNullOrWhiteSpace(tsHeader)){
                    tsFile.Insert(0,File.ReadAllText(tsHeader)+"\n");
                }
                $"tsOut = {tsOut}".Dump();
                File.WriteAllText(tsOut, tsFile.ToString());
            }

            if(dbHookOut!=null)
            {
                $"dbHookOut = {dbHookOut}".Dump();
                var fileName=Path.GetFileName(tsOut??"types.ts").Split('.')[0];
                dbHooksFile.Insert(0,$"import {{ {string.Join(", ",hookImports)} }} from './{fileName}';\n");
                var libHookImports=new List<string>();
                libHookImports.Add("useObj");
                if(hookRefSingle){
                    libHookImports.Add("useObjSingleRef");
                }
                if(hookRefCollection){
                    libHookImports.Add("useObjCollectionRef");
                }
                dbHooksFile.Insert(0,
                    "// this file is generated using MakeTypes.ps1. Do not manually modify.\n"+
                    $"import {{ {string.Join(", ",libHookImports)} }} from '../db/db-hooks';\n"+
                    "import { IdParam } from '../db/db-types';\n");
                File.WriteAllText(dbHookOut, dbHooksFile.ToString());
            }

            if (dbClass != null)
            {


                var configBody=new StringBuilder();
                foreach(var opt in configOptions){
                    switch(opt.Type){

                        case ConfigOptionType.DeleteBehavior:
                            configBody.Append(@$"
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e=>e.GetForeignKeys()))
            {{
                relationship.DeleteBehavior = DeleteBehavior.{opt.DeleteBehavior};
            }}
");
                            break;

                        case ConfigOptionType.OneToMany:
                            configBody.Append(@$"
            modelBuilder.Entity<{opt.OneManyTypeOne}>()
                .HasOne(e=>e.{opt.OneManyPropOne})
                .WithMany(e=>e.{opt.OneManyPropMany});
");
                            break;

                    }
                }

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
                    dbSets.ToString(),
                    configBody.ToString());
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
                    dbInfTmpl,
                    dbInterfaceNs,
                    "interface " + name,
                    dbSetsInterface.ToString());
                File.WriteAllText(dbInterface, value);
                value.Dump();
            }
        }

        static string ToPlural(string pl)
        {
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
            return pl;
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

const string dbInfTmpl =
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

const string dbSetTmpl =
@"using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace {0}
{{
    public partial {1}
    {{
{2}

        private void ConfigureModel(ModelBuilder modelBuilder)
        {{
{3}
        }}
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

const string dbHookTmpl =
@"

export function use{0}(id:IdParam):{0}|null|undefined
{{
    return useObj<{0}>('{1}',id);
}}
";

const string dbHookRefSingleTmpl =//0 BaseType, 1 RefProperty, 2 RefType, 3 BaseCollection, 4 RefCollection, 5 foreignKey
@"

export function use{0}Ref{1}(id:IdParam):{2}|null|undefined
{{
    return useObjSingleRef<{0},{2}>('{3}',id,'{4}',null,'{5}');
}}
";

const string dbHookRefCollectionTmpl =//0 BaseType, 1 RefProperty, 2 RefType, 3 BaseCollection, 4 RefCollection, 5 foreignKey
@"

export function use{0}Ref{1}(id:IdParam):{2}[]|null|undefined
{{
    return useObjCollectionRef<{0},{2}>('{3}',id,'{4}','{1}','{5}');
}}
";
    }
}