using System;
using System.Collections.Generic;
using System.Text;
using System.Data.OleDb;
using System.Data;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;

namespace proto_conv
{

    class FieldInfo
    {
        public string m_fieldName;
        public string m_columnName;
        public int m_columnIndex;
        public string m_typeName;
        public string m_desc;
        public string m_constraintString;
    }

    enum ConvType
    {
        Unknown,
        Client,
        Server,
        Both,
    }

    class conv
    {
        static string protoGenExe = AppDomain.CurrentDomain.BaseDirectory + "..\\..\\..\\..\\ProtoGen\\protogen.exe";
        static string protobufDll = AppDomain.CurrentDomain.BaseDirectory + "..\\..\\..\\..\\ProtoGen\\protobuf-net.dll";
        static string protoGenWorkDir = AppDomain.CurrentDomain.BaseDirectory + "temp";
        static string protoGenPath = AppDomain.CurrentDomain.BaseDirectory + "..\\..\\..\\..\\mw-proto-client\\";
        static ConvType convType = ConvType.Unknown;

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: proto_conv.exe <xlsx path> [type]\n\ttype: client, server or both");
                return;
            }
            string inputFilename = Path.GetFullPath(args[0]); //AppDomain.CurrentDomain.BaseDirectory + "test.xlsx";
            string inputDirName = Path.GetDirectoryName(inputFilename);
            string outputPath = inputDirName;
            switch (args[1].ToLower())
            {
                case "client":
                    convType = ConvType.Client;
                    break;
                case "server":
                    convType = ConvType.Server;
                    break;
                case "both":
                    convType = ConvType.Both;
                    break;
            }
            if (convType == ConvType.Unknown)
            {
                Console.WriteLine("Error: unknown type: " + args[1]);
                return;
            }
            

            string cs = string.Format("Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};Extended Properties='Excel 12.0 XML;HDR=NO;IMEX=1;'",
                inputFilename);
            OleDbConnection CNN = new OleDbConnection(cs);
            CNN.Open();
            

            
            System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(protoGenWorkDir);
            if (!di.Exists)
            {
                di.Create();
            }

            string sharedCode = "";
            {
                
                string protoIncFilename = di.FullName + "\\shared.proto";
                File.Copy(di.FullName + "\\..\\shared.proto", protoIncFilename, true);


                string outFilename = protoGenPath + "\\code\\shared.cs";
                string param = string.Format("-i:\"{0}\" -o:\"{1}\" -ns:mw", protoIncFilename, outFilename);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = protoGenExe;
                psi.Arguments = param;
                psi.WorkingDirectory = protoGenWorkDir;
                psi.UseShellExecute = false;
                Process proc = System.Diagnostics.Process.Start(psi);
                proc.WaitForExit();

                sharedCode = File.ReadAllText(outFilename);
            }
            

