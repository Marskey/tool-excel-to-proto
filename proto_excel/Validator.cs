using System;
using System.Collections.Generic;
using System.Reflection;
using ProtoBuf;

namespace proto_excel
{
    public abstract class Validator
    {
		protected static string ns = Globals.Instance.NameSpace;
		protected static string ns_ = ns + ".";
        
		public static Validator Create(string str)
        {
            List<Validator> v = CreateValidators(str);

            return v[0];
        }

        public abstract bool validate(string v);

        public static List<Validator> CreateValidators(string str)
        {
            int p0 = str.IndexOf('(');
            int p1 = FindPair(str, '(', p0 + 1);

            string typename = str.Substring(0, p0);
            string param = (p1 - p0 - 1 <= 0) ? "" : str.Substring(p0 + 1, p1 - p0 - 1);

			Type t = Type.GetType("proto_excel." + typename + "Validator");
            if (t == null)
            {
                throw new Exception("Invalid validator: " + typename);
            }

            List<Validator> ret = new List<Validator>();
            if (str[p0 + 1] == '"')
            {
                ConstructorInfo ci = t.GetConstructor(new Type[] { typeof(string) });
                Validator v = ci.Invoke(new object[] { param.Trim('\"') }) as Validator;
                if (v == null)
                    throw new Exception("Failed to create validator: " + typename);
                ret.Add(v);
            }
            else if (str[p0 + 1] == ')')
            {
                ConstructorInfo ci = t.GetConstructor(Type.EmptyTypes);
                Validator v = ci.Invoke(new object[0]) as Validator;
                if (v == null)
                    throw new Exception("Failed to create validator: " + typename);
                ret.Add(v);
            }
            else
            {

                List<Validator> childValidators = CreateValidators(param);
                Type[] types = new Type[childValidators.Count];
                for (int i = 0; i < types.Length; i++)
                    types[i] = typeof(Validator);
                ConstructorInfo ci = t.GetConstructor(types);
                Validator v = ci.Invoke(childValidators.ToArray()) as Validator;
                if(v == null)
                    throw new Exception("Failed to create validator: " + typename);
                ret.Add(v);
            }

            if (p1 + 1 < str.Length && str[p1 + 1] == ',')
            {
                List<Validator> v = CreateValidators(str.Substring(p1 + 2));
                ret.AddRange(v);
            }
            return ret;
        }

        public static int FindPair(string s, char p0, int i)
        {
            char p1 = ' ';
            if(p0 == '(')
                p1 = ')';

            int depth = 0;
            while (i < s.Length)
            {
                if (s[i] == p1)
                {
                    if (depth == 0)
                        return i;
                    else
                        depth--;
                }
                else if (s[i] == p0)
                    depth++;
                i++;
            }
            return -1;
        }

    }

    public class NullValidator : Validator
    {
        public NullValidator()
        {

        }

        public override bool validate(string v)
        {
            return string.IsNullOrEmpty(v.Trim());
        }
    }

    public class NotValidator : Validator
    {
        Validator m_v;
        public NotValidator(Validator v)
        {
            m_v = v;
        }

        public override bool validate(string v)
        {
            return !m_v.validate(v);
        }
    }

    public class AndValidator : Validator
    {
        Validator m_v1;
        Validator m_v2;

        public AndValidator(Validator v1, Validator v2)
        {
            m_v1 = v1;
            m_v2 = v2;
        }

        public override bool validate(string v)
        {
            return m_v1.validate(v) && m_v2.validate(v);
        }
    }

    public class OrValidator : Validator
    {
        Validator m_v1;
        Validator m_v2;

        public OrValidator(Validator v1, Validator v2)
        {
            m_v1 = v1;
            m_v2 = v2;
        }

        public override bool validate(string v)
        {
            return m_v1.validate(v) || m_v2.validate(v);
        }
    }

    public class RangeValidator : Validator
    {
        int m_min;
        int m_max;

        public RangeValidator(string param)
        {
            string[] p = param.Split(',');
            m_min = int.Parse(p[0]);
            m_max = int.Parse(p[1]);
        }

        public override bool validate(string v)
        {
            try
            {
                int i = int.Parse(v);
                return i >= m_min && i <= m_max;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
	
	public class FloatRangeValidator : Validator
	{
        float m_min;
        float m_max;

        public FloatRangeValidator(string param)
        {
            string[] p = param.Split(',');
            m_min = float.Parse(p[0]);
            m_max = float.Parse(p[1]);
        }

        public override bool validate(string v)
        {
            try
            {
                float f = float.Parse(v);
                return f >= m_min && f <= m_max;
            }
            catch (Exception)
            {
                return false;
            }
        }
	}

    public class EnumValidator : Validator
    {
        Type m_enumType;
        public EnumValidator(string param)
        {
            m_enumType = ProtoData.ms_protoAssembly.GetType(ns_ + "Enums+" + param);
        }

        public override bool validate(string v)
        {
            try
            {
                Enum.Parse(m_enumType, v);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }

    public class IDValidator : Validator
    {
        string m_tableName;

        public IDValidator(string param)
        {
            m_tableName = param;
        }

        public override bool validate(string v)
		{
			ProtoData pd = ProtoData.ms_protoDatas[m_tableName];
			Type t = pd.Type;
			PropertyInfo pi = t.GetProperty("id_raw");
			// 如果是第一次使用该外键，建立查找字典
			if (null == pd.m_dataDic)
			{
				var dic = new Dictionary<int, IExtensible>();
				for (int i = 0; i < pd.Count; i++)
				{
					IExtensible o = pd[i];
					int id = int.Parse((string)pi.GetValue(o, null));
					// 主键重复
					try
					{
						dic.Add(id, o);
					}
					catch (ArgumentException)
					{
						if ("CutsceneConfig" != m_tableName)
							Console.WriteLine("  " + m_tableName + "表包含重复的ID:" + id);
					}
				}
				pd.m_dataDic = dic;
			}

            try
            {
                int vid = int.Parse(v);
				return pd.m_dataDic.ContainsKey(vid);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}


