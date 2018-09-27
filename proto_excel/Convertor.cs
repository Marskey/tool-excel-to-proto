using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ProtoBuf;
using ProtoBuf.Meta;
using System.Security.Cryptography;

namespace proto_excel
{
	class FieldInfoX
	{
		public int index;
		public string name;
		public string type;
		public string desc;
		public string constraint;
	}

	class ExcelData
	{
		public string excelName;
		public string sheetName;
		public DataTable table;
		public Dictionary<string, FieldInfoX>/*[]*/ fieldInfos;
	}

	enum ConvType
	{
		Unknown = -1,
		Client,
		Server,
		Validate,
		Max
	}

	class Convertor
	{
		static Regex regex = new Regex(@"\A(?<desc>.+?)(?<flag>\|{1,3})(?<name>.+?)\((?<type>.+?)(:(?<constraint>.*?))?\)$");
		static string TempDir = Globals.Instance.TempDir;
		static string csc20 = Globals.Instance.csc20;
		static string protoc = Globals.Instance.protoc;
		static string protogen = Globals.Instance.protogen;
		static string protoFull = Globals.Instance.proto_full;
		static string protoCore = Globals.Instance.proto_core;
		static string exlDllName = Globals.Instance.exl_dll_name;
		static string exlPreName = Globals.Instance.exl_pre_name;
		static int exlPreSize = int.Parse(Globals.Instance.exl_pre_size);
		static string exlClientLibs = Globals.Instance.exl_client_libs;
		static string exlClientData = Globals.Instance.exl_client_data;
		static string exlServerLibs = Globals.Instance.exl_server_libs;
		static string exlServerData = Globals.Instance.exl_server_data;
		static string exlRelyProto = Globals.Instance.exl_rely_proto;
		static string exlRelyCs = Globals.Instance.exl_rely_cs;
		static string ns = Globals.Instance.NameSpace;
		static string ns_ = ns + ".";

		ConvType convType = ConvType.Unknown;
		int convType_;
		string protoPath;
		string codePath;
		string dllPath;
		string dataPath;

		int procNum;
		List<ExcelData> excels;
		Dictionary<string, string> protos;
		List<string> codes ;
		Assembly fullLib;
		Assembly coreLib;

		public Convertor(string type, int procNum)
		{
			type = type.ToLower();
			switch (type)
			{
				case "client": convType = ConvType.Client; break;
				case "server": convType = ConvType.Server; break;
				case "validate": convType = ConvType.Validate; break;
			}
			convType_ = (int)convType;
			this.procNum = procNum;

			protoPath = TempDir + type + "\\proto\\";
			codePath = TempDir + type + "\\code\\";
			dllPath = TempDir + type + "\\dll\\";
			dataPath = TempDir + type + "\\data\\";
			
			EnsureDirectory(TempDir);
			EnsureDirectory(protoPath);
			EnsureDirectory(codePath);
			EnsureDirectory(dllPath);  ClearDirectory(dllPath);
			EnsureDirectory(dataPath); //ClearDirectory(dataPath);
		}

		public void SetData(List<ExcelData> _excels)
		{
			excels = _excels;
		}
		
		// main process
		public void ThreadRun()
		{
			try
			{
				GenField();
				GetImports();
				GenProtos();
				if (convType == ConvType.Validate)
					GenValids();
				GetProtos();
				GenCSharps();
				if (convType == ConvType.Server)
					GenPythons();
				GetCodes();
				CompileFull();
				if (convType == ConvType.Client)
				{
					CompileCore();
					CompilePre(exlPreName, exlPreSize);
				}
				GenDatas();
				DoEnd();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				Console.ReadKey();
			}
		}

		void GenField()
		{
			foreach (ExcelData xd in excels)
			{
				xd.fieldInfos/*[convType_]*/ = GenFieldInfos(xd.table.Rows[0]);
			}
		}