            DataTable ttable = CNN.GetSchema("Tables");
            foreach (DataRow row in ttable.Rows)
            {
                if (row["Table_Type"].ToString() == "TABLE")
                {
                    string tabName = row["Table_Name"].ToString();
                    if(tabName.ToLower().Contains("description"))
                        continue;
                    
                    OleDbDataAdapter adpt = new OleDbDataAdapter("select * from [" + tabName + "]", CNN);
                    DataTable dt = new DataTable();
                    adpt.Fill(dt);

                    if(dt.Rows.Count < 1)
                        continue;
                    tabName = tabName.Replace("$", "");
                    Console.WriteLine("gen proto..." + tabName);
                    Dictionary<string, FieldInfo> fieldInfo = new Dictionary<string, FieldInfo>();
                    string s = GenProto(dt, tabName, fieldInfo);

                    
                    string protoFilename = di.FullName + "\\" + tabName + ".proto";
                    StreamWriter sw = File.CreateText(protoFilename);
                    sw.Write(s);
                    sw.Close();

                    string outFilename = protoGenPath + "\\code\\" + tabName + ".cs";
                    string param = string.Format("-i:\"{0}\" -o:\"{1}\" -ns:mw", protoFilename, outFilename);
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = protoGenExe;
                    psi.Arguments = param;
                    psi.WorkingDirectory = protoGenWorkDir;
                    psi.UseShellExecute = false;
                    Process proc = System.Diagnostics.Process.Start(psi);
                    proc.WaitForExit();


                    string validateCode = GenValidateInfo(tabName, fieldInfo);
                    string outValidateFilename = protoGenPath + "\\code\\" + tabName + "_validate.cs";
                    File.WriteAllText(outValidateFilename, validateCode);




                    string code = File.ReadAllText(outFilename);

                    Assembly lib = CompileCS(code, sharedCode);
                    string typename = "mw." + tabName;
                    Type t = lib.GetType(typename);
                    PropertyInfo[] properties = t.GetProperties();
               

                    FileStream fs = new FileStream(protoGenPath + "\\protodata\\" + tabName + ".protodata.bytes", FileMode.Create);
                    BinaryWriter bw = new BinaryWriter(fs);
                    bw.Write(t.FullName);
                    bw.Write((uint)dt.Rows.Count - 1);

                    System.Reflection.ConstructorInfo ci = t.GetConstructor(Type.EmptyTypes);
                    int rowCount = 0;
                    for (int i = 1; i < dt.Rows.Count; i++ )
                    {
                        DataRow dr = dt.Rows[i];
                        ProtoBuf.IExtensible o = ci.Invoke(new object[0]) as ProtoBuf.IExtensible;
                        foreach (PropertyInfo pi in properties)
                        {
                            if (pi.Name.EndsWith("_raw"))
                            {
                                FieldInfo field = fieldInfo[pi.Name.Substring(0, pi.Name.Length - 4)];
                                pi.SetValue(o, dr[field.m_columnIndex].ToString(), BindingFlags.SetProperty, null, null, null);
                                continue;
                            }
                            FieldInfo fi = fieldInfo[pi.Name];
                            string valueString = dr[fi.m_columnIndex].ToString();
                            try
                            {
                                object v = GetValue(fi, valueString, lib);
                                if (fi.m_typeName == "TFloat")
                                {
                                    Type tfloat = lib.GetType("mw.TFloat");
                                    System.Reflection.ConstructorInfo tfloat_ci = tfloat.GetConstructor(Type.EmptyTypes);
                                    ProtoBuf.IExtensible o_tfloat = tfloat_ci.Invoke(new object[0]) as ProtoBuf.IExtensible;
                                    PropertyInfo pi_v = tfloat.GetProperty("v");
                                    pi_v.SetValue(o_tfloat, v, BindingFlags.SetProperty, null, null, null);
                                    v = o_tfloat;
                                }
                                else if (fi.m_typeName == "TTFloat")
                                {
                                    Type ttfloat = lib.GetType("mw.TTFloat");
                                    System.Reflection.ConstructorInfo ttfloat_ci = ttfloat.GetConstructor(Type.EmptyTypes);
                                    ProtoBuf.IExtensible o_ttfloat = ttfloat_ci.Invoke(new object[0]) as ProtoBuf.IExtensible;
                                    PropertyInfo pi_v = ttfloat.GetProperty("v");
                                    pi_v.SetValue(o_ttfloat, v, BindingFlags.SetProperty, null, null, null);
                                    v = o_ttfloat;
                                }
                                                                
                                pi.SetValue(o, v, BindingFlags.SetProperty, null, null, null);
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(string.Format("{0}, 行: {1}, 列: {2}", ex.Message, rowCount + 2, fi.m_desc));
                                Console.ResetColor();
                            }
                        }
                        rowCount++;
                        ProtoBuf.Serializer.NonGeneric.SerializeWithLengthPrefix(fs, o, ProtoBuf.PrefixStyle.Fixed32, 0);
                    }
                    
                    fs.Close();
                }
            }
            
