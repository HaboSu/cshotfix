﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSHotFix.Runtime.Enviorment;
using CSHotFix.Other;

namespace CSHotFix.Runtime.CLRBinding
{
    public class BindingCodeGenerator
    {
        public static void GenerateBindingCode(List<Type> types, string outputPath,bool deleteOld = true, HashSet<MethodBase> excludeMethods = null, HashSet<FieldInfo> excludeFields = null)
        {
            if (!System.IO.Directory.Exists(outputPath))
                System.IO.Directory.CreateDirectory(outputPath);
            if (deleteOld)
            {
                string[] oldFiles = System.IO.Directory.GetFiles(outputPath, "*.cs");
                foreach (var i in oldFiles)
                {
                    System.IO.File.Delete(i);
                }
            }
            List<string> clsNames = new List<string>();
            foreach (var i in types)
            {
                string clsName, realClsName;
                bool isByRef;
                if (i.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length > 0)
                    continue;
                i.GetClassName(out clsName, out realClsName, out isByRef);
                clsNames.Add(clsName);
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(outputPath + "/" + clsName + ".cs", false, new UTF8Encoding(false)))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(@"using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

using CSHotFix.CLR.TypeSystem;
using CSHotFix.CLR.Method;
using CSHotFix.Runtime.Enviorment;
using CSHotFix.Runtime.Intepreter;
using CSHotFix.Runtime.Stack;
using CSHotFix.Reflection;
using CSHotFix.CLR.Utils;

namespace CSHotFix.Runtime.Generated
{
    unsafe class ");
                    sb.AppendLine(clsName);
                    sb.Append(@"    {
        public static void Register(CSHotFix.Runtime.Enviorment.AppDomain app)
        {
");
                    string flagDef = "            BindingFlags flag = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;";
                    string methodDef = "            MethodBase method;";
                    string fieldDef = "            FieldInfo field;";
                    string argsDef = "            Type[] args;";
                    string typeDef = string.Format("            Type type = typeof({0});", realClsName);

                    MethodInfo[] methods = i.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    //过滤
                    methods = methods.ToList().FindAll((methodInfo) => 
                    {
                        bool hr = methodInfo.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length > 0 ||
                                    MethodBindingGenerator.IsMethodPtrType(methodInfo) ||
                                    GenConfig.SpecialBlackTypeList.Exists((_str)=> 
                                    {
                                        List<string> strpair = _str;
                                        string _class = strpair[0];
                                        string _name = strpair[1];
                                        if(i.FullName.Contains(_class))
                                        {
                                            return methodInfo.Name.Contains(_name);
                                        }
                                        else
                                        {
                                            return false;
                                        }
                                        
                                    });
                        return !hr;
                    }).ToArray();
                    FieldInfo[] fields = i.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    fields = fields.ToList().FindAll((field) => 
                    {
                        bool hr = field.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length > 0 ||
                                  GenConfig.SpecialBlackTypeList.Exists((_str) =>
                                  {
                                      List<string> strpair = _str;
                                      string _class = strpair[0];
                                      string _name = strpair[1];
                                      if (i.FullName.Contains(_class))
                                      {
                                          return field.Name.Contains(_name);
                                      }
                                      else
                                      {
                                          return false;
                                      }

                                  });

                        return !hr;
                    }).ToArray();
                    string registerMethodCode = i.GenerateMethodRegisterCode(methods, excludeMethods);
                    string registerFieldCode = i.GenerateFieldRegisterCode(fields, excludeFields);
                    string registerValueTypeCode = i.GenerateValueTypeRegisterCode(realClsName);
                    string registerMiscCode = i.GenerateMiscRegisterCode(realClsName, true, true);
                    string commonCode = i.GenerateCommonCode(realClsName);
                    ConstructorInfo[] ctors = i.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    string ctorRegisterCode = i.GenerateConstructorRegisterCode(ctors, excludeMethods);
                    string methodWraperCode = i.GenerateMethodWraperCode(methods, realClsName, excludeMethods);
                    string fieldWraperCode = i.GenerateFieldWraperCode(fields, realClsName, excludeFields);
                    string cloneWraperCode = i.GenerateCloneWraperCode(fields, realClsName);
                    string ctorWraperCode = i.GenerateConstructorWraperCode(ctors, realClsName, excludeMethods);

                    bool hasMethodCode = !string.IsNullOrEmpty(registerMethodCode);
                    bool hasFieldCode = !string.IsNullOrEmpty(registerFieldCode);
                    bool hasValueTypeCode = !string.IsNullOrEmpty(registerValueTypeCode);
                    bool hasMiscCode = !string.IsNullOrEmpty(registerMiscCode);
                    bool hasCtorCode = !string.IsNullOrEmpty(ctorRegisterCode);
                    bool hasNormalMethod = methods.Where(x => !x.IsGenericMethod).Count() != 0;

                    if ((hasMethodCode && hasNormalMethod) || hasFieldCode || hasCtorCode)
                        sb.AppendLine(flagDef);
                    if (hasMethodCode || hasCtorCode)
                        sb.AppendLine(methodDef);
                    if (hasFieldCode)
                        sb.AppendLine(fieldDef);
                    if (hasMethodCode || hasFieldCode || hasCtorCode)
                        sb.AppendLine(argsDef);
                    if (hasMethodCode || hasFieldCode || hasValueTypeCode || hasMiscCode || hasCtorCode)
                        sb.AppendLine(typeDef);


                    sb.AppendLine(registerMethodCode);
                    sb.AppendLine(registerFieldCode);
                    sb.AppendLine(registerValueTypeCode);
                    sb.AppendLine(registerMiscCode);
                    sb.AppendLine(ctorRegisterCode);
                    sb.AppendLine("        }");
                    sb.AppendLine();
                    sb.AppendLine(commonCode);
                    sb.AppendLine(methodWraperCode);
                    sb.AppendLine(fieldWraperCode);
                    sb.AppendLine(cloneWraperCode);
                    sb.AppendLine(ctorWraperCode);
                    sb.AppendLine("    }");
                    sb.AppendLine("}");

                    sw.Write(Regex.Replace(sb.ToString(), "(?<!\r)\n", "\r\n"));
                    sw.Flush();
                }
            }

            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(outputPath + "/CLRBindings.cs", false, new UTF8Encoding(false)))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"using System;
using System.Collections.Generic;
using System.Reflection;

namespace CSHotFix.Runtime.Generated
{
    class CLRBindings
    {
        /// <summary>
        /// Initialize the CLR binding, please invoke this AFTER CLR Redirection registration
        /// </summary>
        public static void Initialize(CSHotFix.Runtime.Enviorment.AppDomain app)
        {");
                foreach (var i in clsNames)
                {
                    sb.Append("            ");
                    sb.Append(i);
                    sb.AppendLine(".Register(app);");
                }

                sb.AppendLine(@"        }
    }
}");
                sw.Write(Regex.Replace(sb.ToString(), "(?<!\r)\n", "\r\n"));
            }
        }

        class CLRBindingGenerateInfo
        {
            public Type Type { get; set; }
            public HashSet<MethodInfo> Methods { get; set; }
            public HashSet<FieldInfo> Fields { get; set; }
            public HashSet<ConstructorInfo> Constructors { get; set; }
            public bool ArrayNeeded { get; set; }
            public bool DefaultInstanceNeeded { get; set; }
            public bool ValueTypeNeeded { get; set; }

            public bool NeedGenerate
            {
                get
                {
                    if (Methods.Count == 0 && Constructors.Count == 0 && Fields.Count == 0 && !ArrayNeeded && !DefaultInstanceNeeded && !ValueTypeNeeded)
                        return false;
                    else
                    {
                        //Making CLRBinding for such types makes no sense
                        if (Type == typeof(Delegate) || Type == typeof(System.Runtime.CompilerServices.RuntimeHelpers))
                            return false;
                        return true;
                    }
                }
            }
        }

        public static void GenerateBindingCode(CSHotFix.Runtime.Enviorment.AppDomain domain, string outputPath, bool deleteOld = true)
        {
            if (domain == null)
                return;
            if (!System.IO.Directory.Exists(outputPath))
                System.IO.Directory.CreateDirectory(outputPath);
            Dictionary<Type, CLRBindingGenerateInfo> infos = new Dictionary<Type, CLRBindingGenerateInfo>(new ByReferenceKeyComparer<Type>());
            CrawlAppdomain(domain, infos);
            if (deleteOld)
            {
                string[] oldFiles = System.IO.Directory.GetFiles(outputPath, "*.cs");
                foreach (var i in oldFiles)
                {
                    System.IO.File.Delete(i);
                }
            }

            HashSet<MethodBase> excludeMethods = null;
            HashSet<FieldInfo> excludeFields = null;
            HashSet<string> files = new HashSet<string>();
            List<string> clsNames = new List<string>();
            foreach (var info in infos)
            {
                if (!info.Value.NeedGenerate)
                    continue;
                Type i = info.Value.Type;
                if (i.BaseType == typeof(MulticastDelegate))
                    continue;
                string clsName, realClsName;
                bool isByRef;
                if (i.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length > 0)
                    continue;
                i.GetClassName(out clsName, out realClsName, out isByRef);
                if (clsNames.Contains(clsName))
                    clsName = clsName + "_t";
                clsNames.Add(clsName);
                string oFileName = outputPath + "/" + clsName;
                int len = Math.Min(oFileName.Length, 100);
                if (len < oFileName.Length)
                    oFileName = oFileName.Substring(0, len) + "_t";
                while (files.Contains(oFileName))
                    oFileName = oFileName + "_t";
                files.Add(oFileName);
                oFileName = oFileName + ".cs";
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(oFileName, false, new UTF8Encoding(false)))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(@"using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

using CSHotFix.CLR.TypeSystem;
using CSHotFix.CLR.Method;
using CSHotFix.Runtime.Enviorment;
using CSHotFix.Runtime.Intepreter;
using CSHotFix.Runtime.Stack;
using CSHotFix.Reflection;
using CSHotFix.CLR.Utils;

namespace CSHotFix.Runtime.Generated
{
    unsafe class ");
                    sb.AppendLine(clsName);
                    sb.Append(@"    {
        public static void Register(CSHotFix.Runtime.Enviorment.AppDomain app)
        {
");
                    string flagDef =    "            BindingFlags flag = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;";
                    string methodDef =  "            MethodBase method;";
                    string fieldDef =   "            FieldInfo field;";
                    string argsDef =    "            Type[] args;";
                    string typeDef = string.Format("            Type type = typeof({0});", realClsName);

                    MethodInfo[] methods = info.Value.Methods.ToArray();
                    FieldInfo[] fields = info.Value.Fields.ToArray();
                    string registerMethodCode = i.GenerateMethodRegisterCode(methods, excludeMethods);
                    string registerFieldCode = fields.Length > 0 ? i.GenerateFieldRegisterCode(fields, excludeFields) : null;
                    string registerValueTypeCode = info.Value.ValueTypeNeeded ? i.GenerateValueTypeRegisterCode(realClsName) : null;
                    string registerMiscCode = i.GenerateMiscRegisterCode(realClsName, info.Value.DefaultInstanceNeeded, info.Value.ArrayNeeded);
                    string commonCode = i.GenerateCommonCode(realClsName);
                    ConstructorInfo[] ctors = info.Value.Constructors.ToArray();
                    string ctorRegisterCode = i.GenerateConstructorRegisterCode(ctors, excludeMethods);
                    string methodWraperCode = i.GenerateMethodWraperCode(methods, realClsName, excludeMethods);
                    string fieldWraperCode = fields.Length > 0 ? i.GenerateFieldWraperCode(fields, realClsName, excludeFields) : null;
                    string cloneWraperCode = null;
                    if (info.Value.ValueTypeNeeded)
                    {
                        //Memberwise clone should copy all fields
                        var fs = i.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                        cloneWraperCode = i.GenerateCloneWraperCode(fs, realClsName);
                    }

                    bool hasMethodCode = !string.IsNullOrEmpty(registerMethodCode);
                    bool hasFieldCode = !string.IsNullOrEmpty(registerFieldCode);
                    bool hasValueTypeCode = !string.IsNullOrEmpty(registerValueTypeCode);
                    bool hasMiscCode = !string.IsNullOrEmpty(registerMiscCode);
                    bool hasCtorCode = !string.IsNullOrEmpty(ctorRegisterCode);
                    bool hasNormalMethod = methods.Where(x => !x.IsGenericMethod).Count() != 0;

                    if ((hasMethodCode && hasNormalMethod) || hasFieldCode || hasCtorCode)
                        sb.AppendLine(flagDef);
                    if (hasMethodCode || hasCtorCode)
                        sb.AppendLine(methodDef);
                    if (hasFieldCode)
                        sb.AppendLine(fieldDef);
                    if (hasMethodCode || hasFieldCode || hasCtorCode)
                        sb.AppendLine(argsDef);
                    if (hasMethodCode || hasFieldCode || hasValueTypeCode || hasMiscCode || hasCtorCode)
                        sb.AppendLine(typeDef);

                    sb.AppendLine(registerMethodCode);
                    if (fields.Length > 0)
                        sb.AppendLine(registerFieldCode);
                    if (info.Value.ValueTypeNeeded)
                        sb.AppendLine(registerValueTypeCode);
                    if (!string.IsNullOrEmpty(registerMiscCode))
                        sb.AppendLine(registerMiscCode);
                    sb.AppendLine(ctorRegisterCode);
                    sb.AppendLine("        }");
                    sb.AppendLine();
                    sb.AppendLine(commonCode);
                    sb.AppendLine(methodWraperCode);
                    if (fields.Length > 0)
                        sb.AppendLine(fieldWraperCode);
                    if (info.Value.ValueTypeNeeded)
                        sb.AppendLine(cloneWraperCode);
                    string ctorWraperCode = i.GenerateConstructorWraperCode(ctors, realClsName, excludeMethods);
                    sb.AppendLine(ctorWraperCode);
                    sb.AppendLine("    }");
                    sb.AppendLine("}");

                    sw.Write(Regex.Replace(sb.ToString(), "(?<!\r)\n", "\r\n"));
                    sw.Flush();
                }
            }

            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(outputPath + "/CLRBindings.cs", false, new UTF8Encoding(false)))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"using System;
using System.Collections.Generic;
using System.Reflection;

namespace CSHotFix.Runtime.Generated
{
    class CLRBindings
    {
        /// <summary>
        /// Initialize the CLR binding, please invoke this AFTER CLR Redirection registration
        /// </summary>
        public static void Initialize(CSHotFix.Runtime.Enviorment.AppDomain app)
        {");
                foreach (var i in clsNames)
                {
                    sb.Append("            ");
                    sb.Append(i);
                    sb.AppendLine(".Register(app);");
                }

                sb.AppendLine(@"        }
    }
}");
                sw.Write(Regex.Replace(sb.ToString(), "(?<!\r)\n", "\r\n"));
            }
        }

        static void CrawlAppdomain(CSHotFix.Runtime.Enviorment.AppDomain domain, Dictionary<Type, CLRBindingGenerateInfo> infos)
        {
            var arr = domain.LoadedTypes.Values.ToArray();
            //Prewarm
            foreach (var type in arr)
            {
                if (type is CLR.TypeSystem.ILType)
                {
                    if (type.HasGenericParameter)
                        continue;
                    var methods = type.GetMethods().ToList();
                    foreach (var i in ((CLR.TypeSystem.ILType)type).GetConstructors())
                        methods.Add(i);
                    if (((CLR.TypeSystem.ILType)type).GetStaticConstroctor() != null)
                        methods.Add(((CLR.TypeSystem.ILType)type).GetStaticConstroctor());
                    foreach (var j in methods)
                    {
                        CLR.Method.ILMethod method = j as CLR.Method.ILMethod;
                        if (method != null)
                        {
                            if (method.GenericParameterCount > 0 && !method.IsGenericInstance)
                                continue;
                            var body = method.Body;
                        }
                    }
                }
            }
            arr = domain.LoadedTypes.Values.ToArray();
            foreach (var type in arr)
            {
                if (type is CLR.TypeSystem.ILType)
                {
                    if (type.TypeForCLR.IsByRef || type.HasGenericParameter)
                        continue;
                    var methods = type.GetMethods().ToList();
                    foreach (var i in ((CLR.TypeSystem.ILType)type).GetConstructors())
                        methods.Add(i);

                    foreach (var j in methods)
                    {
                        CLR.Method.ILMethod method = j as CLR.Method.ILMethod;
                        if (method != null)
                        {
                            if (method.GenericParameterCount > 0 && !method.IsGenericInstance)
                                continue;
                            var body = method.Body;
                            foreach (var ins in body)
                            {
                                switch (ins.Code)
                                {
                                    case Intepreter.OpCodes.OpCodeEnum.Newobj:
                                        {
                                            CLR.Method.CLRMethod m = domain.GetMethod(ins.TokenInteger) as CLR.Method.CLRMethod;
                                            if (m != null)
                                            {
                                                if (m.DeclearingType.IsDelegate)
                                                    continue;
                                                Type t = m.DeclearingType.TypeForCLR;
                                                CLRBindingGenerateInfo info;
                                                if (!infos.TryGetValue(t, out info))
                                                {
                                                    info = CreateNewBindingInfo(t);
                                                    infos[t] = info;
                                                }
                                                if (m.IsConstructor)
                                                    info.Constructors.Add(m.ConstructorInfo);
                                                else
                                                    info.Methods.Add(m.MethodInfo);
                                            }
                                        }
                                        break;
                                    case Intepreter.OpCodes.OpCodeEnum.Ldfld:
                                    case Intepreter.OpCodes.OpCodeEnum.Stfld:
                                    case Intepreter.OpCodes.OpCodeEnum.Ldflda:
                                    case Intepreter.OpCodes.OpCodeEnum.Ldsfld:
                                    case Intepreter.OpCodes.OpCodeEnum.Ldsflda:
                                    case Intepreter.OpCodes.OpCodeEnum.Stsfld:
                                        {
                                            var t = domain.GetType((int)(ins.TokenLong >> 32)) as CLR.TypeSystem.CLRType;
                                            if(t != null)
                                            {
                                                var fi = t.GetField((int)ins.TokenLong);
                                                if (fi != null && fi.IsPublic)
                                                {
                                                    CLRBindingGenerateInfo info;
                                                    if (!infos.TryGetValue(t.TypeForCLR, out info))
                                                    {
                                                        info = CreateNewBindingInfo(t.TypeForCLR);
                                                        infos[t.TypeForCLR] = info;
                                                    }
                                                    if(ins.Code == Intepreter.OpCodes.OpCodeEnum.Stfld || ins.Code == Intepreter.OpCodes.OpCodeEnum.Stsfld)
                                                    {
                                                        if (t.IsValueType)
                                                        {
                                                            info.ValueTypeNeeded = true;
                                                            info.DefaultInstanceNeeded = true;
                                                        }
                                                    }
                                                    if (t.TypeForCLR.CheckCanPinn() || !t.IsValueType)
                                                        info.Fields.Add(fi);
                                                }
                                            }
                                        }
                                        break;
                                    case Intepreter.OpCodes.OpCodeEnum.Ldtoken:
                                        {
                                            if (ins.TokenInteger == 0)
                                            {
                                                var t = domain.GetType((int)(ins.TokenLong >> 32)) as CLR.TypeSystem.CLRType;
                                                if (t != null)
                                                {
                                                    var fi = t.GetField((int)ins.TokenLong);
                                                    if (fi != null)
                                                    {
                                                        CLRBindingGenerateInfo info;
                                                        if (!infos.TryGetValue(t.TypeForCLR, out info))
                                                        {
                                                            info = CreateNewBindingInfo(t.TypeForCLR);
                                                            infos[t.TypeForCLR] = info;
                                                        }
                                                        info.Fields.Add(fi);
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    case Intepreter.OpCodes.OpCodeEnum.Newarr:
                                        {
                                            var t = domain.GetType(ins.TokenInteger) as CLR.TypeSystem.CLRType;
                                            if(t != null)
                                            {
                                                CLRBindingGenerateInfo info;
                                                if (!infos.TryGetValue(t.TypeForCLR, out info))
                                                {
                                                    info = CreateNewBindingInfo(t.TypeForCLR);
                                                    infos[t.TypeForCLR] = info;
                                                }
                                                info.ArrayNeeded = true;
                                            }
                                        }
                                        break;
                                    case Intepreter.OpCodes.OpCodeEnum.Call:
                                    case Intepreter.OpCodes.OpCodeEnum.Callvirt:
                                        {
                                            CLR.Method.CLRMethod m = domain.GetMethod(ins.TokenInteger) as CLR.Method.CLRMethod;
                                            if (m != null)
                                            {
                                                //Cannot explicit call base class's constructor directly
                                                if (m.IsConstructor)
                                                    continue;
                                                if (!m.MethodInfo.IsPublic)
                                                    continue;
                                                Type t = m.DeclearingType.TypeForCLR;
                                                CLRBindingGenerateInfo info;
                                                if (!infos.TryGetValue(t, out info))
                                                {
                                                    info = CreateNewBindingInfo(t);
                                                    infos[t] = info;
                                                }

                                                info.Methods.Add(m.MethodInfo);
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        static CLRBindingGenerateInfo CreateNewBindingInfo(Type t)
        {
            CLRBindingGenerateInfo info = new CLRBindingGenerateInfo();
            info.Type = t;
            info.Methods = new HashSet<MethodInfo>();
            info.Fields = new HashSet<FieldInfo>();
            info.Constructors = new HashSet<ConstructorInfo>();
            if (t.IsValueType)
                info.DefaultInstanceNeeded = true;
            return info;
        }
    }
}
