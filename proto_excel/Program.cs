using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace proto_excel
{
	class Program
	{
		//static string exlHeader = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};Extended Properties='Excel 12.0 XML;HDR=NO;IMEX=1;'";
		static string exlPath = Globals.Instance.exl;
		static List<ExcelData> excelDatas = new List<ExcelData>();

		static void Main(string[] args)
		{
			List<Convertor> convs = new List<Convertor>();
#if DEBUG
			args = new string[1];
            args[0] = "server";
#endif
			string type = null;
			int procNum = 0;
			if (0 == args.Length || 3 < args.Length)
			{
				PrintUsage();
				return;
			}
			//else if (args[0] == "all")
			//{
			//	convs.Add(new Convertor("client"));
			//	convs.Add(new Convertor("server"));
			//	convs.Add(new Convertor("validate"));
			//}
			//else if (args[0] == "both")
			//{
			//	convs.Add(new Convertor("client"));
			//	convs.Add(new Convertor("server"));
			//}
			else if (args[0] == "client")
			{
				//convs.Add(new Convertor("client"));
				type = args[0];
			}
			else if (args[0] == "server")
			{
				//convs.Add(new Convertor("server"));
				type = args[0];
			}
			else if (args[0] == "validate")
			{
				//convs.Add(new Convertor("validate"));
				type = args[0];
			}
			else
			{
				PrintUsage();
				return;
			}

			if (1 < args.Length)
			{
				procNum = int.Parse(args[1]);
			}

			if (procNum < 1)
				procNum = 3;

			Stopwatch sw = new Stopwatch();
			sw.Start();

			try
			{
                ReadXml();
				//ReadExcel();
				//StartThreadRun(convs);
				Convertor c = new Convertor(type, procNum);
				c.SetData(excelDatas);
				c.ThreadRun();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			sw.Stop();
			Console.WriteLine("\nusing time: " + sw.ElapsedMilliseconds / 1000.0f);
			Console.ReadKey();
		}

		private static void PrintUsage()
		{
			Console.WriteLine("usage: proto_excel {type} [proc=3]");
			Console.WriteLine(" type: client, server, validate");
			Console.WriteLine(" proc: 最大进程数，默认值为3");
			//Console.WriteLine("type: all , both , client , server , validate");
			//Console.WriteLine("      all = client && server && validate");
			//Console.WriteLine("      both= client && server");
			Console.ReadKey();
		}

        static void ReadXml()
        {
            foreach (string xmlPath in Directory.GetFiles(exlPath, "*.xml"))
            {
                string xmlName = Path.GetFileNameWithoutExtension(xmlPath);

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);

                Console.WriteLine("Adding " + xmlPath + "...");

                DataSet ds = new DataSet();
                //获取Xml字串            
                //Console.WriteLine(xmlDoc.InnerXml);
                StringReader sr = new StringReader(xmlDoc.InnerXml);
                XmlTextReader xr = new XmlTextReader(sr);
                ds.ReadXml(xr);

                Console.WriteLine("Rows: " + ds.Tables[0].Rows.Count.ToString());

                ExcelData md = new ExcelData();
                md.excelName = xmlName;
                md.sheetName = xmlName;
                md.table = ds.Tables[0];
                excelDatas.Add(md);
            }
        }


// 		static void ReadExcel()
// 		{
// 			foreach (string excelPath in Directory.GetFiles(exlPath, "*.xlsx"))
// 			{
// 				string excelName = Path.GetFileNameWithoutExtension(excelPath);
// 				// 正在编辑的Excel文件会生成一个临时文件，并以~$开头
// 				if (excelName.StartsWith("~$"))
// 					continue;
// 				string connectionString = string.Format(exlHeader, excelPath);
// 				OleDbConnection CNN = new OleDbConnection(connectionString);
// 				while (true)
// 				{
// 					try
// 					{
// 						CNN.Open();
// 						break;
// 					}
// 					catch (InvalidOperationException)
// 					{
// 						Thread.Sleep(10);
// 					}
// 					catch (OleDbException)
// 					{
// 						Thread.Sleep(10);
// 					}
// 				}
// 				DataTable sheets = CNN.GetSchema("Tables");
// 				foreach (DataRow sheet in sheets.Rows)
// 				{
// 					if (sheet["Table_Type"].ToString() == "TABLE")
// 					{
// 						string sheetName = sheet["Table_Name"].ToString();
//                         if (!sheetName.EndsWith("Config$"))
// 							continue;
// 
// 						OleDbDataAdapter adpt = new OleDbDataAdapter("select * from [" + sheetName + "]", CNN);
// 						DataTable table = new DataTable();
// 						adpt.Fill(table);
// 
// 						if (table.Rows.Count < 1)
// 							continue;
// 
// 						sheetName = sheetName.Replace("$", "");
// 						ExcelData md = new ExcelData();
// 						md.excelName = excelName;
// 						md.sheetName = sheetName;
// 						md.table = table;
// 						//md.fieldInfos = new Dictionary<string, FieldInfo>[(int)ConvType.Max];
// 						excelDatas.Add(md);
// 					}
// 				}
// 				CNN.Close();
// 			}
// 		}

		//static void StartThreadRun(List<Convertor> convs)
		//{
		//	var threads = new List<Thread>();
		//	for (int i = 0; i < convs.Count; ++i)
		//	{
		//		Convertor conv = convs[i];
		//		conv.SetData(excelDatas);
		//		Thread thread = new Thread(new ThreadStart(conv.ThreadRun));
		//		threads.Add(thread);
		//		thread.Start();
		//	}

		//	for (int i = 0; i < convs.Count; ++i)
		//	{
		//		threads[i].Join();
		//	}
		//}
	}
}