            CNN.Close();
        }

        static object GetValue(FieldInfo fi, string v, Assembly typelib)
        {
            object ret = null;
            try
            {
                switch (fi.m_typeName)
                {
                    case "float":
                        ret = float.Parse(v);
                        break;
                    case "TFloat":
                        ret = (int)(Math.Round(float.Parse(v) * 1000));
                        break;
                    case "TTFloat":
                        ret = (int)(Math.Round(float.Parse(v) * 1000000));
                        break;
                    case "int32":
                        ret = int.Parse(v);
                        break;
                    case "bool":
                        ret = (v.ToLower() == "true" || v == "1");
                        break;
                    case "string":
                        ret = v;
                        break;
                    default:
                        {
                            string typename = "mw." + fi.m_typeName.Replace(".", "+");
                            Type[] tt = typelib.GetTypes();
                            Type t = typelib.GetType(typename);
                            ret = Enum.Parse(t, v);
                        }
                        break;
                }
            }
            catch (Exception)
            {
                //throw new Exception(string.Format("无效的值: '{0}'", v));
            }

            //if (fi.m_constraint != null)
            //{
            //    if (fi.m_constraint.GetValue(ret, out ret))
            //    {
            //        throw new Exception(string.Format("警告: 值({0})不在约束范围({1})内!", v, fi.m_constraint.ToString()));
            //    }
            //}
            return ret;
        }

        static Assembly CompileCS(params string[] code)
        {
            CodeDomProvider domProvider = CodeDomProvider.CreateProvider("CSharp");
            CompilerParameters cp = new CompilerParameters();
            cp.GenerateExecutable = false;
            cp.GenerateInMemory = true;
            cp.ReferencedAssemblies.Add(protobufDll);
            cp.ReferencedAssemblies.Add(@"C:\Windows\Microsoft.NET\Framework\v2.0.50727\System.dll");

            Console.WriteLine("Compile...");
            CompilerResults cr = domProvider.CompileAssemblyFromSource(cp, code);
            
            if (cr.Errors.Count > 0)
            {
                Console.WriteLine("compile error!");
                foreach (CompilerError ce in cr.Errors)
                {
                    Console.WriteLine("  {0}", ce.ToString());
                    Console.WriteLine();
                }
            }
            System.Reflection.Assembly assembly = cr.CompiledAssembly;
            return assembly;
        }

        static FieldInfo GetFieldInfo(string columnName)
        {
            // 分隔符： | : 客户端字段
            //        || : 服务器字段
            //       ||| : 共用的字段
            string str = columnName.Replace("\r\n", "").Replace("\n", "");
            
            
            string dont_export = "@dont_export@";
            if (convType == ConvType.Both)
            {
                str = str.Replace("|||", "|");
                str = str.Replace("||", "|");
            }
            else if (convType == ConvType.Server)
            {
                str = str.Replace("|||", "@~~~@");
                str = str.Replace("||", "@~~~@");
                str = str.Replace("|", dont_export);
                str = str.Replace("@~~~@", "|");
            }
            else if (convType == ConvType.Client)
            {
                str = str.Replace("|||", "|");
                str = str.Replace("||", dont_export);
            }
            if (str.Contains(dont_export))
                return null;
            Regex regx = new Regex(@"\A(?<desc>.+?)\|(?<name>.+?)\((?<type>.+?)(:(?<constraint>.*?))?\)$");
            Match mat = regx.Match(str);
            if (mat.Success)
            {
                string name = mat.Groups["name"].Value;
                string type = mat.Groups["type"].Value;
                string desc = mat.Groups["desc"].Value;
                string constraint = mat.Groups["constraint"].Value;

                if (convType == ConvType.Client && type.ToLower() == "float")
                {// 客户端float转换为TFloat
                    type = "TFloat";
                }
                else if (convType == ConvType.Client && type.ToLower() == "ttfloat")
                {
                    type = "TTFloat";
                }
                else if (convType != ConvType.Client && type.ToLower() == "ttfloat")
                {
                    type = "float";
                }
                
                
                FieldInfo fi = new FieldInfo();
                fi.m_columnName = columnName;
                fi.m_fieldName = name;
                fi.m_typeName = type;
                fi.m_desc = desc;
                fi.m_constraintString = constraint;
                return fi;
            }
            else
            {
                throw new Exception("标题格式错误: " + columnName);
            }

        }

        static string GenEnumDef(string enumDef)
        {
            string[] enumDefStrings = enumDef.Split(',');
            StringBuilder sb = new StringBuilder();
            string[] keyvalue = enumDefStrings[0].Split('=');
            if(keyvalue[0] != "type")
            {
                throw new Exception("错误：第一个枚举定义必须是type.");
            }
            sb.AppendFormat("enum {0} {{\n", keyvalue[1]);

            for (int i = 1; i < enumDefStrings.Length; i++)
            {
                sb.Append("\t");
                sb.AppendLine(enumDefStrings[i] + ";");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        static string GenProto(DataTable t, string tablename, Dictionary<string, FieldInfo> fieldInfo)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("import \"shared.proto\";\n\n");
            sb.AppendFormat("message {0}\n{{\n", tablename);

            DataRow header = t.Rows[0];

            fieldInfo.Clear();
            int c = 1;
            int idx = 0;
            foreach (object o in header.ItemArray)
            {
                idx++;
                if (!o.ToString().Contains("|"))
                    continue;
                FieldInfo fi = GetFieldInfo(o.ToString());
                if(fi == null)
                    continue;
                fi.m_columnIndex = idx - 1;
                string typename = fi.m_typeName;

                

                fieldInfo.Add(fi.m_fieldName, fi);
                sb.AppendFormat("\trequired {0} {1} = {2};\n", typename, fi.m_fieldName, c++);
                if (convType == ConvType.Both)
                    sb.AppendFormat("\trequired string {0} = {1};\n", fi.m_fieldName + "_raw", c++);
            }

            sb.Append("}");


            return sb.ToString();
        }


        static string GenValidateInfo(string typename, Dictionary<string, FieldInfo> fieldInfo)
        {
            StringBuilder sb = new StringBuilder();

            typename = typename + "_ValidateInfo";

            sb.AppendFormat("using System; using System.Collections.Generic;\n\n namespace mw {{\n public class {0}\n ", typename);
            sb.Append("{\n");
            sb.Append("public static Dictionary<string, string> m_validateDefine = new Dictionary<string, string>();\n");
            sb.Append("static ");
            sb.Append(typename);
            sb.Append("()\n");
            sb.Append("{\n");

            var v = fieldInfo.GetEnumerator();
            while (v.MoveNext())
            {
                string validateString = v.Current.Value.m_constraintString.Replace("\\", "\\\\");
                validateString = validateString.Replace("\"", "\\\"");
                if(validateString == "")
                    continue;
                sb.AppendFormat("m_validateDefine.Add(\"{0}\", \"{1}\");\n", v.Current.Value.m_fieldName,
                    validateString);
            }

            sb.Append("}\n");

            sb.Append("}\n");
            sb.Append("}");

            return sb.ToString();
        }
    }
}