		void GetImports()
		{
			protos = new Dictionary<string, string>();
			DirectoryInfo di = new DirectoryInfo(exlRelyProto);
			if (di.Attributes == FileAttributes.Directory)
			{
				foreach (FileInfo fi in di.GetFiles("*.proto"))
				{
					if (null == fi) continue;
					File.Copy(fi.FullName, protoPath + fi.Name, true);
					protos.Add(Path.GetFileNameWithoutExtension(fi.Name), "depend");
				}
			}
			else
			{
				File.Copy(di.FullName, protoPath + di.Name, true);
				protos.Add(Path.GetFileNameWithoutExtension(di.Name), "depend");
			}
		}

		void GenProtos()
		{
			Console.WriteLine("\n----------Gen Proto----------\n");
			foreach (ExcelData xd in excels)
			{
				string sheetName = xd.sheetName;
				Console.WriteLine("Gen Proto from " + xd.excelName + ":" + sheetName);

                if (xd.fieldInfos.Count == 0)
                    continue;
				string protoCode = GenProto(sheetName, xd.fieldInfos/*[convType_]*/);
				string protoFilename = protoPath + sheetName + ".proto";
				File.WriteAllText(protoFilename, protoCode);
			}
		}

		void GenValids()
		{
			Console.WriteLine("\n----------Gen Valid----------\n");
			foreach (ExcelData xd in excels)
			{
				string sheetName = xd.sheetName;
				Console.WriteLine("Gen Valid from " + xd.excelName + ":" + sheetName);

				string validCode = GenValidInfo(sheetName, xd.fieldInfos/*[convType_]*/);
				string validFilename = codePath + sheetName + "_valid.cs";
				File.WriteAllText(validFilename, validCode);
			}
		}

		void GetProtos()
		{
			foreach (ExcelData xd in excels)
			{
                if (xd.fieldInfos.Count != 0)
                {
                    protos.Add(xd.sheetName, xd.excelName);
                }
			}
		}

		void GenCSharps()
		{
			Console.WriteLine("\n----------Gen CSharp----------\n");
			List<Process> procs = new List<Process>();
			foreach (var p in protos)
			{
				Console.WriteLine("Gen CSharp from " + p.Value + ": " + p.Key);
				string protoFilename = protoPath + p.Key + ".proto";
				string codeFilename = codePath + p.Key + ".cs";
				string param = string.Format("-i:\"{0}\" -o:\"{1}\" -ns:{2} -q", protoFilename, codeFilename, ns);
				WaitProcess(procs, procNum);
                Process proc = ProcCmd(protogen, param, protoPath, false, true);
                string output = proc.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(output))
                {
                    if (output.Contains("warning"))
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    else
                        Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(output.TrimEnd(new char[] {'r', '\n'}));
                    Console.ResetColor();
                }
				procs.Add(proc);
			}
			WaitProcess(procs, 0);
		}

