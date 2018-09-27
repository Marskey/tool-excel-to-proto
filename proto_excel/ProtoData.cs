using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProtoBuf;

namespace proto_excel
{
    public class ProtoData
    {
        public static Assembly ms_protoAssembly;
		public static Dictionary<string, ProtoData> ms_protoDatas = new Dictionary<string, ProtoData>();
		static string ns = Globals.Instance.NameSpace;
		static string ns_ = ns + ".";

		Type m_type;
		IExtensible[] m_dataItems;
		public Dictionary<int, IExtensible> m_dataDic;
		Dictionary<string, Validator> m_validators = new Dictionary<string, Validator>();

        public string TypeName
        {
            get;
            private set;
        }

        public Type Type
        {
            get { return m_type; }
        }

        public ProtoData(byte[] buf)
        {
            MemoryStream ms = new MemoryStream(buf);
            BinaryReader br = new BinaryReader(ms);
            string typename = br.ReadString();
            uint size = br.ReadUInt32();
			m_dataItems = new IExtensible[size];

            System.Type type = ms_protoAssembly.GetType(typename);
            TypeName = typename.Replace(ns_, "");
            m_type = type;

			Console.WriteLine("验证表头: " + TypeName);

            Type validateType = ms_protoAssembly.GetType(typename + "_ValidateInfo");
            FieldInfo fi = validateType.GetField("m_validateDefine");
            Dictionary<string, string> validateInfo = fi.GetValue(null) as Dictionary<string, string>;

            foreach (var pair in validateInfo)
            {
                try
                {
                    Validator v = Validator.Create(pair.Value);
                    m_validators.Add(pair.Key, v);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("  create validator failed: {0}\n    Error message: {1}", pair.Key, ex.Message));
                }
            }
            
            for (int i = 0; i < size; i++)
            {
				int len = br.ReadInt32();
				byte[] itemBuf = br.ReadBytes(len);
				m_dataItems[i] = Serializer.NonGeneric.Deserialize(type, new MemoryStream(itemBuf)) as IExtensible;
            }
            ms_protoDatas.Add(TypeName, this);
        }

        public int Count
        {
            get
            {
                return m_dataItems.Length;
            }
        }

        public IExtensible this[int index]
        {
            get
            {
                return m_dataItems[index];
            }
        }

        public void Validate()
        {
            if (m_validators.Count == 0)
                return;

            for (int i = 0; i < m_dataItems.Length; i++)
            {
                object o = m_dataItems[i];
				foreach (var pair in m_validators)
				{
					PropertyInfo pi = m_type.GetProperty(pair.Key + "_raw");
					string s = pi.GetValue(o, null) as string;
					if (!pair.Value.validate(s))
					{
						Console.WriteLine(string.Format("  数据错误： 行: {0}， 列: {1}", i + 3, pair.Key));
					}
				}
            }
        }
    }

}
