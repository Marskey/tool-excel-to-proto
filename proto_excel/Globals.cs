using System;
using System.Collections;
using System.IO;

namespace proto_excel
{
	class Globals
	{
		private static Globals instance = null;
		public static Globals Instance
		{
			get
			{
				if (null == instance)
					instance = new Globals();
				return instance;
			}
		}

		private Config cfg;

		public string curBase;
		public string appBase;
		public string TempDir;
		
		public string NameSpace;
		public string protoc;
		public string protogen;
		public string proto_full;
		public string proto_core;
		public string csc20;
        public string proto_syntax;

		public string exl;
		public string exl_dll_name;
		public string exl_pre_name;
		public string exl_pre_size;
		public string exl_client_libs;
		public string exl_client_data;
		public string exl_server_libs;
		public string exl_server_data;

		public string msg;
		public string msg_dll_name;
		public string msg_pre_name;
		public string msg_pre_size;
		public string msg_client_libs;
		public string msg_server_libs;
		public string exl_rely_proto;
		public string exl_rely_cs;

		public Globals()
		{
			curBase = Path.GetFullPath(".\\");
			appBase = AppDomain.CurrentDomain.BaseDirectory;
			//Console.WriteLine("CurBase: " + curBase);
			//Console.WriteLine("AppBase: " + appBase);

			cfg = new Config("Globals");
			NameSpace		= cfg["NameSpace"];
			exl_dll_name	= cfg["exl_dll_name"];
			exl_pre_name	= cfg["exl_pre_name"];
			exl_pre_size	= cfg["exl_pre_size"];
			msg_dll_name	= cfg["msg_dll_name"];
			msg_pre_name	= cfg["msg_pre_name"];
			msg_pre_size	= cfg["msg_pre_size"];
			csc20			= cfg["csc20"];
            proto_syntax    = cfg["proto_syntax"];

			cfg = new Config("RelativePaths");
			TempDir			= GetFullPath(cfg["TempDir"]);
			exl				= GetFullPath(cfg["exl"]);
			exl_client_libs = GetFullPath(cfg["exl_client_libs"]);
			exl_client_data = GetFullPath(cfg["exl_client_data"]);
			exl_server_libs = GetFullPath(cfg["exl_server_libs"]);
			exl_server_data = GetFullPath(cfg["exl_server_data"]);
			exl_rely_proto	= GetFullPath(cfg["exl_rely_proto"]);
			exl_rely_cs		= GetFullPath(cfg["exl_rely_cs"]);
			msg				= GetFullPath(cfg["msg"]);
			msg_client_libs = GetFullPath(cfg["msg_client_libs"]);
			msg_server_libs = GetFullPath(cfg["msg_server_libs"]);
			protoc			= GetFullPath(cfg["protoc"]);
			protogen		= GetFullPath(cfg["protogen"]);
			proto_full		= GetFullPath(cfg["proto_full"]);
			proto_core		= GetFullPath(cfg["proto_core"]);

			//FileInfo fi = new FileInfo(exl_rely_proto);
			//string n = Path.GetFileNameWithoutExtension(fi.Name);
		}

		private string GetFullPath(string path)
		{
			return Path.GetFullPath(appBase + path);
		}
	}
}