		void GenPythons()
		{
			Console.WriteLine("\n----------Gen Python----------");
            //string param = string.Format("--proto_path={0} --cpp_out={1} {0}*.proto", protoPath, codePath);
            //ProcCmd(protoc, param, protoPath, false);
            foreach (var fi in (new DirectoryInfo(protoPath)).GetFiles("*.proto"))
            {
                string param = string.Format("--proto_path={0} --cpp_out={1} {2}", protoPath, codePath, fi.FullName);
                Process p = ProcCmd(protoc, param, protoPath, false, true);
                string output = p.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(output))
                {
                    if (output.Contains("warning"))
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    else
                        Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(output.TrimEnd(new char[] {'r', '\n'}));
                    Console.ResetColor();
                }
                p.WaitForExit();
            }
		}

		void GetCodes()
		{
			codes = new List<string>();
			DirectoryInfo di = new DirectoryInfo(exlRelyCs);
			if (di.Attributes == FileAttributes.Directory)
			{
				foreach (FileInfo fi in di.GetFiles("*.cs"))
				{
					File.Copy(fi.FullName, codePath + fi.Name, true);
				}
			}
			else
			{
				File.Copy(di.FullName, codePath + di.Name, true);
			}

			foreach (string csFilename in Directory.GetFiles(codePath, "*.cs"))
			{
				codes.Add(File.ReadAllText(csFilename));
			}
		}

		void CompileFull()
		{
			Console.WriteLine("\n----------Compile Full DLL----------");
			fullLib = CompileCS(protoFull, null, null, codes.ToArray());
		}

		void CompileCore()
		{
			Console.WriteLine("\n----------Compile Core DLL----------");
			coreLib = CompileCS(protoCore, dllPath + exlDllName + ".dll", "/debug- /optimize+", codes.ToArray());
		}

		void CompilePre(string name, int size)
		{
			Console.WriteLine("\n----------Compile Serializer DLL----------");

			Type[] types = coreLib.GetTypes();

			RuntimeTypeModel tm = TypeModel.Create();

			int c = 0;
			int i = 0;
			foreach (var t in types)
			{
				if (t.Name.EndsWith("_ValidateInfo"))
				{
					continue;
				}

				tm.Add(t, true);
				i++;
				if (i > size)
				{
					string fn = name + c.ToString() + ".dll";
					tm.Compile(name.Replace("-", "_") + c.ToString(), fn);
					File.Move(Globals.Instance.curBase + fn, dllPath + fn);
					c++;
					tm = TypeModel.Create();
					i = 0;
				}
			}

			if (i > 0)
			{
				string fn = name + c.ToString() + ".dll";
				tm.Compile(name.Replace("-", "_") + c.ToString(), fn);
				File.Move(Globals.Instance.curBase + fn, dllPath + fn);
			}
		}

		void GenDatas()
		{
			if (fullLib == null)
				return;
			// .xlsx to .protodata.bytes
			Console.WriteLine("\n----------Gen Data----------\n");
			foreach (ExcelData xd in excels)
			{
				string sheetName = xd.sheetName;
				DataTable table = xd.table;
				Dictionary<string, FieldInfoX> fieldInfos = xd.fieldInfos/*[convType_]*/;
                if (fieldInfos.Count == 0)
                    continue;

				Console.WriteLine("Gen Data from " + xd.excelName + ":" + sheetName);
				string typename = ns + "." + sheetName;
				Type t = fullLib.GetType(typename);
				PropertyInfo[] properties = t.GetProperties();

				ConstructorInfo ci = t.GetConstructor(Type.EmptyTypes);
				List<IExtensible> list = new List<IExtensible>();
				for (int i = 1; i < table.Rows.Count; i++)
				{
					DataRow dr = table.Rows[i];
					IExtensible obj = ci.Invoke(new object[0]) as IExtensible;
					foreach (PropertyInfo pi in properties)
					{
						FieldInfoX fi = null;
						string v = null;
						object value;
						try
						{
							if (pi.Name.EndsWith("_raw"))
							{
								fi = fieldInfos[pi.Name.Substring(0, pi.Name.Length - 4)];
								v = dr[fi.index].ToString();
								value = v;
							}
							else
							{
								fi = fieldInfos[pi.Name];
								v = dr[fi.index].ToString();
								value = GetValue(fi, v, fullLib);
								if (fi.type == "TFloat")
								{
									Type tfloat = fullLib.GetType(ns + ".TFloat");
									ConstructorInfo tfloat_ci = tfloat.GetConstructor(Type.EmptyTypes);
									IExtensible o_tfloat = tfloat_ci.Invoke(new object[0]) as IExtensible;
									PropertyInfo pi_v = tfloat.GetProperty("v");
									pi_v.SetValue(o_tfloat, value, BindingFlags.SetProperty, null, null, null);
									value = o_tfloat;
								}
								else if (fi.type == "TTFloat")
								{
									Type ttfloat = fullLib.GetType(ns + ".TTFloat");
									ConstructorInfo ttfloat_ci = ttfloat.GetConstructor(Type.EmptyTypes);
									IExtensible o_ttfloat = ttfloat_ci.Invoke(new object[0]) as IExtensible;
									PropertyInfo pi_v = ttfloat.GetProperty("v");
									pi_v.SetValue(o_ttfloat, value, BindingFlags.SetProperty, null, null, null);
									value = o_ttfloat;
								}
							}
							// 一行空数据
							if (0 == fi.index && "" == v.Trim())
							{
								obj = null;
								break;
							}
							pi.SetValue(obj, value, BindingFlags.SetProperty, null, null, null);
						}
						catch (Exception ex)
						{
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine(string.Format("{0}, 行: {1}, 列: {2}", ex.Message, i + 1, fi.desc));
							Console.ResetColor();
						}
					}
					if (null != obj)
						list.Add(obj);
				}

				FileStream fs = new FileStream(dataPath + sheetName + ".protodata.bytes", FileMode.Create);
				BinaryWriter bw = new BinaryWriter(fs);
				bw.Write(t.FullName);
				bw.Write(list.Count);
				foreach (IExtensible obj in list)
					Serializer.NonGeneric.SerializeWithLengthPrefix(fs, obj, PrefixStyle.Fixed32, 0);
				fs.Close();
			}
		}

		void DoEnd()
		{
			if (convType == ConvType.Client)
			{
                if (!Directory.Exists(exlClientLibs))
                {
                    Console.WriteLine("找不到路径: " + exlClientLibs + " 是否创建(Y/N)?");
                    string pressKey = Console.ReadLine();
                    if (pressKey.ToLower() == "y")
                    {
                        Directory.CreateDirectory(exlClientLibs);
                        if (!Directory.Exists(exlClientLibs))
                        {
                            Console.WriteLine("创建路径: " + exlClientLibs + " 失败!");
                        }
                    }
                }

				foreach (var fi in (new DirectoryInfo(dllPath)).GetFiles("*.dll"))
				{
					File.Copy(fi.FullName, exlClientLibs + fi.Name, true);
				}
				foreach (var fi in (new DirectoryInfo(dataPath)).GetFiles("*.protodata.bytes"))
				{
					File.Copy(fi.FullName, exlClientData + fi.Name, true);
				}
			}
			else if (convType == ConvType.Server)
            {
                if (!Directory.Exists(exlServerLibs))
                {
                    Console.WriteLine("找不到路径: " + exlServerLibs + " 是否创建(Y/N)?");
                    string pressKey = Console.ReadLine();
                    if (pressKey.ToLower() == "y")
                    {
                        Directory.CreateDirectory(exlServerLibs);
                        if (!Directory.Exists(exlServerLibs))
                        {
                            Console.WriteLine("创建路径: " + exlServerLibs + " 失败!");
                        }
                    }
                }

                if (!Directory.Exists(exlServerData))
                {
                    Console.WriteLine("找不到路径: " + exlServerData + " 是否创建(Y/N)?");
                    string pressKey = Console.ReadLine();
                    if (pressKey.ToLower() == "y")
                    {
                        Directory.CreateDirectory(exlServerData);
                        if (!Directory.Exists(exlServerData))
                        {
                            Console.WriteLine("创建路径: " + exlServerData + " 失败!");
                        }
                    }
                }

                foreach (var fi in (new DirectoryInfo(codePath)).GetFiles("*.pb.h"))
                {
                    if (!CompareTwoFile(fi.FullName, exlServerLibs + fi.Name))
                    {
                        File.Copy(fi.FullName, exlServerLibs + fi.Name, true);
                        Console.WriteLine(string.Format("gen new file: {0}", fi.Name));
                    }
                }
                foreach (var fi in (new DirectoryInfo(codePath)).GetFiles("*.pb.cc"))
                {
                    if (!CompareTwoFile(fi.FullName, exlServerLibs + fi.Name))
                    {
                        File.Copy(fi.FullName, exlServerLibs + fi.Name, true);
                        Console.WriteLine(string.Format("gen new file: {0}", fi.Name));
                    }
                }

                foreach (var fi in (new DirectoryInfo(dataPath)).GetFiles("*.protodata.bytes"))
                {
                    File.Copy(fi.FullName, exlServerData + fi.Name, true);
                }
            }
			else if (convType == ConvType.Validate)
			{
				Console.WriteLine("\n------------------验证数据--------------------\n");
				ProtoData.ms_protoAssembly = fullLib;
				string[] fis = Directory.GetFiles(dataPath);
				foreach (string fi in fis)
				{
					byte[] buf = File.ReadAllBytes(fi);
					ProtoData pd = new ProtoData(buf);
				}
				foreach (ExcelData xd in excels)
				{
					ProtoData pd = null;
					ProtoData.ms_protoDatas.TryGetValue(xd.sheetName, out pd);
					if (null != pd)
					{
						Console.WriteLine("验证数据: " + xd.excelName + ":" + xd.sheetName);
						pd.Validate();
					}
				}
			}
		}
		
		// other method
		Dictionary<string, FieldInfoX> GenFieldInfos(DataRow header)
		{
			Dictionary<string, FieldInfoX> fieldInfos = new Dictionary<string, FieldInfoX>();
			int idx = 0;
			foreach (object o in header.ItemArray)
			{
				idx++;
				if (!o.ToString().Contains("|"))
					continue;
				FieldInfoX fi = GetFieldInfo(o.ToString());
				if (fi == null)
					continue;
				fi.index = idx - 1;
                try
                {
                    fieldInfos.Add(fi.name, fi);
                }
                catch (ArgumentException)
                {
                    Console.Write(fi.name + " 键重复");
                    throw new Exception(" ");
                }
			}
			return fieldInfos;
		}

		FieldInfoX GetFieldInfo(string content)
		{
			content = content.Replace("\r\n", "").Replace("\n", "");

			Match match = regex.Match(content);
			if (match.Success)
			{
				string flag = match.Groups["flag"].Value;
				if (convType == ConvType.Server && flag == "|") return null;
				if (convType == ConvType.Client && flag == "||") return null;

				string name = match.Groups["name"].Value;
				string type = match.Groups["type"].Value;
				string desc = match.Groups["desc"].Value;
				string constraint = match.Groups["constraint"].Value;

				FieldInfoX fi = new FieldInfoX();
				fi.name = name;
				fi.type = ConvertType(type);
				fi.desc = desc;
				fi.constraint = constraint;

				return fi;
			}
			else
			{
				throw new Exception("标题格式错误: " + content);
			}
		}

		string ConvertType(string type)
		{
			string newType = type.ToLower();
			if (newType.Contains("float"))
			{
				if (convType == ConvType.Client)
					return newType == "float" ? "TFloat" : newType == "ttfloat" ? "TTFloat" : type;
				else
					return newType == "ttfloat" ? "float" : type;
			}
			else
			{
				return type;
			}
		}

		object GetValue(FieldInfoX fi, string v, Assembly typelib)
		{
			object ret = null;
			try
			{
				switch (fi.type)
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
							string typename = ns_ + fi.type.Replace(".", "+");
							Type[] tt = typelib.GetTypes();
							Type t = typelib.GetType(typename);
							ret = Enum.Parse(t, v);
						}
						break;
				}
			}
			catch (Exception)
			{
			}
			return ret;
		}

		Assembly CompileCS(String refer, String output, String options, params string[] code)
		{
			CodeDomProvider domProvider = CodeDomProvider.CreateProvider("CSharp");
			CompilerParameters cp = new CompilerParameters();
			cp.GenerateExecutable = false;
			cp.GenerateInMemory = true;
			cp.ReferencedAssemblies.Add(csc20);
			if (null != refer)
				cp.ReferencedAssemblies.Add(refer);
			if (null != output)
				cp.OutputAssembly = output;
			if (null != options)
				cp.CompilerOptions = options;

			CompilerResults cr = domProvider.CompileAssemblyFromSource(cp, code);
			
			if (cr.Errors.Count > 0)
			{
				Console.WriteLine("compile error!");
				foreach (CompilerError ce in cr.Errors)
				{
					Console.WriteLine(string.Format("  {0}", ce.ToString()));
					Console.WriteLine("");
				}
			}
			System.Reflection.Assembly assembly = cr.CompiledAssembly;
			return assembly;
		}

		string GenProto(string messageName, Dictionary<string, FieldInfoX> fieldInfos)
		{
			StringBuilder sb = new StringBuilder();
            // protobuf 3x 版本中使用
            sb.Append("syntax=\"" + Globals.Instance.proto_syntax + "\";\n");
            foreach (string importName in protos.Keys)
                sb.Append("import \"" + importName + ".proto\";\n");
			
			sb.AppendFormat("\nmessage {0}\n{{\n", messageName);

			int c = 1;
			foreach (FieldInfoX fi in fieldInfos.Values)
			{
				if (convType == ConvType.Validate)
					sb.AppendFormat("\trequired string {0} = {1};\n", fi.name + "_raw", c++);
				else
					sb.AppendFormat("\trequired {0} {1} = {2};\n", fi.type, fi.name, c++);
			}

			sb.Append("}");

			return sb.ToString();
		}

		string GenValidInfo(string sheetname, Dictionary<string, FieldInfoX> fieldInfo)
		{
			string typename = sheetname + "_ValidateInfo";

			StringBuilder sb = new StringBuilder();

			sb.Append("using System;\n");
			sb.Append("using System.Collections.Generic;\n\n");
			sb.Append("namespace " + ns + "\n");
			sb.Append("{\n");
			sb.Append("\tpublic class " + typename + "\n");
			sb.Append("\t{\n");
			sb.Append("\t\tpublic static Dictionary<string, string> m_validateDefine = new Dictionary<string, string>();\n");
			sb.Append("\t\tstatic " + typename + "()\n");
			sb.Append("\t\t{\n");

			foreach (FieldInfoX fi in fieldInfo.Values)
			{
				string validateString = fi.constraint.Replace("\\", "\\\\");
				validateString = validateString.Replace("\"", "\\\"");
				if(validateString == "") continue;
				sb.AppendFormat("\t\t\tm_validateDefine.Add(\"{0}\", \"{1}\");\n", fi.name, validateString);
			}

			sb.Append("\t\t}\n");
			sb.Append("\t}\n");
			sb.Append("}");

			return sb.ToString();
		}

		void ClearDirectory(string path)
		{
			foreach (string file in Directory.GetFiles(path))
				File.Delete(file);
		}

		void EnsureDirectory(string path)
		{
			DirectoryInfo di = new DirectoryInfo(path);
			if (!di.Exists)
				di.Create();
		}

		Process ProcCmd(string exe, string param, string workDir, bool useShell, bool redirectError)
		{
			ProcessStartInfo psi = new ProcessStartInfo();
			psi.FileName = exe;
			psi.Arguments = param;
			psi.WorkingDirectory = workDir;
			psi.UseShellExecute = useShell;
            psi.RedirectStandardError = redirectError;
			return System.Diagnostics.Process.Start(psi);
		}

		void WaitProcess(List<Process> procs, int remainCount)
		{
			while (GetActiveProcessNum(procs) > remainCount)
			{
				Thread.Sleep(10);
			}
		}

		int GetActiveProcessNum(List<Process> procs)
		{
			for (int i = 0; i < procs.Count; ++i)
			{
				if (procs[i].HasExited)
				{
					procs.RemoveAt(i);
					--i;
				}
			}
			return procs.Count;
		}

        bool CompareTwoFile(string filePath1, string filePath2)
        {
            //创建一个哈希算法对象 
            using (HashAlgorithm hash = HashAlgorithm.Create())
            {
                try
                {
                    FileStream file1 = new FileStream(filePath1, FileMode.Open);
                    FileStream file2 = new FileStream(filePath2, FileMode.Open);

                    byte[] hashByte1 = hash.ComputeHash(file1);//哈希算法根据文本得到哈希码的字节数组 
                    byte[] hashByte2 = hash.ComputeHash(file2);
                    string str1 = BitConverter.ToString(hashByte1);//将字节数组装换为字符串 
                    string str2 = BitConverter.ToString(hashByte2);
                    file1.Close();
                    file2.Close();
                    return (str1 == str2);//比较哈希码 
                }
                catch (Exception e)
                {
                    return false;
                }
            } 
        }
	}
}
